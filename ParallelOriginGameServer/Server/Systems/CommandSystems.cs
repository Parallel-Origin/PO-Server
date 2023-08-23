using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Drawing;
using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.ECS.Events;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.Persistence;
using ParallelOriginGameServer.Server.ThirdParty;
using ZLogger;
using Character = ParallelOrigin.Core.ECS.Components.Character;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;
using Identity = ParallelOrigin.Core.ECS.Components.Identity;
using Item = ParallelOrigin.Core.ECS.Components.Item;
using NotImplementedException = System.NotImplementedException;
using Type = ParallelOriginGameServer.Server.Persistence.Type;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which controlls all systems which process commands.
///     Basically entities which contain logic to do something once... logic which is outstanding and doesnt really belong to an entity itself like inventory mechanics or certain activities.
/// </summary>
public sealed class CommandGroup : Group<float>
{
    public CommandGroup(World world) : base(
        // Commands
        new EntityCommandSystem(world),
        new ChunkCommandSystem(world),
        new ClickedCommandSystem(world),
        new DoubleClickedCommandSystem(world),
        new TeleportationCommandSystem(world),
        new PopUpCommandSystem(world),
        new InventoryCommandSystem(world),
        new BuildCommandSystem(world),
        new PickupCommandSystem(world),
        new ChatCommandSystem(world)
    )
    {
    }
}

/// <summary>
///     An interface to define executeable logic. 
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IExecute<T>
{
    public World World { get; set; }
    public void Initialize(){}
    public void Execute(ref T command);
}

/// <summary>
/// Creates a system which records commands and executes an method upon each one during update. 
/// </summary>
/// <typeparam name="T0">The command.</typeparam>
/// <typeparam name="T1">The <see cref="IExecute{T}"/> implemenation.</typeparam>
public class CommandSystem<T0,T1> : BaseSystem<World, float> where T1 : struct, IExecute<T0>
{
    /// <summary>
    /// The recorded commands.
    /// </summary>
    private static List<T0> Commands { get; } = new (512);

    /// <summary>
    /// The <see cref="IExecute{T}"/> instance being inlined and executed on each command. 
    /// </summary>
    private T1 _instance;
    
    public CommandSystem(World world) : base(world)
    {
        World = world;
        _instance = new T1
        {
            World = World
        };
        _instance.Initialize();
    }

    /// <summary>
    ///     The world.
    /// </summary>
    public World World { get; set; }

    /// <summary>
    ///     Called upon update, executes all registered commands.
    /// </summary>
    /// <param name="state"></param>
    public override void Update(in float state)
    {
        for (var index = 0; index < Commands.Count; index++)
        {
            var item = Commands[index];
            _instance.Execute(ref item);
        }
        Commands.Clear();
    }

    /// <summary>
    ///     Adds a new command to the list.
    /// </summary>
    /// <param name="command"></param>
    public static void Add(in T0 command)
    {
        Commands.Add(command);
    }
    
    /// <summary>
    ///     Disposes this instance.
    /// </summary>
    public override void Dispose() { Commands.Clear(); }
}

/// <summary>
///     Executes logic on <see cref="EntityCommand"/>s to forward those to the server entities. 
/// </summary>
public struct ExecuteEntityCommand : IExecute<EntityCommand>
{
    public World World { get; set; }
    public void Execute(ref EntityCommand command)
    {
        // Get entity and mark it for destruction.
        var entity = World.GetById(command.Id);
        if (!entity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Client destroy request", LogStatus.EntityNotAlive, command.Id);
            return;
        }

        var cb = World.Record();
        cb.Add(in entity, new Destroy());
    }
}

