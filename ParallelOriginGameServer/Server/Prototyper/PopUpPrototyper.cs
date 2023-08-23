using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.LowLevel;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server.Prototyper;

/// <summary>
///     An <see cref="Prototyper{I,T}" /> for <see cref="Popup" />'s
/// </summary>
public class PopUpPrototyper : Prototyper<Entity>
{
    // Southern tree popup localisation
    public static readonly Localizations SouthernTreeLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 1 }, { "name", 4 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    // flag localisation
    public static readonly Localizations FlagLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 7 }, { "name", 5 } }.ToHandle(),
        UniqueLocalizations = new Dictionary<string, string>(1).ToHandle()
    };
    
    // Mob popup localisation
    public static readonly Localizations MobLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 1 }, { "name", 9 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    // gold item popup localisation
    public static readonly Localizations GoldItemLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 1 }, { "name", 2 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };

    // wood item localisation
    public static readonly Localizations WoodItemLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 1 }, { "name", 23 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    // Pickup localisation
    public static readonly Localizations PickupLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 12 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };


    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();

        // Southern Tree popup
        Register(1, () => world.Create<Identity,Popup,Mesh,Localizations,Parent>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Popup, Type = Types.SouthernTreePopup },
                new Popup(Types.ChopOption),
                new Mesh { Id = 7, Instantiate = true },
                SouthernTreeLocalisation,
                new Parent(4)
            );
        });

        // Flag popup
        Register(2, () => world.Create<Identity,Popup,Mesh,Localizations,OwnerNameLocalisation,Parent>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Popup, Type = Types.FlagPopup },
                new Popup(Types.VisitOption),
                new Mesh { Id = 7, Instantiate = true },
                FlagLocalisation,
                new Parent(4)
            );
        });

        // Mob popup
        Register(3, () => world.Create<Identity,Popup,Mesh,Localizations,Parent>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Popup, Type = Types.MobPopup },
                new Popup(Types.AttackOption),
                new Mesh { Id = 7, Instantiate = true },
                MobLocalisation,
                new Parent(4)
            );
        });
        
        // Gold Item popup
        Register(4, () => world.Create<Identity,Popup,Mesh,Localizations,Parent>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Popup, Type = Types.GoldGroundItemPopup },
                new Popup(Types.PickupOption, Types.PickupAllOption),
                new Mesh { Id = 7, Instantiate = true },
                GoldItemLocalisation,
                new Parent(4)
            );
        });
        
        // Wood Item popup
        Register(5, () => world.Create<Identity,Popup,Mesh,Localizations,Parent>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Popup, Type = Types.WoodGroundItemPopup },
                new Popup(Types.PickupAllOption),
                new Mesh { Id = 7, Instantiate = true },
                WoodItemLocalisation,
                new Parent(4)
            );
        });
        
        // Pickup amount popup, no options, client sends a PickUpCommand with the amount. 
        Register(6, () => world.Create<Identity,Popup,Mesh,Localizations,Parent>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Popup, Type = Types.PickupPopup },
                new Popup(Types.PickupOption),
                new Mesh { Id = 15, Instantiate = true },
                PickupLocalisation,
                new Parent(4)
            );
        });
    }

    public override void AfterInstanced(short typeId, ref Entity clonedInstance)
    {
        base.AfterInstanced(typeId, ref clonedInstance);
        clonedInstance.Add<Prefab>();
    }
}

/// <summary>
///     An <see cref="Prototyper{I,T}" /> for <see cref="Popup" />'s
/// </summary>
public class OptionPrototyper : Prototyper<Entity>
{
    // GATHER localisation
    public static readonly Localizations GatherLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 3 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    // Visit localisation
    public static readonly Localizations VisitLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 8 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    // Attack localisation
    public static readonly Localizations AttackLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 10 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    // pickup all localisation
    public static readonly Localizations PickupAllLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 19 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    // Pickup localisation
    public static readonly Localizations PickupLocalisation = new() {
        LocalizationsHandle = new Dictionary<string, short> { { "title", 12 } }.ToHandle(),
        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
    };
    
    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();

        // Gather option
        Register(1, () => world.Create<Identity,Option,Mesh,Localizations,OnClickedChop,OnClickedDestroyPopup,Child>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Option, Type = Types.ChopOption },
                new Mesh { Id = 8, Instantiate = true },
                GatherLocalisation
            );
        });

        // Visit option
        Register(2, () => world.Create<Identity,Option,Mesh,Localizations,OnClickedVisit,OnClickedDestroyPopup,Child>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Option, Type = Types.VisitOption },
                new Mesh { Id = 8, Instantiate = true },
                VisitLocalisation
            );
        });

        // Attack option
        Register(3, () => world.Create<Identity,Option,Mesh,Localizations,OnClickedAttack,OnClickedDestroyPopup,Child>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Option, Type = Types.AttackOption },
                new Mesh { Id = 8, Instantiate = true },
                AttackLocalisation
            );
        });
        
        // Pickup all option, handled directly, without extra popup.
        Register(4, () => world.Create<Identity,Option,Mesh,Localizations,OnClickedPickup,OnClickedDestroyPopup,Child>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Option, Type = Types.PickupAllOption },
                new Mesh { Id = 8, Instantiate = true },
                PickupAllLocalisation
            );
        });
        
        // Pickup option, triggers pickup popup
        Register(5, () => world.Create<Identity,Option,Mesh,Localizations,OnClickedSpawnPopUp,Child>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Option, Type = Types.PickupOption },
                new Mesh { Id = 8, Instantiate = true },
                PickupLocalisation,
                new OnClickedSpawnPopUp{ Type = Types.PickupPopup }
            );
        });
    }

    public override void AfterInstanced(short typeId, ref Entity clonedInstance)
    {
        base.AfterInstanced(typeId, ref clonedInstance);
        clonedInstance.Add<Prefab>();
    }
}