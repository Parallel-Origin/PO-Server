using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.Core;
using Arch.System;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Events;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Network;
using ZLogger;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     An system which executes other systems in an defined intervall.
///     TODO: Make group stuff virtual for easy overriden
/// </summary>
public class IntervallGroup : ISystem<float>
{
    public IntervallGroup(float triggerSec, params ISystem<float>[] systems)
    {
        Systems = systems;
        TriggerSec = triggerSec;
    }

    public IntervallGroup(float triggerSec, IEnumerable<ISystem<float>> systems)
    {
        Systems = (systems ?? throw new ArgumentNullException(nameof(systems))).Where(s => s != null).ToArray();
        TriggerSec = triggerSec;
    }

    /// <summary>
    /// The registered <see cref="ISystem{T}"/>s.
    /// </summary>
    private ISystem<float>[] Systems { get; }
    
    /// <summary>
    /// The counted intervall.
    /// </summary>
    public float Intervall { get; set; }
    
    /// <summary>
    /// The time that triggers the execution once <see cref="Intervall"/> reached it.
    /// </summary>
    public float TriggerSec { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeforeUpdate(in float t)
    {
        Intervall += t;
        
        if (Intervall <= TriggerSec) return;
        for (var index = 0; index < Systems.Length; index++)
        {
            var system = Systems[index];
            system.BeforeUpdate(t);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(in float t)
    {
        if (Intervall <= TriggerSec) return;
        for (var index = 0; index < Systems.Length; index++)
        {
            var system = Systems[index];
            system.Update(t);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AfterUpdate(in float t)
    {
        if (Intervall <= TriggerSec) return;
        Intervall = 0;
        
        for (var index = 0; index < Systems.Length; index++)
        {
            var system = Systems[index];
            system.AfterUpdate(t);
        }
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        for (var index = 0; index < Systems.Length; index++)
        {
            var system = Systems[index];
            system.Dispose();
        }
    }
}

/// <summary>
///     Just a little util system to debug various stats every tick
/// </summary>
public sealed class DebugSystem : BaseSystem<World, float>
{
    private readonly World _world;
    private ServerNetwork _network;

    private static readonly QueryDescription Chunks = new QueryDescription().WithAll<Chunk>().WithNone<Prefab>();
    private static readonly QueryDescription Mobs = new QueryDescription().WithAll<Mob>().WithNone<Prefab>();

    private static readonly QueryDescription Players = new QueryDescription().WithAll<Character, LogedIn>().WithNone<Prefab>();
    private static readonly QueryDescription Resources = new QueryDescription().WithAll<Resource>().WithNone<Prefab>();
    private static readonly QueryDescription Structures = new QueryDescription().WithAll<Structure>().WithNone<Prefab>();

    public DebugSystem(World world, ServerNetwork network) : base(world)
    {
        this._world = world;
        this._network = network;
    }

    public override void Update(in float t)
    {
        base.Update(in t);
        
        Program.Logger.ZLogDebug("[Monitor] QuadTree size : {0}", _world.GetTree().Count);
        Program.Logger.ZLogDebug("[Monitor] Chunks : {0}", _world.CountEntities(in Chunks));
        Program.Logger.ZLogDebug("[Monitor] Resources : {0}", _world.CountEntities(in Resources));
        Program.Logger.ZLogDebug("[Monitor] Structures : {0}", _world.CountEntities(in Structures));
        Program.Logger.ZLogDebug("[Monitor] Mobs : {0}", _world.CountEntities(in Mobs));
        Program.Logger.ZLogDebug("[Monitor] Active Players : {0}", _world.CountEntities(in Players));
        Program.Logger.ZLogDebug("[Monitor] Total entities : {0}", _world.Size);

        // Log all players
        Program.Logger.ZLogDebug("[Monitor] Playerlist...");
        _world.Query(in Players, (in Entity entity, ref Character character) =>
        {
            Program.Logger.ZLogDebug("[Monitor] Player : {0}/{1}", character.Name, entity);
        });
    }
}