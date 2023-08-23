using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOriginGameServer.Server.Persistence;
using ParallelOriginGameServer.Server.Systems;
using ParallelOriginGameServer.Server.ThirdParty;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for working with <see cref="ParallelOrigin.Core.ECS.Components.Environment.Chunk" />'s in an easy way. Mainly focuses the <see cref="World" />
/// </summary>
public static class WorldEnvironmentExtensions
{
    /// <summary>
    ///     Makes the chunk loader entity enter a chunk and its surrounding chunks.
    ///     If the chunk/chunks do not exist yet they will either be loaded from the database or being created.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="en"></param>
    /// <param name="enteredGrid"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnterChunks(this World world, Entity en, in Grid enteredGrid)
    {
        // Find chunk, if it exists... simply add the loader... if not, create a new chunk. 
        var surroundingGrids = enteredGrid.GetSurroundingGrids(1);
        var existingChunks = world.GetChunkGrids(in enteredGrid);
        var nonExistingCunks = new HashSet<Grid>(surroundingGrids);
        nonExistingCunks.ExceptWith(existingChunks);

        // Add ourself as a chunk loader to all chunks in range which are already loaded
        foreach (var grid in existingChunks)
        {
            var chunkEntity = world.GetChunk(in grid);
            ref var chunk = ref chunkEntity.Get<Chunk>();
            chunk.LoadedBy.Add(en);
        }

        // Prevent database operations if all chunks already exist
        if (nonExistingCunks.Count <= 0) return;

        // Get the db
        // Check which chunks already exist and which not
        var context = ServiceLocator.Get<GameDbContext>();
        context.ChunksExist(nonExistingCunks).ContinueWith(task =>
        {
            var databaseChunks = task.Result;
            nonExistingCunks.ExceptWith(databaseChunks);

            var chunkCommand = new ChunkCommand { Operation = ChunkOperation.CREATE, By = en, Grids = nonExistingCunks };
            ChunkCommandSystem.Add(chunkCommand);

            return databaseChunks;
        }).ContinueWith(async task =>
        {
            // Create existing chunk entities from the database dto
            var databaseChunks = task.Result;
            var chunks = await context.GetChunks(databaseChunks);

            var chunkCommand = new ChunkCommand { Operation = ChunkOperation.LOADED, By = en, Chunks = chunks };
            ChunkCommandSystem.Add(chunkCommand);
        });
    }

    /// <summary>
    ///     Makes an chunk loader entity leave all surrounding chunks.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="en"></param>
    /// <param name="enteredGrid"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LeaveChunks(this World world, in Entity en, in Grid enteredGrid)
    {
        // Remove the chunkloader entitiy from all surrounding chunks 
        var surroundingChunks = world.GetChunks(in enteredGrid);
        foreach (var chunkEntity in surroundingChunks)
        {
            ref var chunk = ref chunkEntity.Get<Chunk>();
            chunk.LoadedBy.Remove(en);
        }
    }

    /// <summary>
    ///     Performanes queries to search the left and the newly entered chunks.
    ///     Switches the chunk and makes the entity move from the old one into the new one.
    ///     Basically removed the entity from the old chunk and adds it to the new one
    /// </summary>
    /// <param name="world"></param>
    /// <param name="en"></param>
    /// <param name="left"></param>
    /// <param name="entered"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SwitchChunks(this World world, in Entity en, in Grid left, in Grid entered)
    {
        var leftChunk = world.GetChunk(in left);
        var enteredChunk = world.GetChunk(in entered);

        if (enteredChunk.IsAlive())
        {
            ref var chunk = ref enteredChunk.Get<Chunk>();
            chunk.Contains.Get().Add(en);
        }

        if (leftChunk.IsAlive())
        {
            ref var chunk = ref leftChunk.Get<Chunk>();
            chunk.Contains.Get().Remove(en);
        }
    }
}