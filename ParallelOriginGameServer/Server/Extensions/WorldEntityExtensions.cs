using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.CommandBuffer;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using ConcurrentCollections;
using LiteNetLib;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.ECS.Events;
using ParallelOriginGameServer.Server.Persistence;
using ParallelOriginGameServer.Server.Systems;
using ParallelOriginGameServer.Server.ThirdParty;
using Character = ParallelOrigin.Core.ECS.Components.Character;
using Chunk = ParallelOriginGameServer.Server.Persistence.Chunk;
using Identity = ParallelOrigin.Core.ECS.Components.Identity;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An class which contains multiple world extensions.
/// </summary>
public static class WorldEntityExtensions
{

    /// <summary>
    /// Returns a <see cref="CommandBuffer"/> instance used to record operations on <see cref="Entity"/>s.
    /// </summary>
    /// <param name="world">The <see cref="World"/> instance.</param>
    /// <returns>The <see cref="CommandBuffer"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandBuffer Record(this World world)
    {
        var scbs = ServiceLocator.Get<StartCommandBufferSystem>();
        return scbs.EntityCommandBuffer;
    }
    
    /// <summary>
    ///     Creates an character based on an <see cref="Account" />.
    ///     Cant be done via the prototyper, but it uses the prototyper.
    ///     Assigns peer and model, marks the entity as logedIn and as active.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="peer"></param>
    /// <param name="account"></param>
    public static void CreateCharacter(this World world, NetPeer peer, Account account)
    {
        var entity = account.ToEcs();

        ref var character = ref entity.Get<Character>();
        character.Peer = peer.ToHandle();

        entity.Add<LogedIn>();
    }

    /// <summary>
    ///     Initialises an character by assigning the network connection and transfering its account data into it.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="peer"></param>
    /// <param name="account"></param>
    public static void InitializeCharacter(this World world, NetPeer peer, Account account)
    {
        var existingEntity = world.GetById(account.Character.IdentityId);

        ref var character = ref existingEntity.Get<Character>();
        character.Peer = peer.ToHandle();

        existingEntity.Add<LogedIn>();
        existingEntity.Remove<Inactive>();
        
        // Fire event
        EventBus.Send(new LoginEvent(existingEntity));
    }

    /// <summary>
    ///     Deinitializes an character which basically marks him as loged out and non active.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="peer"></param>
    /// <param name="account"></param>
    public static void DeinitializeCharacter(this World world, NetPeer peer, Account account)
    {
        var existingEntity = world.GetById(account.Character.IdentityId);
        
        existingEntity.Remove<LogedIn>();
        existingEntity.Add<Inactive>();
    }

    /// <summary>
    ///     Creates a new chunk.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="grid"></param>
    public static Entity CreateChunk(this World world, in Grid grid)
    {
        // Create chunk components
        var identity = new Identity { Type = "0:1", Tag = "chunk" };
        var chunk = new ParallelOrigin.Core.ECS.Components.Chunk(grid);

        // Record them and play them back properly 
        var entity = world.Create(identity, chunk, new NetworkTransform());
        return entity;
    }

    /// <summary>
    ///     Creates a new chunk.
    /// </summary>
    /// <param name="world"></param>
    /// <param name="grid"></param>
    public static Entity CreateChunk(this World world, in Chunk chunk)
    {
        var entity = chunk.ToEcs();
        return entity;
    }

    public static List<Entity> GetEntitiesAsList(this World world, in QueryDescription queryDescription)
    {
        var list = new List<Entity>();
        world.GetEntities(in queryDescription, list);
        return list;
    }
}