/// <summary>
///     Executes a <see cref="ChunkCommand"/> and creates and loads <see cref="Chunk"/> entities accordingly.
/// </summary>
public struct ExecuteChunkCommand : IExecute<ChunkCommand>
{
    public World World { get; set; }
    public void Execute(ref ChunkCommand command)
    {
        switch (command.Operation)
        {
            case ChunkOperation.CREATE:

                // Create chunks which arent present in the database yet. 
                foreach (var grid in command.Grids)
                {
                    var newChunkEntity = World.CreateChunk(in grid);
                    ref var chunk = ref newChunkEntity.Get<Chunk>();
                    chunk.LoadedBy.Add(command.By);
                    
                    EventBus.Send(new ChunkCreatedEvent(newChunkEntity, grid));
                    Program.Logger.ZLogInformation(Logs.Chunk, "Created", newChunkEntity, chunk.Grid.X, chunk.Grid.Y);
                }

                break;

            case ChunkOperation.LOADED:

                foreach (var chunkDto in command.Chunks)
                {
                    var existingChunkEntity = World.CreateChunk(in chunkDto);
                    ref var chunk = ref existingChunkEntity.Get<Chunk>();
                    chunk.LoadedBy.Add(command.By);

                    foreach (var chunkResource in chunkDto.ContainedResources) chunkResource.ToEcs();
                    foreach (var chunkStructure in chunkDto.ContainedStructures) chunkStructure.ToEcs();
                    foreach (var chunkMob in chunkDto.ContainedMobs) chunkMob.ToEcs();

                    EventBus.Send(new ChunkCreatedEvent(existingChunkEntity, new Grid(chunkDto.X, chunkDto.Y)));
                    Program.Logger.ZLogInformation(Logs.Chunk, "Loaded", existingChunkEntity, chunk.Grid.X, chunk.Grid.Y);
                }

                break;
        }
    }
}

/// <summary>
///     Executes a <see cref="ClickCommand"/> and applies to to the entity.
/// </summary>
public struct ExecuteClickedCommand : IExecute<ClickCommand>
{
    public World World { get; set; }
    
    public void Execute(ref ClickCommand command)
    {
        // Resolve
        ref var clickerReference = ref command.Clicker;
        ref var clickedReference = ref command.Clicked;

        var clickerEntity = clickerReference.Resolve(World);
        var clickedEntity = clickedReference.Resolve(World);
        
        if (!clickedEntity.IsAlive() || !clickerEntity.IsAlive())
            return;

        // Either create clicked or enable it.
        if (!clickedEntity.Has<Toggle<Clicked>>())
        {
            clickedEntity.Add(new Toggle<Clicked>(new Clicked(2), true));
        }
        
        // Add the clicker to the clicked list. 
        ref var clicked = ref clickedEntity.Get<Toggle<Clicked>>();
        clicked.Enabled = true;
        clicked.Component.Clickers.Get().Add(clickerEntity);

        Program.Logger.ZLogDebug(Logs.Action, "Clicked entity", LogStatus.Sucessfull, clickerEntity, clickedEntity);
    }
}

/// <summary>
///     Executes the <see cref="DoubleClickCommand"/> and makes entities move.
/// </summary>
public struct ExecuteDoubleClickCommand : IExecute<DoubleClickCommand>
{
    public World World { get; set; }
    public void Execute(ref DoubleClickCommand command)
    {
        // Resolve
        ref var entityReference = ref command.Clicker;
        var entity = entityReference.Resolve(World);

        // Prevent exception when we try to move a non-existant entity. 
        if (!entity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Move to", LogStatus.EntityNotAlive, entity);
            return;
        }

        // Abort chop or build activities 
        if (entity.Has<Chop>())
        {
            entity.Remove<Chop>();
        }
        if (entity.Has<Build>())
        {
            entity.Remove<Build>();
        }

        // Update movement target
        ref var movement = ref entity.Get<Movement>();
        movement.Target = command.Position;

        Program.Logger.ZLogInformation(Logs.SingleAction, "Moving to", movement.Target, entity);
    }
}

/// <summary>
///     Executes a <see cref="TeleportationCommand"/> and applies it to the entity. 
/// </summary>
public struct ExecuteTeleportCommand : IExecute<TeleportationCommand>
{
    public World World { get; set; }
    public ServerNetwork ServerNetwork { get; set; }
    
    public void Initialize()
    {
        ServerNetwork = Program.Network;
    }

    public void Execute(ref TeleportationCommand command)
    {
        var clicker = (Entity)command.Entity;
        ref var pos = ref command.Position;
        
        // Teleport clicker to the clicked entity
        ref var clickerTransform = ref clicker.Get<NetworkTransform>();
        ref var movement = ref clicker.Get<Movement>();
        clickerTransform.Pos = pos;
        movement.Target = pos;

        // Update client
        var mapCommand = new MapCommand { Position = command.Position };
        ServerNetwork.Send(ref command);
        ServerNetwork.Send(ref mapCommand);
        Program.Logger.ZLogInformation(Logs.SingleAction, "Teleported", pos, clicker);
    }
}

