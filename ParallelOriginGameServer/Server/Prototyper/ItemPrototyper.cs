using System;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;

namespace ParallelOriginGameServer.Server.Prototyper;

/// <summary>
///     An <see cref="Prototyper{I,T}" /> for <see cref="Item" />'s
/// </summary>
public class ItemPrototyper : Prototyper<Entity>
{

    public override void Initialize()
    {
        // ! Item names & localisations are handled client side currently !
        var world = ServiceLocator.Get<World>();

        // Gold item
        Register(1, () => world.Create<Identity, Item, Mesh, Sprite, InInventory>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Item, Type = Types.Gold },
                new Item { Amount = 1, Stackable = true },
                new Mesh { Id = 10, Instantiate = false },
                new Sprite { Id = 1 },
                new InInventory()
            );
        });

        // Wood item
        Register(2, () => world.Create<Identity, Item, Mesh, Sprite, InInventory>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Item, Type = Types.Wood },
                new Item { Amount = 1, Stackable = true },
                new Mesh { Id = 10, Instantiate = false },
                new Sprite { Id = 4 },
                new InInventory()
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
///     An <see cref="Prototyper{I,T}" /> for <see cref="Item" />'s
/// </summary>
public class ItemOnGroundPrototyper : Prototyper<Entity>
{
    public override void Initialize()
    {
        // ! Item names & localisations are handled client side currently !
        var world = ServiceLocator.Get<World>();

        // Gold item
        Register(1, () => world.Create<Identity, Item, OnGround, Mesh, Sprite, OnClickedSpawnPopUp, NetworkTransform, BoxCollider>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Item, Type = Types.GoldGround },
                new Item { Amount = 1, Stackable = true },
                new OnGround{ PickupType = Types.Gold},
                new Mesh { Id = 23, Instantiate = true },
                new Sprite { Id = 1 },
                new OnClickedSpawnPopUp { Type = Types.GoldGroundItemPopup },
                new BoxCollider { Width = 0.001f, Height = 0.001f }
            );
        });

        // Wood item
        Register(2, () => world.Create<Identity, Item, OnGround, Mesh, Sprite, OnClickedSpawnPopUp, NetworkTransform, BoxCollider>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Item, Type = Types.WoodGround },
                new Item { Amount = 1, Stackable = true },
                new OnGround{ PickupType = Types.Wood},
                new Mesh { Id = 23, Instantiate = true },
                new Sprite { Id = 4 },
                new OnClickedSpawnPopUp { Type = Types.WoodGroundItemPopup },
                new BoxCollider { Width = 0.001f, Height = 0.001f }
            );
        });
    }

    public override void AfterInstanced(short typeId, ref Entity clonedInstance)
    {
        base.AfterInstanced(typeId, ref clonedInstance);
        clonedInstance.Add<Prefab>();
    }
}