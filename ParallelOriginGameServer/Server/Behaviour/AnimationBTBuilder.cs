using Arch.Core;
using Arch.Core.Extensions;
using FluentBehaviourTree;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server.Behaviour;

/// <summary>
///     A class which stores several methods for building BT's.
/// </summary>
public static class AnimationBtBuilder
{
    /// <summary>
    ///     Creates a normal animation bt
    /// </summary>
    /// <param name="btb"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    public static IBehaviourTreeNode NormalAnimationBt(Entity entity)
    {
        return new BehaviourTreeBuilder()
            .Selector("bt")
                .Sequence("moving")
                .Condition("checkMoving", _ => entity.Has<Toggle<Moving>>())
                    .Do("playMove", _ =>
                    {
                        // Set run to true
                        ref var animation = ref entity.Get<Animation>();
                        var boolParams = animation.BoolParams.Get();
                        boolParams.SetOnce("Run", true);

                        return BehaviourTreeStatus.Success;
                    }).End()
                .Sequence("attacking")
                .Condition("checkAttacking", _ => entity.Has<Attacks>())
                    .Do("playAttack", _ =>
                    {
                        // Set run to true
                        ref var animation = ref entity.Get<Animation>();
                        var triggers = animation.Triggers.Get();
                        triggers.Add("Attack");

                        return BehaviourTreeStatus.Success;
                    }).End()
                    .Do("playIdle", _ =>
                    {
                        ref var animation = ref entity.Get<Animation>();
                        var boolParams = animation.BoolParams.Get();
                        boolParams.SetOnce("Run", false);

                        return BehaviourTreeStatus.Success;
                    })
                .End()
            .Build();
    }
}