/// <summary>
///     Executes a <see cref="PopUpCommand"/> and spawns a popup. 
/// </summary>
public struct ExecutePopupCommand : IExecute<PopUpCommand>
{

    private EntityPrototyperHierarchy _prototyperHierarchy;
    public World World { get; set; }

    public void Initialize()
    {
        _prototyperHierarchy = ServiceLocator.Get<EntityPrototyperHierarchy>();
    }

    public void Execute(ref PopUpCommand command)
    {
        // Get owner and target identity refs
        ref var ownerIdentity = ref command.Owner.Get<Identity>();
        ref var targetIdentity = ref command.Target.Get<Identity>();

        // Clone and configurate the entity which is ready to get spawned. 
        var entity = _prototyperHierarchy.Clone(command.Type);
        ref var identity = ref entity.Get<Identity>();
        ref var popup = ref entity.Get<Popup>();
        ref var parent = ref entity.Get<Parent>();

        // Assign owner and the target of the popup
        popup.Owner = new EntityLink(command.Owner, ownerIdentity.Id);
        popup.Target = new EntityLink(command.Target, targetIdentity.Id);

        // Fire event & initialize
        EventBus.Send(new CreateEvent(entity, ref identity));

        // Create options as childs for the popup
        foreach (var optionType in popup.Options.Get())
        {
            var optionEntity = _prototyperHierarchy.Clone(optionType);
            ref var optionIdentity = ref optionEntity.Get<Identity>();
            ref var child = ref optionEntity.Get<Child>();

            // Assign relation between the child and its parent 
            child.Parent = new EntityLink(entity, identity.Id);
            EventBus.Send(new CreateEvent(optionEntity, ref optionIdentity));
            
            parent.Children.Add(new EntityLink(optionEntity, optionIdentity.Id));
        }

        Program.Logger.ZLogDebug(Logs.Action, "Opened Popup", entity, popup.Owner.Entity, popup.Target.Entity);
    }
}

/// <summary>
/// Executes a <see cref="InventoryCommand"/>.
/// </summary>
public struct ExecuteInventoryCommand : IExecute<InventoryCommand>
{

    private EntityPrototyperHierarchy _prototyperHierarchy;
    public World World { get; set; }
    
    public void Initialize()
    {
        _prototyperHierarchy = ServiceLocator.Get<EntityPrototyperHierarchy>();
    }

    public void Execute(ref InventoryCommand command)
    {
        ref var inventoryEntity = ref command.Inventory;
        ref var inventory = ref inventoryEntity.Get<Inventory>();

        switch (command.OpCode)
        {
            case InventoryOperation.ADD:

                // Add entity because we provided a type
                var prefaItemEntity = _prototyperHierarchy.Get(command.Type);
                ref var prefabItem = ref prefaItemEntity.Get<Item>();
                
                // Either add and merge or just add
                if (prefabItem.Stackable)
                {
                    var created = inventory.AddOrMerge(in _prototyperHierarchy, inventoryEntity, command.Amount, command.Type, out var itemEntity);
                    if (created)
                    {
                        // Mark entity with an additional component
                        var addedItemEvent = new ItemAddedEvent(inventoryEntity, itemEntity);
                        EventBus.Send(addedItemEvent);
                    }
                    else
                    {
                        // Mark entity with in with an additional component
                        var updatedItemEvent = new ItemUpdatedEvent(inventoryEntity, itemEntity);
                        EventBus.Send(updatedItemEvent);
                    }
                }
                else
                {
                    // Create item entity and put it into the inventory, mark it as in inventory
                    var itemEntity = inventory.Add(in _prototyperHierarchy, inventoryEntity, command.Amount, command.Type);
                    ref var inInventory = ref itemEntity.Get<InInventory>();
                    inInventory.Inventory = inventoryEntity;

                    // Mark entity with an additional component
                    var addedItemEvent = new ItemAddedEvent(inventoryEntity, itemEntity);
                    EventBus.Send(addedItemEvent);
                }

                break;

            case InventoryOperation.SUBSTRACT:
                
                // Either just subtract the amount or delete it completly
                var removed = inventory.SubstractOrRemove(command.Type, command.Amount, out var entity);
                if (removed)
                {
                    // Mark it
                    entity.Add<Destroy>();
                    var removedItemEvent = new ItemRemovedEvent(inventoryEntity, entity);
                    EventBus.Send(removedItemEvent);
                }
                else
                {
                    var updatedItemEvent = new ItemUpdatedEvent(inventoryEntity, entity);
                    EventBus.Send(updatedItemEvent);
                }
                break;
        }
    }
}

