using System;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Extensions;
using ZLogger;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which controlls all systems which are either reactive systems or one frame systems.
/// </summary>
public sealed class ReactiveGroup : Arch.System.Group<float>
{
    public ReactiveGroup(World world) : base(
        // Reactive
        new CreatedReactiveSystem(world),
        new DirtyOneFrameDisableSystem(world),
        new SpawnOneFrameSystem(world),
        new MovingOneFrameDisableSystem(world),
        new DirtyTransformOneFrameDisableSystem(world),
        new DirtyHealthOneFrameDisableSystem(world),
        new ClickedOneFrameComponentDisableAndDisableSystem(world),
        new AttacksOneFameDisableSystem(world),
        new OneFrameDestroySystem<Damage>(world)
    )
    {
    }
}

/// <summary>
///     An reactive system which adds <see cref="TAdded" /> once the <see cref="TComponent" /> was added and adds <see cref="TRemoved" /> once it was removed.
///     Stores the <see cref="TComponent" /> in an regular <see cref="State" /> for disposal operations and similar.
/// </summary>
/// <typeparam name="TComponent"></typeparam>
/// <typeparam name="TAdded"></typeparam>
/// <typeparam name="TRemoved"></typeparam>
public abstract class ReactiveSystem<TComponent, TAdded, TRemoved> : BaseSystem<World,float> where TAdded : unmanaged where TRemoved : unmanaged
{
    protected unsafe ReactiveSystem(World world, bool copy) : base(world)
    {
        StaticWorld = world;
        Copy = copy;

        var cmp = new ComponentType(ComponentRegistry.Size, typeof(State), sizeof(State), false);
        ComponentRegistry.Add(cmp);
        
        _entitiesWithComponentAndState = new QueryDescription().WithAll<TComponent,State>().WithNone<Prefab>();
        _entitiesWithComponentOnly =  new QueryDescription().WithAll<TComponent>().WithNone<TAdded,TRemoved,State,Prefab>();
        _entitiesWithAdded =  new QueryDescription().WithAll<TComponent, State, TAdded>().WithNone<TRemoved, Prefab>();
        _entitiesWithoutComponent = new QueryDescription().WithAll<State>().WithNone<TComponent,TAdded,TRemoved,Prefab>();
        _entitiesWithRemoved = new QueryDescription().WithAll<State,TRemoved>().WithNone<TComponent,TAdded,Prefab>();
    }

    private static World StaticWorld { get; set; }
    private bool Copy { get; set; }

    private readonly QueryDescription _entitiesWithComponentAndState;
    private readonly QueryDescription _entitiesWithComponentOnly;
    private readonly QueryDescription _entitiesWithAdded;
    private readonly QueryDescription _entitiesWithoutComponent;
    private readonly QueryDescription _entitiesWithRemoved;

    public override void Update(in float t)
    {
        base.Update(in t);

        // Originally this system buffered all changes, not sure if this makes sense anymore= 
        
        // Mark entity with state and tadded
        World.Query(in _entitiesWithComponentOnly, static (in Entity entity, ref TComponent component) =>
        {
            var cb = StaticWorld.Record();
            cb.Add(in entity, new State { });
            cb.Add(in entity, new TAdded { });
        });

        // Remove entities with TAdded
        World.Query(in _entitiesWithAdded, static (in Entity entity) =>
        {
            var cb = StaticWorld.Record();
            cb.Remove<TAdded>(in entity);
        });


        // Add Removed to entities without the component 
        World.Query(in _entitiesWithoutComponent, static (in Entity entity) =>
        {
            var cb = StaticWorld.Record();
            cb.Add(in entity, new TRemoved());
        });

        // Remove removed and state
        World.Query(in _entitiesWithRemoved, static (in Entity entity) =>
        {
            var cb = StaticWorld.Record();
            cb.Remove<State>(in entity);
            cb.Remove<TRemoved>(in entity);
        });

        if (!Copy) return;
        
        // Remove removed and state
        World.Query(in _entitiesWithComponentAndState, static (ref TComponent component, ref State state) =>
        {
            //state.component = component;
        });
    }

