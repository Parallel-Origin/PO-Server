using System;
using System.Linq;
using System.Runtime.InteropServices;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using Arch.System.SourceGenerator;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.ThirdParty;
using ZLogger;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which controlls all systems which process UI stuff and interactions. 
/// </summary>
public sealed class InteractionGroup : Group<float>
{
    public InteractionGroup(World world) : base(
        // UI
        new OnClickPopUpSystem(world),
        new OnClickUserActivitySystem(world),
        new OwnerNameLocalisationSystem(world)
    )
    {
    }
}

/// <summary>
///     A system processing <see cref="Clicked" /> entities to spawn popups for them.
/// </summary>
public sealed partial class OnClickPopUpSystem : BaseSystem<World,float>
{
    public OnClickPopUpSystem(World world) : base(world)
    {
    }
    
    [Query]
    [None<Prefab>]
    private void OnClickSpawnPopup(in Entity entity, ref Toggle<Clicked> clicked, ref OnClickedSpawnPopUp spawnPopUp)
    {
        if (!clicked.Enabled) return;
        
        // Create command/event to create an popup
        var owner = clicked.Component.Clickers.Get().First();
        var target = entity;
        PopUpCommandSystem.Add(new PopUpCommand(spawnPopUp.Type, owner, target));
    }
    
    [Query]
    [All<Option, OnClickedDestroyPopup>, None<Prefab>]
    private void OnClickDestroyPopup(ref Toggle<Clicked> clicked, ref Child child)
    {
        if (!clicked.Enabled) return;
        
        // Make the whole popup destroy after chopped was clicked
        var record = World.Record();
        record.Add((Entity)child.Parent, new Destroy());
    }
}

/// <summary>
///     A system iterating ober <see cref="Option" />'s which were <see cref="Clicked" /> and <see cref="OnClickedChop" /> to make the clicker go and chop down the clicked entity.
/// </summary>
public sealed partial class OnClickUserActivitySystem : BaseSystem<World,float>
{
    
    public OnClickUserActivitySystem(World world) : base(world)
    {
    }
    
    [Query]
    [All<Option,OnClickedChop>, None<Prefab>]
    private void OnClickChop(ref Child child, ref Toggle<Clicked> clicked)
    {
        if (!clicked.Enabled) return;
        
        // Get popup
        var popupEntity = (Entity)child.Parent.Entity;

        // Prevent exception
        if (!popupEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Chop", LogStatus.EntityNotAlive, popupEntity);
            return;
        }

        // Get clicker and the clicked entity
        ref var popup = ref popupEntity.Get<Popup>();
        var clicker = clicked.Component.Clickers.Get().First();
        var clickedEntity = (Entity) popup.Target.Entity;

        // Prevent exception
        if (!clickedEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Chop", LogStatus.EntityNotAlive, clickedEntity);
            return;
        }

        // Get movement of clicker and the pos of the clicked entity
        ref var transform = ref clickedEntity.Get<NetworkTransform>();
        ref var movement = ref clicker.Get<Movement>();

        // Make the clicker go chop the clicked entity next frame
        var record = World.Record();
        record.Add(clicker, new Chop { Target = clickedEntity });
        record.Set(clicker,movement with { Target = transform.Pos });

        Program.Logger.ZLogDebug(Logs.Action, "Clicked on chop", LogStatus.Sucessfull, clicker, clickedEntity);
    }
    
    [Query]
    [All<Option,OnClickedVisit>, None<Prefab>]
    private void OnClickVisit(ref Child child, ref Toggle<Clicked> clicked)
    {
        if (!clicked.Enabled) return;
        
        // Get popup
        var popupEntity = (Entity)child.Parent.Entity;

        // Prevent exception
        if (!popupEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Visit", LogStatus.EntityNotAlive, popupEntity);
            return;
        }

        // Get clicker and the clicked entity
        ref var popup = ref popupEntity.Get<Popup>();
        var clicker = clicked.Component.Clickers.Get().First();
        var clickedEntity = (Entity) popup.Target.Entity;

        // Prevent exception
        if (!clickedEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Visit", LogStatus.EntityNotAlive, clickedEntity);
            return;
        }

        // Get clicked and record a teleportation command
        ref var clickerIdentity = ref clicker.Get<Identity>();
        ref var clickedTransform = ref clickedEntity.Get<NetworkTransform>();
        TeleportationCommandSystem.Add(new TeleportationCommand{ Entity = new EntityLink(clicker, clickerIdentity.Id), Position = clickedTransform.Pos});

        Program.Logger.ZLogInformation(Logs.SingleAction, "Clicked on Visit", clickedTransform.Pos, clicker);
    }
    