/// <summary>
/// Executes the <see cref="ChatMessageCommand"/>
/// </summary>
public struct ChatCommandExecute : IExecute<ChatMessageCommand>
{
    public World World { get; set; }
    
    public void Execute(ref ChatMessageCommand command)
    {
        // Get player who sended the message
        var charEntity = World.GetCharacter(command.SenderUsername);
        ref var identity = ref charEntity.Get<Identity>();
        ref var character = ref charEntity.Get<Character>();
        ref var model = ref charEntity.Get<Model>();
        var account = model.ModelDto as Account;

        // Check if hes a moderator and invoke typed command
        if (account.Type.Equals(Type.MODERATION) || account.Type.Equals(Type.ADMIN))
        {
            // Commands will not be forwarded to all clients, only to the caller 
            if (command.Message[0] == '/')
            {
                Program.Network.Send(character.Peer.Get(), ref command);
                Program.CommandUserId.Value = identity.Id;

                try
                {
                    Program.Command.Invoke(command.Message);
                }
                catch (Exception e)
                {
                    Program.Logger.ZLogError(e.Message);
                }
            }
            else
            {
                // Redirect message to all other connected clients and push it back to the global message circular buffer..
                Program.Network.Send(ref command);
                Program.GlobalChatMessages.PushBack(command);
            }
        }
        else
        {
            // Redirect message to all other connected clients and push it back to the global message circular buffer..
            Program.Network.Send(ref command);
            Program.GlobalChatMessages.PushBack(command);
        }

        Program.Logger.ZLogInformation(Logs.Message, command.Message, command.Channel, command.SenderUsername);
    }
}

/// <summary>
/// Executes the <see cref="BuildCommand"/>
/// </summary>
public struct BuildCommandExecute : IExecute<BuildCommand>
{
    private EntityPrototyperHierarchy _prototyperHierarchy;
    public World World { get; set; }
    
    public void Initialize()
    {
        _prototyperHierarchy = ServiceLocator.Get<EntityPrototyperHierarchy>();
    }

    public void Execute(ref BuildCommand command)
    {
        // Get components
        var entity = command.Builder.Resolve(World);
        ref var inventory = ref entity.Get<Inventory>();
        ref var transform = ref entity.Get<NetworkTransform>();
        ref var movement = ref entity.Get<Movement>();

        var recipeEntity = _prototyperHierarchy.Get(command.Recipe);
        ref var recipe = ref recipeEntity.Get<Recipe>();
        ref var buildRecipe = ref recipeEntity.Get<BuildingRecipe>();

        if (!inventory.Has(ref recipe)) return;


        // Prepare
        var type = recipe.Craftables[0].Type;
        var spot = movement.Target;

        // Choose spot and make sure it can be build... 
        switch (buildRecipe.Spot)
        {
            case BuildSpot.Spot:
                spot = movement.Target;
                break;

            case BuildSpot.Tile:

                // Calculate middle of tile at zoom 15
                var tile = TileExtensions.GeoToTile(in transform.Pos.X, in transform.Pos.Y, 14);
                spot = tile.Middle;
                break;
        }

        switch (buildRecipe.BuildCondition)
        {
            case BuildCondition.None:
                break;

            case BuildCondition.FreeSpace:

                var size = 0.01f;
                var rect = new RectangleF((float)spot.X - size / 2, (float)spot.Y - size / 2, size, size);
                var free = !World.ExistsInRange(rect, type);

                if (!free) return;
                break;
        }

        // Attach build component which manages the build process
        entity.Set(new Build {
    
            Abortable = true,
            Ingredients = recipe.Ingredients,
            Position = spot,
            Duration = buildRecipe.Duration,
        });

        Program.Logger.ZLogDebug(Logs.Recipe, "Build", type, entity);
    }
}


