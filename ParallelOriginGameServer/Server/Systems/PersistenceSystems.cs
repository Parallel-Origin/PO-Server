using System;
using System.Collections.Generic;
using System.Diagnostics;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Persistence;
using ParallelOriginGameServer.Server.Prototyper;
using ParallelOriginGameServer.Server.ThirdParty;
using ZLogger;
using Character = ParallelOrigin.Core.ECS.Components.Character;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;
using Identity = ParallelOrigin.Core.ECS.Components.Identity;
using Item = ParallelOrigin.Core.ECS.Components.Item;
using Mob = ParallelOriginGameServer.Server.Persistence.Mob;
using Resource = ParallelOrigin.Core.ECS.Components.Resource;
using Structure = ParallelOrigin.Core.ECS.Components.Structure;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group that controlls all systems which communicate with the database.
/// </summary>
public sealed class DatabaseGroup : Group<float>
{
    public DatabaseGroup(World world, GameDbContext gameDbContext) : base(
        
        // Create models
        new ModelSystem(world, gameDbContext),

        // Destroy to delete
        new DeleteAndDetachModelSystem(world, gameDbContext),

        // Regular saving intervall 
        new IntervallGroup(60.0f,
            // Update
            new ModelUpdateSystem(world, gameDbContext),
            new PersistenceSystem(world, gameDbContext)
        )
    )
    {
    }
}

/// <summary>
///     The <see cref="PersistenceSystem"/> class
///     saves the <see cref="GameDbContext" /> once being called.
/// </summary>
public class PersistenceSystem : BaseSystem<World,float>
{
    // Stopwatch used for time measurement of database operations
    private static readonly Stopwatch Sw = new();

    public PersistenceSystem(World world, GameDbContext context) : base(world)
    {
        Context = context;
    }

    public GameDbContext Context { get; set; }

    public override void Update(in float state)
    {
        try
        {
            Sw.Reset();
            Sw.Start();
            Context.SaveChanges();
            Sw.Stop();

            Program.Logger.ZLogInformation(Logs.Gamestate, $"Persisted in {Sw.ElapsedMilliseconds}");
        }
        catch (DbUpdateException e)
        {
            Program.Logger.ZLogError(Logs.Gamestate, "Not persisted");

            Program.Logger.ZLogError("{0}", e.Message);
            Program.Logger.ZLogError("{0}", e.InnerException?.Message);
            Program.Logger.ZLogError("{0}", e.InnerException?.InnerException?.Message);
            Program.Logger.ZLogError("{0}", e.InnerException?.InnerException?.InnerException?.Message);
            Program.Logger.ZLogError("{0}", e.StackTrace);

            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                foreach (var eve in e.Entries)
                    Program.Logger.ZLogError($"Entity of type {eve.Entity.GetType().Name} in state {eve.State} could not be updated : {JsonConvert.SerializeObject(eve.Entity, settings)}");
            }
            catch (Exception json)
            {
                Program.Logger.ZLogError(json.Message);
            }

            throw new Exception("Database couldnt update, time to crash");
        }
    }
}

/// <summary>
///     The <see cref="ModelSystem"/> class
///     ensures that all game-entities without a <see cref="Model"/> component get one and are therefore stored in the database during the next save.
/// </summary>
public sealed partial class ModelSystem : BaseSystem<World,float>
{
    private readonly GameDbContext _context;
    
    public ModelSystem(World world, GameDbContext context) : base(world)
    {
        this._context = context;
    }

    public override void BeforeUpdate(in float state)
    {
        base.BeforeUpdate(state);

        _context.ChangeTracker.AutoDetectChangesEnabled = false; // Disable for adding stuff to context without triggering DetectChanges every time an item was addded. 
    }