    [Query]
    [All<Option,OnClickedAttack>, None<Prefab>]
    private void OnClickAttack(ref Child child, ref Clicked clicked)
    {
        // Get popup
        var popupEntity = (Entity)child.Parent.Entity;
        
        // Prevent exception
        if (!popupEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Attack", LogStatus.EntityNotAlive, popupEntity);
            return;
        }

        // Get clicker and the clicked entity
        ref var popup = ref popupEntity.Get<Popup>();
        var clicker = clicked.Clickers.Get().First();
        var clickedEntity = (Entity) popup.Target.Entity;

        // Prevent exception
        if (!clickedEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Attack", LogStatus.EntityNotAlive, clickedEntity);
            return;
        }

        // Make clicker attack the clicked entity and move towards  
        ref var position = ref clickedEntity.Get<NetworkTransform>();
        ref var movement = ref clicker.Get<Movement>();
        ref var attacks = ref clicker.Get<InCombat>();

        movement.Target = position.Pos;
        attacks.Entities.Get().Add(clickedEntity);
        attacks.Intervall = 100; // Instantly trigger attack upon click

        Program.Logger.ZLogInformation(Logs.SingleAction, "Clicked on Attack", clickedEntity, clicker);
    }
    
    [Query]
    [All<Option,OnClickedPickup>, None<Prefab>]
    private void OnClickPickup(ref Child child, ref Clicked clicked)
    {
        // Get popup
        var popupEntity = (Entity) child.Parent.Entity;

        // Prevent exception
        if (!popupEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Pickup", LogStatus.EntityNotAlive, popupEntity);
            return;
        }

        // Get clicker and the clicked entity
        ref var popup = ref popupEntity.Get<Popup>();
        var clicker = clicked.Clickers.Get().First();
        var clickedEntity = (Entity) popup.Target.Entity;

        // Prevent exception
        if (!clickedEntity.IsAlive())
        {
            Program.Logger.ZLogError(Logs.SingleAction, "Click on Pickup", LogStatus.EntityNotAlive, clickedEntity);
            return;
        }

        // Move clicker towards clicked item  
        ref var position = ref clickedEntity.Get<NetworkTransform>();
        ref var item = ref clickedEntity.Get<Item>();
        ref var movement = ref clicker.Get<Movement>();
        movement.Target = position.Pos;
        
        // Make it pickup the item
        var record = World.Record();
        record.Add(clicker,new Pickup{ Target = clickedEntity, Amount = item.Amount });

        Program.Logger.ZLogInformation(Logs.SingleAction, "Clicked on Pickup", clickedEntity, clicker);
    }
}

/// <summary>
///     A system which adds the targets owner name of a popup to it once after being created ( For example the owner name of a flag )
/// </summary>
public sealed partial class OwnerNameLocalisationSystem : BaseSystem<World,float>
{
    
    public OwnerNameLocalisationSystem(World world) : base(world)
    {
    }
    
    [Query]
    [All<Created,OwnerNameLocalisation>, None<Prefab>]
    private void OnCreateLocalise(ref Popup popup, ref Localizations localizations)
    {
        // Get the name of the targets owner
        ref var target = ref popup.Target;
        ref var structure = ref target.Entity.Entity.Get<Structure>();
        var owner = (Entity) structure.Owner;
        ref var character = ref owner.Get<Character>();

        localizations.UniqueLocalizations.Get()["owner"] = character.Name;
    }
}