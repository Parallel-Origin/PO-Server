using System;
using System.Collections.Generic;
using Arch.Bus;
using Arch.CommandBuffer;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using Arch.System.SourceGenerator;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Events;
using ParallelOriginGameServer.Server.Extensions;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;

namespace ParallelOriginGameServer.Server.Systems;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;

/// <summary>
///     An <see cref="ISystem{T}" /> which takes care of assigning an <see cref="Identity.Id" /> and marking it as <see cref="Created" /> and <see cref="Dirty" /> for the first time.
/// </summary>
public class InitialisationSystem : BaseSystem<World, float>
{
    /// <summary>
    ///     Marks an <see cref="Entity"/> as initialized and registered.
    /// </summary>
    public struct Initialized{ }
    
    public InitialisationSystem(World world) : base(world)
    {
    }
    
    public override void Update(in float t)
    {
        base.Update(in t);
        
        var notInitialized = new QueryDescription().WithAll<Identity>().WithNone<Prefab, Initialized>();
        
        // Add initialized and set id
        World.Query(in notInitialized, (in Entity en, ref Identity identity) =>
        {
            var @event = new CreateEvent(en, ref identity);
            EventBus.Send(@event);
        });
        World.Add<Initialized>(in notInitialized);
    }
}

/// <summary>
///     A system iterating over <see cref="Destroy" /> components to dispose and destroy entities taged with them.
/// </summary>
public sealed partial class DestroySystem : BaseSystem<World, float>
{
    private CommandBuffer _commandBuffer;
    private readonly QueryDescription _toDestroy = new QueryDescription().WithAll<Identity, InitialisationSystem.Initialized, Destroy>().WithNone<Prefab>();
    private readonly QueryDescription _toDestroyChunks = new QueryDescription().WithAll<Identity, Chunk, InitialisationSystem.Initialized, Destroy>().WithNone<Prefab>();
    
    public DestroySystem(World world) : base(world)
    {
    }

    public override void Initialize()
    {
        base.Initialize();
        _commandBuffer = World.Record();
    }

    public override void Update(in float time)
    {
        DestroyChildQuery(World);
        DestroyAfterQuery(World, time);
        
        // Remove initialized from entitys ready for being destroyed
        World.Query(in _toDestroy, (in Entity en, ref Identity identity) =>
        {
            var @event = new DestroyEvent(en, ref identity);
            EventBus.Send(@event);
        });
        
        // Remove initialized from entitys ready for being destroyed
        World.Query(in _toDestroyChunks, (in Entity en, ref Chunk chunk) =>
        {
            var @event = new ChunkDestroyedEvent(en, chunk.Grid);
            EventBus.Send(@event);
        });
        
        World.Destroy(in _toDestroy);
    }
    
    /// <summary>
    /// Iterates over all <see cref="Parent"/> entities to destroy their children aswell.
    /// </summary>
    /// <param name="parent">The <see cref="Parent"/> component.</param>
    [Query]
    [All<Destroy>, None<Prefab>]
    private void DestroyChild(ref Parent parent)
    {
        // Mark all childs for destruction
        for (var index = 0; index < parent.Children.Count; index++)
        {
            var childrenRef = parent.Children[index];
            var child = (Entity) childrenRef;
            
            _commandBuffer.Add<Destroy>(child);
        }
    }
    
    /// <summary>
    /// Iterates over all <see cref="DestroyAfter"/> marked entities to mark them with destroy once the time is up.
    /// </summary>
    /// <param name="state">The delta time.</param>
    /// <param name="entity">The <see cref="Entity"/> itself.</param>
    /// <param name="after">Its <see cref="DestroyAfter"/> component.</param>
    [Query]
    [None<Destroy,Prefab>]
    private void DestroyAfter([Data] float state, in Entity entity, ref DestroyAfter after)
    {
        after.Seconds -= state;
        if (after.Seconds >= 0.0f) return;
        
        _commandBuffer.Add<Destroy>(entity);
    }
}