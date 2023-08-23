using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.ECS.Components.Items;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     Extensions for <see cref="IWeight" /> as an alternative to the <see cref="WeightTable{T}" /> for providing weight calculation methods without requiring an instance
///     Especially targets Arrays, does not make use of <see cref="IEnumerable{T}" /> due to the fact that its useage is way slower and as far as i know also causes garbage
/// </summary>
public static partial class WeightTableExtensions<C> where C : IWeight
{
    /// <summary>
    ///     Calculates the total weight of the passed <see cref="Entity" />'s
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float TotalWeight(C[] entities)
    {
        var weight = 0f;
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            weight += entity.Weight;
        }

        return weight;
    }

    /// <summary>
    ///     Returns a weighted <see cref="Entity" /> based on its <see cref="IWeight" />-Component
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static C Get(C[] entities)
    {
        // Assign total weight
        var totalWeight = TotalWeight(entities);
        var randomVal = RandomExtensions.GetRandom(0, totalWeight);

        // Weight based spawning mechanism to select entity 
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            var weight = entity.Weight;
            if (randomVal < weight) return entity;
            randomVal -= weight;
        }

        return entities.Length == 1 ? entities[0] : default;
    }

    /// <summary>
    ///     Returns a weighted <see cref="Entity" /> based on its <see cref="IWeight" />-Component
    ///     Better performance because <see cref="totalWeight" /> can be calculated once instead of every call
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static C Get(float totalWeight, C[] entities)
    {
        // Assign total weight
        var randomVal = RandomExtensions.GetRandom(0, totalWeight);

        // Weight based spawning mechanism to select entity 
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            var weight = entity.Weight;
            if (randomVal < weight) return entity;
            randomVal -= weight;
        }

        return entities.Length == 1 ? entities[0] : default;
    }
}

/// <summary>
///     Extensions for <see cref="IWeight" /> as an alternative to the <see cref="WeightTable{T}" /> for providing weight calculation methods without requiring an instance
///     Especially targets <see cref="ReadOnlySpan{T}" />
/// </summary>
public static partial class WeightTableExtensions<C> where C : IWeight
{
    /// <summary>
    ///     Calculates the total weight of the passed <see cref="Entity" />'s
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float TotalWeight(ReadOnlySpan<C> entities)
    {
        var weight = 0f;
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            weight += entity.Weight;
        }

        return weight;
    }

    /// <summary>
    ///     Returns a weighted <see cref="Entity" /> based on its <see cref="IWeight" />-Component
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static C Get(ReadOnlySpan<C> entities)
    {
        // Assign total weight
        var totalWeight = TotalWeight(entities);
        var randomVal = RandomExtensions.GetRandom(0, totalWeight);

        // Weight based spawning mechanism to select entity 
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            var weight = entity.Weight;
            if (randomVal < weight) return entity;
            randomVal -= weight;
        }

        // If theres only one entity, return that one because otherwhise the get method ALWAYS returns the default entity 
        return entities.Length == 1 ? entities[0] : default;
    }

    /// <summary>
    ///     Returns a weighted <see cref="Entity" /> based on its <see cref="IWeight" />-Component
    ///     Better performance because <see cref="totalWeight" /> can be calculated once instead of every call
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static C Get(float totalWeight, ReadOnlySpan<C> entities)
    {
        // Assign total weight
        var randomVal = RandomExtensions.GetRandom(0, totalWeight);

        // Weight based spawning mechanism to select entity 
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            var weight = entity.Weight;
            if (randomVal < weight) return entity;
            randomVal -= weight;
        }

        return entities.Length == 1 ? entities[0] : default;
    }
}

/// <summary>
///     Extensions for <see cref="IWeight" /> as an alternative to the <see cref="WeightTable{T}" /> for providing weight calculation methods without requiring an instance
///     Especially targets <see cref="Entity" /> and <see cref="EntitySet" />
/// </summary>
public static partial class WeightTableExtensions<C> where C : IWeight
{
    /// <summary>
    ///     Calculates the total weight of the passed <see cref="Entity" />'s
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float TotalWeight(ReadOnlySpan<Entity> entities)
    {
        var weight = 0f;
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            ref var weighted = ref entity.Get<C>();
            weight += weighted.Weight;
        }

        return weight;
    }

    /// <summary>
    ///     Returns a weighted <see cref="Entity" /> based on its <see cref="IWeight" />-Component
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity Get(ReadOnlySpan<Entity> entities)
    {
        // Assign total weight
        var totalWeight = TotalWeight(entities);
        var randomVal = RandomExtensions.GetRandom(0, totalWeight);

        // Weight based spawning mechanism to select entity 
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            ref var weighted = ref entity.Get<C>();
            var weight = weighted.Weight;
            if (randomVal < weight) return entity;
            randomVal -= weight;
        }

        return entities.Length == 1 ? entities[0] : default;
    }

    /// <summary>
    ///     Returns a weighted <see cref="Entity" /> based on its <see cref="IWeight" />-Component
    ///     Better performance because <see cref="totalWeight" /> can be calculated once instead of every call
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity Get(float totalWeight, ReadOnlySpan<Entity> entities)
    {
        // Assign total weight
        var randomVal = RandomExtensions.GetRandom(0, totalWeight);

        // Weight based spawning mechanism to select entity 
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];
            ref var weighted = ref entity.Get<C>();
            var weight = weighted.Weight;
            if (randomVal < weight) return entity;
            randomVal -= weight;
        }

        return entities.Length == 1 ? entities[0] : default;
    }
}