    [Query]
    [None(typeof(Model), typeof(Prefab))]
    private void CreateChunkModel(in Entity en, ref Identity identity, ref Chunk ch)
    {
        // Create dto
        var identityDto = new Persistence.Identity { Id = identity.Id, Tag = identity.Tag, Type = identity.Type };
        var chunkDto = new Persistence.Chunk
        {
            Identity = identityDto,
            X = ch.Grid.X,
            Y = ch.Grid.Y,
            CreatedOn = ch.CreatedOn,
            RefreshedOn = ch.CreatedOn,
            ContainedCharacters = new HashSet<Persistence.Character>(),
            ContainedResources = new HashSet<Persistence.Resource>(),
            ContainedStructures = new HashSet<Persistence.Structure>(),
            ContainedItems = new HashSet<Persistence.Item>(),
            ContainedMobs = new HashSet<Mob>()
        };

        // Assign dto next frame. 
        var model = new Model { ModelDto = chunkDto };
        en.Add(model);

        _context.Identities.Add(identityDto);
        _context.Chunks.Add(chunkDto);
    }
    
    [Query]
    [All(typeof(Resource)), None(typeof(Model), typeof(Prefab))]
    private void CreateResourceModel(in Entity en, ref Identity identity, ref NetworkTransform transform, ref NetworkRotation rot)
    {
        // Chunk
        var chunkEntity = World.GetChunk(transform.Chunk);
        ref var chunkModel = ref chunkEntity.Get<Model>();
        var chunk = chunkModel.ModelDto as Persistence.Chunk;

        // Create dto
        var identityDto = new Persistence.Identity { Id = identity.Id, Tag = identity.Tag, Type = identity.Type };
        var transformDto = new Transform { X = (float)transform.Pos.X, Y = (float)transform.Pos.Y, RotX = rot.Value.X, RotY = rot.Value.Y, RotZ = rot.Value.Z, RotW = rot.Value.W };
        var resourceDto = new Persistence.Resource
        {
            Identity = identityDto,
            Transform = transformDto,
            Chunk = chunk,
            HarvestedOn = DateTime.UnixEpoch
        };

        // Assign dto next frame. 
        var model = new Model { ModelDto = resourceDto };
        en.Add(model);

        _context.Identities.Add(identityDto);
        _context.Resources.Add(resourceDto);
    }
    
    [Query]
    [None(typeof(Model), typeof(Prefab))]
    private void CreateStructureModel(in Entity en, ref Identity identity, ref Structure structure, ref NetworkTransform transform, ref NetworkRotation rot, ref Health health)
    {
        // Chunk
        var chunkEntity = World.GetChunk(transform.Chunk);
        ref var chunkModel = ref chunkEntity.Get<Model>();
        var chunk = chunkModel.ModelDto as Persistence.Chunk;

        // Owner
        var owner = structure.Owner.Resolve(World);
        ref var ownerModel = ref owner.Get<Model>();

        // Create dto
        var identityDto = new Persistence.Identity { Id = identity.Id, Tag = identity.Tag, Type = identity.Type };
        var transformDto = new Transform { X = (float)transform.Pos.X, Y = (float)transform.Pos.Y, RotX = rot.Value.X, RotY = rot.Value.Y, RotZ = rot.Value.Z, RotW = rot.Value.W };
        var structureDto = new Persistence.Structure
        {
            Identity = identityDto,
            Transform = transformDto,
            Chunk = chunk,
            Character = ((Account)ownerModel.ModelDto).Character,
            Health = health.CurrentHealth
        };

        // Assign dto next frame. 
        var model = new Model { ModelDto = structureDto };
        en.Add(model);

        // Add it to the context
        _context.Identities.Add(identityDto);
        _context.Structures.Add(structureDto);
    }
    
    [Query]
    [None(typeof(Model), typeof(Prefab))]
    private void CreateItemModel(in Entity en, ref Identity identity, ref Item item, ref InInventory inInventory)
    {
        // Character model
        ref var charModel = ref inInventory.Inventory.Get<Model>();
        var character = (Account)charModel.ModelDto;

        // Create dto
        var identityDto = new Persistence.Identity { Id = identity.Id, Tag = identity.Tag, Type = identity.Type };
        var itemDto = new InventoryItem
        {
            Identity = identityDto,
            Character = character.Character,
            Amount = item.Amount,
            Level = 1
        };

        // Assign dto next frame. 
        var model = new Model { ModelDto = itemDto };
        en.Add(model);

        // Add it to the chunks context 
        _context.Identities.Add(identityDto);
        _context.InventoryItems.Add(itemDto);
    }

