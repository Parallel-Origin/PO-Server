using System;
using Arch.Core;
using Arch.Core.Extensions;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the <see cref="Entity" /> class
/// </summary>
public static class EntityExtensions
{
    /// <summary>
    ///     Gets an component from the entity and if it does not exist yet it gets created.
    /// </summary>
    /// <param name="entity"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetOrDefault<T>(this ref Entity entity)
    {
        if (entity.Has<T>())
            return entity.Get<T>();

        return default;
    }

    /// <summary>
    ///     Gets an component from the entity and if it does not exist yet it gets created.
    /// </summary>
    /// <param name="entity"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetOrDefault<T>(this ref Entity entity, Func<T> create)
    {
        if (entity.Has<T>())
            return entity.Get<T>();

        return create();
    }

    /// <summary>
    ///     Gets an component from the entity and if it does not exist yet it gets created.
    /// </summary>
    /// <param name="entity"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static ref T GetOrSet<T>(this ref Entity entity)
    {
        if (entity.Has<T>())
            return ref entity.Get<T>();

        entity.Set<T>();
        return ref entity.Get<T>();
    }

    /// <summary>
    ///     Gets an component from the entity and if it does not exist yet it gets created.
    /// </summary>
    /// <param name="entity"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static ref T GetOrSet<T>(this ref Entity entity, Func<T> create)
    {
        if (entity.Has<T>())
            return ref entity.Get<T>();

        entity.Set(create());
        return ref entity.Get<T>();
    }
}