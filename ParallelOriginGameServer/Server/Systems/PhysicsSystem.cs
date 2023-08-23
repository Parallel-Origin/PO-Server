using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using Collections.Pooled;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.ECS.Events;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.ThirdParty;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which controlls all systems which controll physics, collisions and quad trees.
/// </summary>
public sealed class PhysicsGroup : Arch.System.Group<float>
{
    public PhysicsGroup(World world) : base(
        
        // Physics
        new IntervallGroup(1.0f,
            new PhysicsSystem(world)
        ),
        new InactiveSystem(world)
    )
    {
    }
}

/// <summary>
/// AOI payload passed to the quadtree
/// </summary>
readonly struct AoiPayload
{
    public readonly PooledSet<EntityLink> Aoi;

    public AoiPayload(PooledSet<EntityLink> aoi)
    {
        this.Aoi = aoi;
    }
}


// Payload being passed to the quadtree query 
readonly struct CollisionPayload
{
    public readonly Entity Local;
    public readonly RectangleF ColliderRect;
    public readonly PooledSet<Collision> Collided;

    public CollisionPayload(Entity local, RectangleF colliderRect, PooledSet<Collision> collided)
    {
        this.Local = local;
        this.ColliderRect = colliderRect;
        this.Collided = collided;
    }
}

/// <summary>
///     A system which iterates over all <see cref="NetworkTransform" />'s to update their quad tree location which is important for networking the AOI.
/// </summary>
public sealed partial class PhysicsSystem : BaseSystem<World,float>
{
    private const float WidthAndHeight = 0.01f;

    private readonly PooledSet<Collision> _lastFrame = new(512);
    private readonly PooledSet<Collision> _entered = new(512);
    private readonly PooledSet<Collision> _collided = new(512); 
    private readonly PooledSet<Collision> _left = new(512);

    public PhysicsSystem(World world) : base(world)
    {
    }
    
    /// <summary>
    /// Iterates over all <see cref="NetworkTransform"/> entities to set their <see cref="QuadTree"/> position. 
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="identity">The <see cref="Identity"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [None(typeof(Destroy), typeof(Chunk), typeof(Dead), typeof(Prefab))] // Dont process entities which are almost destroyed and dead, may leave them inside the quadtree ? 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void QuadTree(in Entity en, ref Identity identity, ref NetworkTransform transform)
    {
        var tree = World.GetTree();
        var quadEntity = new QuadEntity(en, identity.Id, ref transform);

        if (tree.Contains(quadEntity))
            tree.Move(ref quadEntity);
        else tree.Add(quadEntity);
    }
    
