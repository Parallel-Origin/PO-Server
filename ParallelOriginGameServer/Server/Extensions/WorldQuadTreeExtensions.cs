using System;
using System.Drawing;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Transform;
using QuadTrees;
using QuadTrees.Common;
using QuadTrees.QTreePointF;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     Represents an entity transform for the quadtree.
/// </summary>
public readonly struct QuadEntity : IPointFQuadStorable, IEquatable<QuadEntity>
{
    // A entity reference which references an entity struct and a unique id for that entity ( in case it might get deleted, unique id is important to persist )
    public readonly EntityLink EntityRef;
    private readonly PointF _point;

    public QuadEntity(Entity en, long id, ref NetworkTransform transform)
    {
        EntityRef = new EntityLink(en, id);
        _point = new PointF((float)transform.Pos.X, (float)transform.Pos.Y);
    }
    
    public QuadEntity(ref EntityLink entityRef, ref NetworkTransform transform)
    {
        this.EntityRef = entityRef;
        _point = new PointF((float)transform.Pos.X, (float)transform.Pos.Y);
    }

    public bool Equals(QuadEntity other)
    {
        return EntityRef.Equals(other.EntityRef);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 23 + EntityRef.GetHashCode();
            return hash;
        }
    }

    public PointF Point => _point;
}

/// <summary>
///     A extension for the world which stores several methods to interact with the most important quadtrees ingame.
/// </summary>
public static class WorldQuadTreeExtensions
{
    public static QuadTreePointF<QuadEntity> QuadTree { get; set; } = new(new RectangleF(float.MinValue / 2, float.MinValue / 2, float.MaxValue, float.MaxValue));

    /// <summary>
    ///     Returns the worlds <see cref="QuadTree" />
    /// </summary>
    /// <param name="world"></param>
    /// <returns></returns>
    public static QuadTreePointF<QuadEntity> GetTree(this World world)
    {
        return QuadTree;
    }

    /// <summary>
    ///     Executes an action for all entities in range of the <see cref="RectangleF" />
    /// </summary>
    /// <param name="world"></param>
    /// <param name="rect"></param>
    /// <param name="action"></param>
    public static void ForEntitiesInRange(this World world, RectangleF rect, ForObjectStruct<QuadEntity> action)
    {
        QuadTree.GetObjects(rect, action);
    }

    /// <summary>
    ///     Returns true if a certain type exists in range.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="rect"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool ExistsInRange(this World world, RectangleF rect, string type)
    {
        var payload = new ExistInRangePayload { World = world, Type = type, Exists = false };
        QuadTree.GetObjects(rect, ref payload, (ref ExistInRangePayload payload, ref QuadEntity quadEntity) =>
        {
            var entity = quadEntity.EntityRef.Resolve(payload.World);
            ref var identity = ref entity.Get<Identity>();

            if (identity.Type.Equals(payload.Type)) payload.Exists = true;
        });

        return payload.Exists;
    }

    /// <summary>
    ///     Checks wether a entity of a certain type is nearby to the entity and returns true or false.
    /// </summary>
    /// <param name="tree"></param>
    /// <param name="entity"></param>
    /// <param name="type"></param>
    /// <param name="distance"></param>
    /// <returns></returns>
    public static bool ExistsInRange(this World world, Entity entity, string type, float distance)
    {
        var payload = new ExistInRangePayload { World = world, Type = type, Exists = false };

        ref var transform = ref entity.Get<NetworkTransform>();
        var rect = new RectangleF((float)transform.Pos.X - distance / 2, (float)transform.Pos.Y - distance / 2, distance, distance);
        QuadTree.GetObjects(rect, ref payload, (ref ExistInRangePayload payload, ref QuadEntity quadEntity) =>
        {
            // Get entity components
            var entity = (Entity) quadEntity.EntityRef;
            ref var identity = ref entity.Get<Identity>();
            if (identity.Tag.Equals(payload.Type))
                payload.Exists = true;
        });

        return payload.Exists;
    }

    /// <summary>
    ///     Checks wether a entity of a certain type is nearby to the entity and returns true or false.
    /// </summary>
    /// <param name="tree"></param>
    /// <param name="entity"></param>
    /// <param name="type"></param>
    /// <param name="distance"></param>
    /// <returns></returns>
    public static Entity GetNearby(this World world, Entity entity, string type, float distance)
    {
        var payload = new GetNearbyPayload { World = world, Type = type, Exists = default };

        ref var transform = ref entity.Get<NetworkTransform>();
        var rect = new RectangleF((float)transform.Pos.X - distance / 2, (float)transform.Pos.Y - distance / 2, distance, distance);
        QuadTree.GetObjects(rect, ref payload, (ref GetNearbyPayload payload, ref QuadEntity quadEntity) =>
        {
            // Get entity components
            var entity = (Entity) quadEntity.EntityRef;
            ref var identity = ref entity.Get<Identity>();
            if (!identity.Tag.Equals(payload.Type)) return;

            payload.Exists = entity;
        });

        return payload.Exists;
    }

    /// Payload for passing to the quadtree queries to reduce allocs
    public struct ExistInRangePayload
    {
        public World World;
        public string Type;
        public bool Exists;
    }


    /// Payload for passing to the quadtree queries to reduce allocs
    public struct GetNearbyPayload
    {
        public World World;
        public string Type;
        public Entity Exists;
    }
}