    /// <summary>
    ///     Represents the state which tracks the entity.
    /// </summary>
    public struct State
    {
    }
}

/// <summary>
///     An system which removed one component at the end of the frame.
///     Great for markers
/// </summary>
/// <typeparam name="TComponent"></typeparam>
public abstract partial class OneFrameSystem<TComponent> : BaseSystem<World,float> where TComponent : unmanaged
{
    protected unsafe OneFrameSystem(World world) : base(world)
    {
        var cmp = new ComponentType(ComponentRegistry.Size + 1, typeof(TComponent), sizeof(TComponent), false);
        ComponentRegistry.Add(cmp);
    }

    public override void Update(in float t)
    {
        base.Update(in t);

        var queryDesc = new QueryDescription().WithAll<TComponent>().WithNone<Prefab>();
        World.Remove<TComponent>(in queryDesc);
    }
}

/// <summary>
///     The <see cref="OneFrameDestroySystem{TComponent}"/>
///     is a system that destroys all <see cref="Entity"/>s with the passed component attached.
/// </summary>
/// <typeparam name="TComponent">The component.</typeparam>
public partial class OneFrameDestroySystem<TComponent> : BaseSystem<World,float> where TComponent : unmanaged
{
    public unsafe OneFrameDestroySystem(World world) : base(world)
    {
        var cmp = new ComponentType(ComponentRegistry.Size + 1, typeof(TComponent), sizeof(TComponent), false);
        ComponentRegistry.Add(cmp);
    }

    public override void Update(in float t)
    {
        base.Update(in t);

        var queryDesc = new QueryDescription().WithAll<TComponent>().WithNone<Prefab>();
        World.Destroy(in queryDesc);
    }
}

/// <summary>
///     An system which disables one component at the end of the frame.
///     Great for markers
/// </summary>
/// <typeparam name="TComponent"></typeparam>
public abstract partial class OneFrameDisableSystem<TComponent> : BaseSystem<World,float> where TComponent : unmanaged
{
    static unsafe OneFrameDisableSystem()
    {
        var cmp = new ComponentType(ComponentRegistry.Size + 1, typeof(Toggle<TComponent>), sizeof(Toggle<TComponent>), false);
        ComponentRegistry.Add(cmp);
    }

    protected OneFrameDisableSystem(World world) : base(world)
    {
 
    }
    
    [Query]
    [None(typeof(Prefab))]
    private void Update(ref Toggle<TComponent> component)
    {
        component.Enabled = false;
    }
}

/// <summary>
///     An system which disables one component at the end of the frame.
///     Does not make use of a buffer, it disables the component directly and calls its disposal mechanism.
///     Great for markers
/// </summary>
/// <typeparam name="TComponent"></typeparam>
public abstract partial class OneFrameComponentDisposeSystem<TComponent> : BaseSystem<World,float> where TComponent : struct, IDisposable
{
    protected OneFrameComponentDisposeSystem(World world) : base(world)
    {
    }
    
    [Query]
    [None(typeof(Prefab))]
    private void Update(ref TComponent component)
    {
        component.Dispose();
    }
}

/// <summary>
///     An system which disables one component at the end of the frame.
///     Does not make use of a buffer, it disables the component directly and calls its disposal mechanism.
///     Great for markers
/// </summary>
/// <typeparam name="TComponent"></typeparam>
public abstract partial class OneFrameComponentDisposeAndDisableSystem<TComponent> : BaseSystem<World,float> where TComponent : struct, IDisposable
{
    private readonly QueryDescription _query = new QueryDescription().WithAll<Toggle<TComponent>>().WithNone<Prefab>();
    protected OneFrameComponentDisposeAndDisableSystem(World world) : base(world)
    {
    }

