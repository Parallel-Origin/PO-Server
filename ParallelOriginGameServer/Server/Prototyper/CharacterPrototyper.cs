using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using FluentBehaviourTree;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Behaviour;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.ThirdParty;
using Range = ParallelOrigin.Core.ECS.Components.Combat.Range;

namespace ParallelOriginGameServer.Server.Prototyper;

/// <summary>
///     A <see cref="Prototyper{I,T}" /> for a character entity controlled by the player.
/// </summary>
public class CharacterPrototyper : Prototyper<Entity>
{
    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();
        var archetype = new ComponentType[]
        {
            typeof(Identity),
            typeof(Character),
            typeof(Mesh),
            typeof(ChunkLoader),
            typeof(NetworkTransform),
            typeof(NetworkRotation),
            typeof(Movement),
            typeof(Health),
            typeof(Range),
            typeof(AttackSpeed),
            typeof(PhysicalDamage),
            typeof(InCombat),
            typeof(Inventory),
            typeof(Equipment),
            typeof(Aoi),
            typeof(Animation),
            typeof(BuildRecipes),
            typeof(BoxCollider),
            typeof(Rigidbody),
            typeof(Saveable),
            typeof(Updateable),
            typeof(Model),
            typeof(Inactive)
        };
        
        // The default player
        Register(1, () => world.Create(archetype), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Tag = Tags.Character, Type = Types.DefaultCharacter },
                new Mesh { Id = 6, Instantiate = true },
                new Movement { Speed = 0.004f, Target = Vector2d.Zero },
                new Health { CurrentHealth = 100.0f, MaxHealth = 100.0f },
                new Range { Value = new Stat<float>(0.0125f) },
                new AttackSpeed { Value = new Stat<float>(3.0f) },
                new PhysicalDamage { Value = new Stat<float>(12.5f) },
                new InCombat(1),
                new Inventory(8),
                new Equipment{ Weapon = EntityLink.Null },
                new Aoi(0.1f),
                new Animation(1, AnimationBtBuilder.NormalAnimationBt(entity)),
                new BuildRecipes { Recipes = new[] { Types.FlagRecipe } },
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