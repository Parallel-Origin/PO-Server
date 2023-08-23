using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Events;
using ParallelOriginGameServer.Server.Extensions;

namespace ParallelOriginGameServer.Server;

public static class LifecycleEventHandler
{
    /// <summary>
    ///     A dictionary for fast lookup operations based on the global id of an entity.
    /// </summary>
    public static Dictionary<long, Entity> Identities { get; set; } = new (1024);
    
    /// <summary>
    ///     A dictionary for fast lookup operations to find a chunk <see cref="Entity"/> based on its <see cref="Grid"/>.
    /// </summary>
    public static Dictionary<Grid, Entity> Chunks { get; set; } = new (32);

    /// <summary>
    ///     Gets called on <see cref="CreateEvent"/>.
    ///     Generates an id for the passed <see cref="Entity"/> and tracks it in the <see cref="Identities"/>.
    /// </summary>
    /// <param name="event">The <see cref="CreateEvent"/>.</param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnCreateGenerateId(in CreateEvent @event)
    {
        // When id was already set ( database or whatever ), add to global list and return
        if (@event.Identity.Id is not 0)
        {
            Identities[@event.Identity.Id] = @event.Entity;
            return;
        }

        // Otherwhise generate new id 
        var uniqueLong = (long)RandomExtensions.GetUniqueULong();
        @event.Identity.Id = uniqueLong;

        Identities[uniqueLong] = @event.Entity;
    }
    
    /// <summary>
    ///     Gets called upon receiving a <see cref="DestroyEvent"/>.
    ///     Removes the destroyed entity from the <see cref="Identities"/>.
    /// </summary>
    /// <param name="event">The <see cref="DestroyEvent"/>.</param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnDestroyRemoveId(in DestroyEvent @event)
    {
        Identities.Remove(@event.Identity.Id);
    }

    /// <summary>
    ///     Gets called upon receiving a <see cref="ChunkCreatedEvent"/>.
    ///     Adds the newly created chunk to the <see cref="Chunks"/> dictionary.
    /// </summary>
    /// <param name="event"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnChunkCreatedEvent(in ChunkCreatedEvent @event)
    {
        Chunks.Add(@event.Grid, @event.Entity);
    }
    
    /// <summary>
    ///     Gets called upon receiving a <see cref="ChunkDestroyedEvent"/>.
    ///     Removes the chunk <see cref="Entity"/> from the <see cref="Chunks"/> dic.
    /// </summary>
    /// <param name="event"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnChunkDestroyedEvent(in ChunkDestroyedEvent @event)
    {
        Chunks.Remove(@event.Grid);
    }
}