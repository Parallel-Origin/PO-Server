using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using CommunityToolkit.HighPerformance;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.ECS.Components;
using ParallelOriginGameServer.Server.Systems;
using ZLogger;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the <see cref="Arch.Core.World" /> to query fast and efficient for certain entities.
/// </summary>
public static class EntityQueryExtensions
{
    /// <summary>
    ///     The character query description
    /// </summary>
    public static QueryDescription CharacterQueryDescription = new QueryDescription().WithAll<Character>().WithNone<Prefab>();

    /// <summary>
    ///     A cache list for storing character entities. 
    /// </summary>
    public static List<Entity> Characters = new(512);

    /// <summary>
    ///     Searches an entity by his id and returns it.
    /// </summary>
    /// <param name="grid">The grid we are using the chunk for</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity GetById(this World world, in long id)
    {
        var identities = LifecycleEventHandler.Identities;
        if (identities.TryGetValue(id, out var entity)) return entity;

        Program.Logger.ZLogDebug("EntityQuery returned default entity during getid, searched for {0}", id);
        return Entity.Null;
    }

    /// <summary>
    ///     Searches an chunk by the passed grid by using a dic.
    /// </summary>
    /// <param name="grid">The grid we are using the chunk for</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity GetChunk(this World world, in Grid grid)
    {
        var existing = LifecycleEventHandler.Chunks.TryGetValue(grid, out var chunkEntity);
        return existing ? chunkEntity : Entity.Null;
    }

    /// <summary>
    ///     Searches all surrounding chunks and returns them... if they exist of course.....
    /// </summary>
    /// <param name="grid">The grid we are using the chunk for</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<Entity> GetChunks(this World world, in Grid grid)
    {
        var surroundingGrids = grid.GetSurroundingGrids(1);
        var surroundingChunkEntities = new HashSet<Entity>(surroundingGrids.Count);

        foreach (var surrounding in surroundingGrids)
        {
            var existing = LifecycleEventHandler.Chunks.TryGetValue(surrounding, out var chunkEntity);
            if (existing) surroundingChunkEntities.Add(chunkEntity);
        }

        return surroundingChunkEntities;
    }

    /// <summary>
    ///     Searches all surrounding chunk grids and returns them... if they exist of course.
    /// </summary>
    /// <param name="grid">The grid we are using the chunk for</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HashSet<Grid> GetChunkGrids(this World world, in Grid grid)
    {
        var surroundingGrids = grid.GetSurroundingGrids(1);
        var surroundingChunkEntities = new HashSet<Grid>(surroundingGrids.Count);

        foreach (var surrounding in surroundingGrids)
        {
            var existing = LifecycleEventHandler.Chunks.ContainsKey(surrounding);
            if (existing) surroundingChunkEntities.Add(surrounding);
        }

        return surroundingChunkEntities;
    }
    
    /// <summary>
    ///     Searches an player by his name and returns him.
    /// </summary>
    /// <param name="grid">The grid we are using the chunk for</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity GetCharacter(this World world, string username)
    {
        // Fill query characters once with all registered players
        Characters.Clear();
        world.GetEntities(in CharacterQueryDescription, Characters);

        // Search for character with name
        foreach (ref var entity in Characters.AsSpan())
        {
            ref var character = ref entity.Get<Character>();
            if (character.Name.Equals(username)) return entity;
        }

        return default;
    }
}