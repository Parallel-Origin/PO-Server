
using Arch.Core;
using Arch.Core.Extensions;
using FluentBehaviourTree;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Prototyper;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server.Behaviour;

/// <summary>
///     A class which stores several methods for building BT's.
/// </summary>
public static class AibtBuilder
{
    /// <summary>
    ///     Creates a normal animation bt
    /// </summary>
    /// <param name="btb"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    public static IBehaviourTreeNode NormalAibt(Entity entity)
    {
        var world = ServiceLocator.Get<World>();
        var size = 0.01f;
        return new BehaviourTreeBuilder()
            .Selector("bt")
                .Sequence("chase")
                .Condition("entityNearby", _ => world.ExistsInRange(entity, Tags.Character, size))
                    .Do("chase", data =>
                    {
                        // Get nearby entity and move local entity to the nearby entity 
                        var nearby = world.GetNearby(entity, Tags.Character, size);
                        ref var transform = ref nearby.Get<NetworkTransform>();
                        ref var movement = ref entity.Get<Movement>();
                        ref var attack = ref entity.Get<InCombat>();

                        movement.Target = transform.Pos;
                        attack.Entities.Get().Add(nearby);

                        return BehaviourTreeStatus.Success;
                    }).End()
                .Sequence("wandering")
                .Condition("chance", _ => RandomExtensions.GetRandom(0, 100.0f) <= 0.25f)
                    .Do("moveToRandomLocation", _ =>
                    {
                        // Get components
                        ref var transform = ref entity.Get<NetworkTransform>();
                        ref var movement = ref entity.Get<Movement>();

                        ref var grid = ref transform.Chunk;
                        var tile = grid.ToTile(13);

                        // Choose random offset and move to that position
                        // Do it till a position inside the chunk was choosen to prevent a mob from going to unloaded territory 
                        var counter = 0;
                        var max = 10;
                        do
                        {
                            var offsetX = RandomExtensions.GetRandom(-0.01f, 0.01f);
                            var offsetY = RandomExtensions.GetRandom(-0.01f, 0.01f);
                            movement.Target = new Vector2d { X = transform.Pos.X + offsetX, Y = transform.Pos.Y + offsetY };

                            counter++;
                        } while (!tile.Inside(movement.Target.X, movement.Target.Y) && counter <= 10);

                        return BehaviourTreeStatus.Success;
                    }).End()
                .End()
            .Build();
    }
}