/// <summary>
/// Executes the <see cref="PickupCommand"/> command logic. 
/// </summary>
public struct PickupCommandExecute : IExecute<PickupCommand>
{
    public World World { get; set; }
    public void Execute(ref PickupCommand command)
    {
        // Get pickup popup
        var entity = command.Popup.Resolve(World);
        ref var popup = ref entity.Get<Popup>();
        var triggeredByOption = (Entity)popup.Target;
        var parent = (Entity)triggeredByOption.Get<Child>().Parent;

        // Get item popup
        ref var parentPopup = ref parent.Get<Popup>();
        var owner = (Entity) parentPopup.Owner;
        var target = (Entity) parentPopup.Target;
        
        // Make owner move to item and make it popup
        ref var movement = ref owner.Get<Movement>();
        ref var item = ref target.Get<Item>();
        ref var postion = ref target.Get<NetworkTransform>();
        movement.Target = postion.Pos;
        
        // Make it pickup the item and close popup
        var cb = World.Record();
        cb.Add(in owner, new Pickup{ Target = target, Amount = item.Amount });
        cb.Add(in parent, new Destroy());
    }
}


/// <summary>
///     A system processing <see cref="EntityCommand" /> to handle entity operations.
/// </summary>
public sealed partial class EntityCommandSystem : CommandSystem<EntityCommand, ExecuteEntityCommand>
{
    public EntityCommandSystem(World world) : base(world)
    {
    }
}

/// <summary>
///     A system processing <see cref="ChunkCommand" /> to handle chunk creation, loading and deletion properly
///     Required since those happen async and make use of the .ToECS methods which are still running on the main, resulting in entity creation midframe which breaks the game
/// </summary>
public sealed partial class ChunkCommandSystem : CommandSystem<ChunkCommand, ExecuteChunkCommand>
{
    public ChunkCommandSystem(World world) : base(world)
    {
    }
}

/// <summary>
///     A system processing <see cref="ClickCommand" /> entities to simulate click from the clicked to the clicker.
/// </summary>
public sealed partial class ClickedCommandSystem : CommandSystem<ClickCommand, ExecuteClickedCommand>
{
    public ClickedCommandSystem(World world) : base(world)
    {
    }
}

/// <summary>
///     A system processing <see cref="ClickCommand" /> entities to simulate click from the clicked to the clicker.
/// </summary>
public sealed partial class DoubleClickedCommandSystem : CommandSystem<DoubleClickCommand, ExecuteDoubleClickCommand>
{
    public DoubleClickedCommandSystem(World world) : base(world)
    {
    }
}

/// <summary>
///     A systen which processes a <see cref="TeleportationCommand" /> to teleport a entity to a certain position
/// </summary>
public sealed partial class TeleportationCommandSystem : CommandSystem<TeleportationCommand, ExecuteTeleportCommand>
{
    public TeleportationCommandSystem(World world) : base(world)
    {
    }
}

/// <summary>
///     A system processing <see cref="PopUpCommand" />'s to spawn in popups properly.
/// </summary>
public sealed partial class PopUpCommandSystem : CommandSystem<PopUpCommand, ExecutePopupCommand>
{
    public PopUpCommandSystem(World world) : base(world)
    {
    }
}

/// <summary>
///     A system processing <see cref="InventoryCommand" />-entities to either add, update or remove entities inside an inventory.
/// </summary>
public sealed partial class InventoryCommandSystem : CommandSystem<InventoryCommand, ExecuteInventoryCommand>
{
    public InventoryCommandSystem(World world) : base(world)
    {
    }
}

public sealed class ChatCommandSystem : CommandSystem<ChatMessageCommand, ChatCommandExecute>
{
    public ChatCommandSystem(World world) : base(world)
    {
    }
}

/// <summary>
///     A system processing incoming <see cref="BuildCommand" />'s to initiate a entities build process and wire the building steps together.
/// </summary>
public sealed class BuildCommandSystem : CommandSystem<BuildCommand, BuildCommandExecute>
{
    public BuildCommandSystem(World world) : base(world) { }
}

/// <summary>
///     A system processing incoming <see cref="PickupCommand" />'s to initiate a entities build process and wire the building steps together.
/// </summary>
public sealed class PickupCommandSystem : CommandSystem<PickupCommand, PickupCommandExecute>
{
    public PickupCommandSystem(World world) : base(world)
    {
    }
}