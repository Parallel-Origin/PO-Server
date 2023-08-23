using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.LowLevel;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Behaviour;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.ThirdParty;
using Range = ParallelOrigin.Core.ECS.Components.Combat.Range;

namespace ParallelOriginGameServer.Server.Prototyper;

/// <summary>
///     An <see cref="Prototyper{I,T}" /> for <see cref="Mob" />'s
/// </summary>
public class MobPrototyper : Prototyper<Entity>
{
    // Drop/Weight-Tables to reduce memory useage
    private static readonly Handle<WeightTable<WeightedItem>> WolveDroptable = new WeightTable<WeightedItem>(
        new WeightedItem { Amount = 1, Type = Types.GoldGround, Weight = 0 }
    ).ToHandle();

    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();
        var archetype = new ComponentType[]
        {
            typeof(Identity),
            typeof(Mob),
            typeof(Mesh),
            typeof(NetworkTransform),
            typeof(NetworkRotation),
            typeof(Movement),
            typeof(Health),
            typeof(Range),
            typeof(AttackSpeed),
            typeof(PhysicalDamage),
            typeof(InCombat),
            typeof(BoxCollider),
            typeof(Animation),
            typeof(AiController),
            typeof(Drops),
            typeof(OnClickedSpawnPopUp),
            typeof(Saveable),
            typeof(Updateable)
        };
        
        // Wolve
        Register(1, () => world.Create(archetype), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Mob, Type = Types.Wolve },
                new Mesh { Id = 14, Instantiate = true },
                new Movement { Speed = 0.003f, Target = Vector2d.Zero },
                new Health { CurrentHealth = 100.0f, MaxHealth = 100.0f },
                new Range { Value = new Stat<float>(0.0125f) },
                new AttackSpeed { Value = new Stat<float>(2.5f) },
                new PhysicalDamage { Value = new Stat<float>(7.5f) },
                new InCombat { Entities = new HashSet<Entity>(1).ToHandle() },
                new BoxCollider { Width = 0.001f, Height = 0.001f },
                new Animation(1, AnimationBtBuilder.NormalAnimationBt(entity)),
                new AiController { BehaviourTree = AibtBuilder.NormalAibt(entity) },
                new Drops{ DropsHandle = WolveDroptable},
                new OnClickedSpawnPopUp { Type = Types.MobPopup }
            );
        });
    }

    public override void AfterInstanced(short typeId, ref Entity clonedInstance)
    {
        base.AfterInstanced(typeId, ref clonedInstance);
        clonedInstance.Add<Prefab>();
    }
}