    [Query]
    [All(typeof(ParallelOrigin.Core.ECS.Components.Mob)), None(typeof(Model), typeof(Prefab))]
    private void CreateMobModel(in Entity en, ref Identity identity, ref NetworkTransform transform, ref NetworkRotation rot, ref Health health)
    {
        // Chunk
        var chunkEntity = World.GetChunk(transform.Chunk);
        ref var chunkModel = ref chunkEntity.Get<Model>();
        var chunk = chunkModel.ModelDto as Persistence.Chunk;

        // Create dto
        var identityDto = new Persistence.Identity { Id = identity.Id, Tag = identity.Tag, Type = identity.Type };
        var transformDto = new Transform { X = (float)transform.Pos.X, Y = (float)transform.Pos.Y, RotX = rot.Value.X, RotY = rot.Value.Y, RotZ = rot.Value.Z, RotW = rot.Value.W };
        var mobDto = new Mob
        {
            Identity = identityDto,
            Transform = transformDto,
            Chunk = chunk,
            Health = health.CurrentHealth
        };

        // Assign dto next frame. 
        var model = new Model { ModelDto = mobDto };
        en.Add(model);

        // Add it to the context
        _context.Identities.Add(identityDto);
        _context.Mobs.Add(mobDto);
    }

    public override void AfterUpdate(in float state)
    {
        base.AfterUpdate(state);

        _context.ChangeTracker.AutoDetectChangesEnabled = true;
    }
}



/// <summary>
///     The <see cref="ModelUpdateSystem"/> class
///     updates the <see cref="Model"/> components by transferring the current state of the respective entities into them.
///     Thus the database entities are kept up to date at the next save.
/// </summary>
public sealed partial class ModelUpdateSystem : BaseSystem<World,float>
{
    private readonly GameDbContext _context;

    public ModelUpdateSystem(World world, GameDbContext context) : base(world)
    {
        this._context = context;
    }
    
    public override void BeforeUpdate(in float state)
    {
        base.BeforeUpdate(state);

        _context.ChangeTracker.AutoDetectChangesEnabled = false; // Disable for adding stuff to context without triggering DetectChanges every time an item was addded. 
    }

    [Query]
    [None(typeof(Prefab))]
    private void UpdateChunk(in Entity en, ref Chunk ch, ref Model model)
    {
        // Update chunk attributes
        var chunkDto = (Persistence.Chunk)model.ModelDto;
        chunkDto.CreatedOn = ch.CreatedOn;

        // Update chunk childs TODO : Find a cleaner way with less garbage :)
        var contains = ch.Contains.Get();
        chunkDto.ContainedCharacters.Clear();
        foreach (var entity in contains)
        {
            if (!entity.Has<Model>()) continue;

            ref var identity = ref entity.Get<Identity>();
            ref var entityModel = ref entity.Get<Model>();

            if (!identity.Tag.Equals(Tags.Character)) continue;
            
            // If model was marked as deleted previously, make it track again to prevent weird behaviour and errors
            var acc = ((Account)entityModel.ModelDto).Character;
            chunkDto.ContainedCharacters.Add(acc);
        }

        chunkDto.ContainedResources.Clear();
        foreach (var entity in contains)
        {
            if (!entity.Has<Model>()) continue;

            ref var identity = ref entity.Get<Identity>();
            ref var entityModel = ref entity.Get<Model>();

            if (!identity.Tag.Equals(Tags.Resource)) continue;

            // If model was marked as deleted previously, make it track again to prevent weird behaviour and errors
            var res = (Persistence.Resource)entityModel.ModelDto;
            chunkDto.ContainedResources.Add(res);
        }

        // Put structures into the chunk for database
        chunkDto.ContainedStructures.Clear();
        foreach (var entity in contains)
        {
            if (!entity.Has<Model>()) continue;

            ref var identity = ref entity.Get<Identity>();
            ref var entityModel = ref entity.Get<Model>();

            if (!identity.Tag.Equals(Tags.Structure)) continue;
            
            // If model was marked as deleted previously, make it track again to prevent weird behaviour and errors
            var structure = (Persistence.Structure)entityModel.ModelDto;
            chunkDto.ContainedStructures.Add(structure);
        }

        // Put mobs into the chunk for database
        chunkDto.ContainedMobs.Clear();
        foreach (var entity in contains)
        {
            if (!entity.Has<Model>()) continue;

            ref var identity = ref entity.Get<Identity>();
            ref var entityModel = ref entity.Get<Model>();

            if (!identity.Tag.Equals(Tags.Mob)) continue;
            
            // If model was marked as deleted previously, make it track again to prevent weird behaviour and errors
            var mob = (Mob)entityModel.ModelDto;
            chunkDto.ContainedMobs.Add(mob);
        }

        Program.Logger.ZLogInformation("[Gamestate] | [Chunk model updated] involved {0}/{1}/{2}", en, ch.Grid.X, ch.Grid.Y);
    }
    
