
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.LowLevel;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server.Prototyper;

/// <summary>
///     An <see cref="Prototyper{I,T}" /> for <see cref="Resource" />'s
/// </summary>
public class ResourcePrototyper : Prototyper<Entity>
{
    // Drop/Weight-Tables to reduce memory useage
    private static readonly Handle<WeightTable<WeightedItem>> SouthernTreeChopDroptable = new WeightTable<WeightedItem>(
        new WeightedItem { Amount = 1, Type = Types.Wood, Weight = -1 },
        new WeightedItem { Amount = 1, Type = Types.Wood, Weight = 50.0f }
    ).ToHandle();

    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();
        
        var gatherableArchetype = new ComponentType[]
        {
            typeof(Identity),
            typeof(Resource),
            typeof(Mesh),
            typeof(NetworkTransform),
            typeof(NetworkRotation),
            typeof(Health),
            typeof(BoxCollider),
            typeof(Loot),
            typeof(OnClickedSpawnPopUp),
            typeof(Saveable),
            typeof(Updateable)
        };
        
        var decorativeArchetype = new ComponentType[]
        {
            typeof(Identity),
            typeof(Resource),
            typeof(Mesh),
            typeof(NetworkTransform),
            typeof(NetworkRotation),
            typeof(Health),
            typeof(BoxCollider),
            typeof(Saveable),
            typeof(Updateable)
        };
        
        // Southern tree 
        Register(1, () => world.Create(gatherableArchetype), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Resource, Type = Types.SouthernTree },
                new Mesh { Id = 1, Instantiate = true },
                new Health { CurrentHealth = 100.0f, MaxHealth = 100.0f },
                new BoxCollider { Width = 0.001f, Height = 0.001f },
                new Loot { LootHandle = SouthernTreeChopDroptable },
                new OnClickedSpawnPopUp { Type = Types.SouthernTreePopup }
            );
        });

        // Stone pile
        Register(2, () => world.Create(decorativeArchetype), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Resource, Type = Types.StonePile },
                new Mesh { Id = 20, Instantiate = true },
                new Health { CurrentHealth = 100.0f, MaxHealth = 100.0f },
                new BoxCollider { Width = 0.001f, Height = 0.001f }
            );
        });
        
        // bush
        Register(3, () => world.Create(decorativeArchetype), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Resource, Type = Types.Bush },
                new Mesh { Id = 21, Instantiate = true },
                new Health { CurrentHealth = 100.0f, MaxHealth = 100.0f },
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