    public override void Update(in float t)
    {
        base.Update(in t);
        World.Query(in _query, (ref Toggle<TComponent> cmp) =>
        {
            cmp.Component.Dispose();
            cmp.Enabled = false;
        });
    }
}

/// <summary>
///     Adds an <see cref="Created" /> to each entity which was marked with an <see cref="Identity" /> which lasts about one frame.
/// </summary>
public class CreatedReactiveSystem : ReactiveSystem<Identity, Created, Destroyed>
{
    public CreatedReactiveSystem(World world) : base(world, false)
    {
    }
}

/// <summary>
///     Disables <see cref="Dirty" /> marked entities after exactly one frame.
/// </summary>
public class DirtyOneFrameDisableSystem : OneFrameDisableSystem<Dirty>
{
    public DirtyOneFrameDisableSystem(World world) : base(world)
    {
    }
    //public DirtyOneFrameDisableSystem(World world, IParallelRunner runner) : base(world, runner) { }
}

/// <summary>
///     A system which removes a <see cref="Spawn" /> at the end of the frame
/// </summary>
public sealed class SpawnOneFrameSystem : OneFrameSystem<Spawn>
{
    public SpawnOneFrameSystem(World world) : base(world)
    {
    }
    //public SpawnOneFrameSystem(World world, IParallelRunner runner) : base(world, runner) { }
}

/// <summary>
///     A system which disables <see cref="Moving" /> after exactly one frame.
///     Disabling makes more sense here because we make entities move a lot, so its better to reuse i.
/// </summary>
public sealed class MovingOneFrameDisableSystem : OneFrameDisableSystem<Moving>
{
    public MovingOneFrameDisableSystem(World world) : base(world)
    {
    }
    //public MovingOneFrameDisableSystem(World world, IParallelRunner runner) : base(world, runner) { }
}

/// <summary>
///     A system which disables <see cref="DirtyNetworkTransform" /> after exactly one frame.
///     Disabling makes more sense here because we make entities move a lot, so its better to reuse i.
///     ! Actually needs to be placed inside the network system group/loop... Otherwhise it will remove the component like every frame which is bad when the network group only runs every few secs !
/// </summary>
public sealed class DirtyTransformOneFrameDisableSystem : OneFrameDisableSystem<DirtyNetworkTransform>
{
    public DirtyTransformOneFrameDisableSystem(World world) : base(world)
    {
    }
    //public DirtyTransformOneFrameDisableSystem(World world, IParallelRunner runner) : base(world, runner) { }
}

/// <summary>
///     A system which disables <see cref="DirtyNetworkHealth" /> after exactly one frame.
///     Disabling makes more sense here because we make entities move a lot, so its better to reuse i.
///     ! Actually needs to be placed inside the network system group/loop... Otherwhise it will remove the component like every frame which is bad when the network group only runs every few secs !
/// </summary>
public sealed class DirtyHealthOneFrameDisableSystem : OneFrameDisableSystem<DirtyNetworkHealth>
{
    public DirtyHealthOneFrameDisableSystem(World world) : base(world)
    {
    }
    //public DirtyTransformOneFrameDisableSystem(World world, IParallelRunner runner) : base(world, runner) { }
}

/// <summary>
///     A system which disables a <see cref="Clicked" /> after exactly one frame. Also clears it.
/// </summary>
public sealed class ClickedOneFrameComponentDisableAndDisableSystem : OneFrameComponentDisposeAndDisableSystem<Clicked>
{
    public ClickedOneFrameComponentDisableAndDisableSystem(World world) : base(world)
    {
    }
    //public ClickedOneFrameDisableSystem(World world, IParallelRunner runner) : base(world, runner) { }
}


public sealed class AttacksOneFameDisableSystem : OneFrameDisableSystem<Attacks>
{
    public AttacksOneFameDisableSystem(World world) : base(world)
    {
    }
}