    [Query]
    [All(typeof(LogedIn)), None(typeof(Prefab))]
    private void UpdateCharacter(ref Character character, ref Inventory inventory, ref NetworkTransform transform, ref Model model)
    {
        // Update chunk if player is in one
        Persistence.Chunk chunk = null;
        if (transform.Chunk != Grid.Zero)
        {
            var chunkEntity = World.GetChunk(transform.Chunk);
            ref var chunkModel = ref chunkEntity.Get<Model>();
            chunk = chunkModel.ModelDto as Persistence.Chunk;
        }

        // Account
        var account = (Account)model.ModelDto;
        account.Character.Transform.X = (float)transform.Pos.X;
        account.Character.Transform.Y = (float)transform.Pos.Y;
        account.Character.Chunk = chunk;

        // Clear inventory and fill it again with all its items 
        account.Character.Inventory.Clear();
        foreach (var entityReference in inventory.Items)
        {
            var entity = (Entity)entityReference;
            ref var entityModel = ref entity.Get<Model>();
            var itemModel = (InventoryItem)entityModel.ModelDto;
            account.Character.Inventory.Add(itemModel);
        }

        Program.Logger.ZLogInformation(Logs.SingleAction, "Gamestate", "Character model Updated", character.Name);
    }
    
    [Query]
    [All(typeof(Structure)), None(typeof(Prefab))]
    private void UpdateStructure(ref Health health, ref Model model)
    {
        var structureDto = (Persistence.Structure)model.ModelDto;
        structureDto.Health = health.CurrentHealth;
    }
    
    [Query]
    [All(typeof(ParallelOrigin.Core.ECS.Components.Mob)), None(typeof(Prefab), typeof(Dead), typeof(Delete))]
    private void UpateMob(ref NetworkTransform transform, ref NetworkRotation rot, ref Health health, ref Model model)
    {
        // Check if chunk is about to be destroyed, if yes its already detached once this system runs and we cant update this entity chunk references anymore !
        var chunkEntity = World.GetChunk(transform.Chunk);
        if (chunkEntity.Has<Destroy>() || !chunkEntity.Has<Chunk>()) return;

        // Modify entity and make sure transform is existing
        var mobDto = (Mob)model.ModelDto;

        // Make sure a invalid transform gets reassigned and fixed -> fixes random crashes
        var transformEntry = _context.Entry(mobDto.Transform);
        if(transformEntry.State == EntityState.Deleted) transformEntry.State = EntityState.Modified;
        
        // If new chunk couldnt be found, stay in old one to avoid errors
        ref var chunkModel = ref chunkEntity.Get<Model>();
        var chunk = chunkModel.ModelDto as Persistence.Chunk;

        mobDto.Transform.X = (float)transform.Pos.X;
        mobDto.Transform.Y = (float)transform.Pos.Y;
        mobDto.Transform.RotX = rot.Value.X;
        mobDto.Transform.RotY = rot.Value.Y;
        mobDto.Transform.RotZ = rot.Value.Z;
        mobDto.Transform.RotW = rot.Value.W;
        mobDto.Health = health.CurrentHealth;
        mobDto.Chunk = chunk;
    }
    
    [Query]
    [None(typeof(Prefab))]
    private void UpdateItem(ref Item item, ref Model model)
    {
        var itemDto = (InventoryItem)model.ModelDto;
        itemDto.Amount = item.Amount;
    }