    /// <summary>
    /// Iterates over all <see cref="Aoi"/> entities to query all entities in their AOI and to update their components aswell as fire events. 
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="aoi">The <see cref="Aoi"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [None(typeof(Prefab), typeof(Inactive))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Aoi(in Entity en, ref Aoi aoi, ref NetworkTransform transform)
    {
        var thisFrame = ObjectPool<PooledSet<EntityLink>>.Get();
        var lastFrame = aoi.Entities.Get();
        var entered = aoi.Entered.Get();
        var left = aoi.Left.Get();
        
        thisFrame.Clear();
        entered.Clear();
        left.Clear();

        // Query the quadtree to determine whats inside the aoi and put it into the entered aoi
        var payload = new AoiPayload(thisFrame);
        var rect = new RectangleF((float)transform.Pos.X - aoi.Range / 2, (float)transform.Pos.Y - aoi.Range / 2, aoi.Range, aoi.Range);
        World.GetTree().GetObjects(rect, ref payload, (ref AoiPayload payload, ref QuadEntity quadEntity) => {
            payload.Aoi.Add(quadEntity.EntityRef);
        });
        
        // New 
        entered.UnionWith(thisFrame);
        entered.ExceptWith(lastFrame);

        // Left
        left.UnionWith(lastFrame);
        left.ExceptWith(thisFrame);

        // Copy stayed aois from this frame into the last frame 
        lastFrame.Clear();
        lastFrame.UnionWith(thisFrame);

        SendAoiEvents(en, entered, lastFrame, left);
        ObjectPool<PooledSet<EntityLink>>.Return(thisFrame);
    }
    
    /// <summary>
    /// Iterates over all <see cref="BoxCollider"/>s to check if they collide and to fire events. 
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="transform">Its <see cref="NetworkTransform"/>.</param>
    /// <param name="collider">Its <see cref="BoxCollider"/>.</param>
    [Query]
    [None(typeof(Prefab), typeof(Inactive), typeof(Dead))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Collision(in Entity en, ref NetworkTransform transform, ref BoxCollider collider)
    {
        var tree = World.GetTree();

        // Create collider and the rect we check collisions in
        var colliderRect = new RectangleF((float)transform.Pos.X - WidthAndHeight / 2, (float)transform.Pos.Y - WidthAndHeight / 2, collider.Width, collider.Height);
        var rect = new RectangleF((float)transform.Pos.X - WidthAndHeight / 2, (float)transform.Pos.Y - WidthAndHeight / 2, WidthAndHeight, WidthAndHeight);

        // Create payload being passed to the quadtree to avoid garbage
        var payload = new CollisionPayload(en, colliderRect, _collided);

        // Query entities to check with which one the current entity collides
        tree.GetObjects(rect, ref payload, (ref CollisionPayload payload, ref QuadEntity quadEntity) =>
        {
            var entity = (Entity)quadEntity.EntityRef;
            
            // If the entity is the entity we check collisions for... prevent that
            if (!entity.Has<BoxCollider>()) return; // Ignore those without colliders 
            if (entity.Equals(payload.Local)) return;
            
            // Get entity components
            ref var entityNetworkTransform = ref entity.Get<NetworkTransform>();
            ref var entityCollider = ref entity.Get<BoxCollider>();

            // Check if we are colliding.
            var entityColliderRect = new RectangleF((float)entityNetworkTransform.Pos.X - WidthAndHeight / 2, (float)entityNetworkTransform.Pos.Y - WidthAndHeight / 2, entityCollider.Width, entityCollider.Height);
            if (!payload.ColliderRect.IntersectsWith(entityColliderRect)) return;

            var collided = new Collision(payload.Local, entity);
            payload.Collided.Add(collided);
        });
    }

    public override void AfterUpdate(in float t)
    {
        base.AfterUpdate(in t);
        
        // New 
        _entered.UnionWith(_collided);
        _entered.ExceptWith(_lastFrame);

        // Left
        _left.UnionWith(_lastFrame);
        _left.ExceptWith(_collided);

        // Copy stayed collisions from this frame into the last frame 
        _lastFrame.Clear();
        _lastFrame.UnionWith(_collided);
        
        // Clear 
        _entered.Clear();
        _collided.Clear();
        _left.Clear();
    }
    
    /// <summary>
    /// Creates AOI event entities which can be queries to update the client for example. 
    /// </summary>
    /// <param name="currentEntity">The <see cref="Entity"/> to fire events for.</param>
    /// <param name="entered">All entered entities.</param>
    /// <param name="aoi">All stayed entities.</param>
    /// <param name="left">All left entities.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SendAoiEvents(Entity currentEntity, PooledSet<EntityLink> entered, PooledSet<EntityLink> aoi, PooledSet<EntityLink> left)
    {
        var enteredEvent = new AoiEnteredEvent(currentEntity, entered);
        var stayedEvent = new AoiStayedEvent(currentEntity, aoi);
        var leftEvent = new AoiLeftEvent(currentEntity, left);

        EventBus.Send(ref enteredEvent);
        EventBus.Send(ref stayedEvent);
        EventBus.Send(ref leftEvent);
    }
}

/// <summary>
///     A system iterating over <see cref="Destroy" /> marked entities to remove them from the quadtree.
/// </summary>
public sealed partial class InactiveSystem : BaseSystem<World,float>
{
    
    public InactiveSystem(World world) : base(world)
    {
    }
    
    /// <summary>
    ///     A system iterating over <see cref="Destroy" /> marked entities to remove them from the quadtree.
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="identity">Its <see cref="Identity"/>.</param>
    /// <param name="transform">Its <see cref="NetworkTransform"/>.</param>
    /// </summary>
    [Query]
    [All(typeof(Destroy)), None(typeof(Prefab), typeof(Chunk))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearInactiveQuadTree(in Entity en, ref Identity identity, ref NetworkTransform transform)
    {
        var quadEntity = new QuadEntity(en, identity.Id, ref transform);
        World.GetTree().Remove(quadEntity);
    }
    
    /// <summary>
    /// Clears inactive aois, otherwhise aoi wont work properly on relogin. 
    /// </summary>
    /// <param name="aoi">The <see cref="Aoi"/>.</param>
    [Query]
    [All(typeof(Inactive)), None(typeof(Prefab))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearInactiveAoi(ref Aoi aoi)
    {
        aoi.Entities.Get().Clear();
    }
}