using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Extensions;
using ZLogger;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which controlls all systems that make something move or rotate. 
/// </summary>
public sealed class MovementGroup : Group<float>
{
    public MovementGroup(World world) : base(
        new MovementSystem(world)     // Movement & Rotation
    )
    {
    }
}

/// <summary>
///     The <see cref="MovementSystem"/> class
///     calculates the movement of entities and their rotation. It virtually ensures that they move and rotate. 
/// </summary>
public sealed partial class MovementSystem : BaseSystem<World, float>
{
    public MovementSystem(World world) : base(world)
    {
    }
    
    // TODO : Fix source generator bug, when entity is called "en" it does not work properly. 
    
    /// <summary>
    /// Moves an <see cref="Entity"/> to its destination defined in its <see cref="Movement"/> component. 
    /// </summary>
    /// <param name="elapsedTime">The delta time.</param>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="transform">Its <see cref="NetworkTransform"/>.</param>
    /// <param name="movement">Its <see cref="Movement"/>.</param>
    [Query]
    [None(typeof(Prefab), typeof(Dead))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Move([Data] in float elapsedTime, in Entity entity, ref NetworkTransform transform, ref Movement movement)
    {
        // If target is zero ignore otherwhise entitys might move all the way to 0;0 forever...
        if (movement.Target.X == 0 && movement.Target.Y == 0) return;

        var step = movement.Speed * elapsedTime;
        var moved = transform.Pos.MoveTowards(ref movement.Target, in step);

        if (!moved) return;

        // Ensure entity components
        entity.AddOrGet<Toggle<Moving>>();
        entity.AddOrGet<Toggle<DirtyNetworkTransform>>();

        // Mark entity properly 
        var record = World.Record();
        record.Set(entity, new Toggle<Moving>(true));
        record.Set(entity, new Toggle<DirtyNetworkTransform>(true));
    }
    
    [Query]
    [None(typeof(Prefab), typeof(Dead))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Rotate([Data] in float elapsedTime, ref NetworkTransform transform, ref NetworkRotation rotation, ref Movement movement)
    {
        // Vector pointing from the pos to the target
        var step = movement.Speed * 10000 * elapsedTime;
        var directionVector = movement.Target - transform.Pos;
        var angle = Math.Atan2(directionVector.Y, directionVector.X);
        var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)angle);

        rotation.Value = Quaternion.Lerp(rotation.Value, rot, step);

        // Skip rotation if it almost reached its destination ( Wont get networked this way to save some bandwith )
        var rotated = rotation.Value - rot;
        var sum = Math.Abs(rotated.X + rotated.Y + rotated.Z + rotated.W);

        if (sum <= 0.1f) return;

        // TODO : Mark entity as rotated or stuff ?
    }
}

