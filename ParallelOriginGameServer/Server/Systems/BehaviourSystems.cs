using Arch.Core;
using Arch.System;
using Arch.System.SourceGenerator;
using FluentBehaviourTree;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which controlls all systems which process commands.
///     Basically entities which contain logic to do something once.
/// </summary>
public sealed class BehaviourGroup : Group<float>
{
    public BehaviourGroup(World world) : base(
        new ControllerSystem(world)
    )
    {
    }
}

/// <summary>
///     The <see cref="ControllerSystem"/> class 
///     controlls the behaviour-trees acting as controllers for ai, animations and more.
/// </summary>
public sealed partial class ControllerSystem : BaseSystem<World, float>
{    
    public ControllerSystem(World world) : base(world)
     {
     }
    
    /// <summary>
    ///     Iterates over all <see cref="AiController"/> to update them accordingly.
    /// </summary>
    /// <param name="state">The delta time.</param>
    /// <param name="aiController">The entities <see cref="AiController"/>.</param>
    [Query]
    [None<Dead, Prefab>]
    private void AiController(float state, ref AiController aiController)
    {
        var timeData = new TimeData(state);
        aiController.BehaviourTree.Tick(timeData);
    }

    /// <summary>
    ///     Iterates over all <see cref="Animation"/> to update them accordingly.
    /// </summary>
    /// <param name="state">The delta time.</param>
    /// <param name="ac">The entities <see cref="Animation"/>.</param>
    [Query]
    [None<Dead, Prefab>]
    private void AnimationController(float state, ref Animation ac)
    {
        // Resetting triggers once, so that they never stay. 
        var timeData = new TimeData(state);
        ac.BehaviourTree.Tick(timeData);
    }
}

/// <summary>
///     The <see cref="ClearTrackedAnimationsSystem"/>
///     clears the tracked animation-variables once per frame.
///     Important for the way animations work.
/// </summary>
public sealed partial class ClearTrackedAnimationsSystem : BaseSystem<World,float>
{
    public ClearTrackedAnimationsSystem(World world) : base(world)
    {
    }
    
    [Query]
    [None<Prefab>]
    private void ClearAnimation(ref Animation animation)
    {
        animation.BoolParams.Get().ClearTracked();
        animation.Triggers.Get().Clear();
    }
}