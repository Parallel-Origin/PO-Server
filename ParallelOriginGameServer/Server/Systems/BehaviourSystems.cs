using Arch.Core;
using Arch.System;
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
///     A system which iterates over all <see cref="AnimationController" /> with <see cref="Animation" />'s to run their BT for controlling the animation states.
/// </summary>
public sealed partial class ControllerSystem : BaseSystem<World, float>
{    
    public ControllerSystem(World world) : base(world)
     {
     }
    
    /// <summary>
    /// Iterates over all <see cref="AiController"/> to update them accordingly.
    /// </summary>
    /// <param name="state">The delta time.</param>
    /// <param name="aiController">The entities <see cref="AiController"/>.</param>
    [Query]
    [None(typeof(Prefab), typeof(Dead))]
    private void AiController(float state, ref AiController aiController)
    {
        var timeData = new TimeData(state);
        aiController.BehaviourTree.Tick(timeData);
    }

    /// <summary>
    /// Iterates over all <see cref="Animation"/> to update them accordingly.
    /// </summary>
    /// <param name="state">The delta time.</param>
    /// <param name="ac">The entities <see cref="Animation"/>.</param>
    [Query]
    [None(typeof(Prefab), typeof(Dead))]
    private void AnimationController(float state, ref Animation ac)
    {
        // Resetting triggers once, so that they never stay. 
        var timeData = new TimeData(state);
        ac.BehaviourTree.Tick(timeData);
    }
}

/// <summary>
///     Its own system since its place might vary. Might can be merged with the animation controller system, on the other hand it should be possible to have animations without an explicit controller.
/// </summary>
public sealed partial class ClearTrackedAnimationsSystem : BaseSystem<World,float>
{
    public ClearTrackedAnimationsSystem(World world) : base(world)
    {
    }
    
    [Query]
    [None(typeof(Prefab))]
    private void ClearAnimation(ref Animation animation)
    {
        animation.BoolParams.Get().ClearTracked();
        animation.Triggers.Get().Clear();
    }
}