    public override void AfterUpdate(in float state)
    {
        base.AfterUpdate(state);

        _context.ChangeTracker.AutoDetectChangesEnabled = true;
    }
}

/// <summary>
///     The <see cref="DeleteAndDetachModelSystem"/> class
///     ensures that entities are appropriately removed from the database with <see cref="Destroy"/> or <see cref="Delete"/> during the next save.
///     At least those that should also be removed from it. 
/// </summary>
public sealed partial class DeleteAndDetachModelSystem : BaseSystem<World,float>
{
    private readonly GameDbContext _context;

    public DeleteAndDetachModelSystem(World world, GameDbContext context) : base(world)
    {
        this._context = context;
    }
    
    [Query]
    [All(typeof(Chunk), typeof(Destroy)), None(typeof(Prefab))]
    private void DetachChunk(in Entity en, ref Model model)
    {
        // Detach chunk
        var chunkModel = (Persistence.Chunk)model.ModelDto;

        var chunkEntry = _context.Entry(chunkModel);
        var chunkIdentityEntry = _context.Entry(chunkModel.Identity);

        chunkEntry.State = EntityState.Detached;
        chunkIdentityEntry.State = EntityState.Detached;

        // Detach resources
        foreach (var chunkResourceModel in chunkModel.ContainedResources)
        {
            var resourceEntry = _context.Entry(chunkResourceModel);
            var identityEntry = _context.Entry(chunkResourceModel.Identity);

            resourceEntry.State = EntityState.Detached;
            identityEntry.State = EntityState.Detached;
        }

        // Detach structures
        foreach (var chunkStructureModel in chunkModel.ContainedStructures)
        {
            var resourceEntry = _context.Entry(chunkStructureModel);
            var identityEntry = _context.Entry(chunkStructureModel.Identity);

            resourceEntry.State = EntityState.Detached;
            identityEntry.State = EntityState.Detached;
        }

        // Detach mobs
        foreach (var chunkMobModel in chunkModel.ContainedMobs)
        {
            var mobEntry = _context.Entry(chunkMobModel);
            var identityEntry = _context.Entry(chunkMobModel.Identity);

            mobEntry.State = EntityState.Detached;
            identityEntry.State = EntityState.Detached;
        }

        Program.Logger.ZLogInformation("[Gamestate] | [Detached chunk] involved {0}/{1}/{2}", en, chunkModel.X, chunkModel.Y);
    }
    
    [Query]
    [All(typeof(Item), typeof(InInventory), typeof(Destroy)), None(typeof(Prefab))]
    private void DeleteItem(in Entity en, ref Model model)
    {
        var itemModel = (InventoryItem)model.ModelDto;
        _context.InventoryItems.Remove(itemModel);
        _context.Identities.Remove(itemModel.Identity);

        Program.Logger.ZLogInformation("[Gamestate] | [Deleted Item] involved {0}", en);
    }
    
    [Query]
    [All(typeof(Resource), typeof(Destroy)), None(typeof(Prefab))]
    private void DeleteResource(in Entity en, ref Health health, ref Model model)
    {
        // Prevent deleting resources with JUST the destroy component... otherwhise it will also delete resources when deloading chunks and resources 
        if (health.CurrentHealth > 0) return;

        var resourceModel = (Persistence.Resource)model.ModelDto;
        _context.Resources.Remove(resourceModel);
        _context.Identities.Remove(resourceModel.Identity);

        Program.Logger.ZLogInformation("[Gamestate] | [Deleted resource] involved {0}", en);
    }
    
    [Query]
    [All(typeof(ParallelOrigin.Core.ECS.Components.Mob), typeof(Delete)), None(typeof(Prefab))]
    private void DeleteMob(in Entity en, ref Health health, ref Model model)
    {
        // Prevent deleting mobs with JUST the destroy component... otherwhise it will also delete resources when deloading chunks and resources 
        if (health.CurrentHealth > 0) return;
        
        var modelModel = (Mob)model.ModelDto;
        _context.Mobs.Remove(modelModel);
        _context.Identities.Remove(modelModel.Identity);

        Program.Logger.ZLogInformation("[Gamestate] | [Deleted mob] involved {0}", en);
    }
}