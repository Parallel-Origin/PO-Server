using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.ECS;
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
///     A group which runs all activity related systems like chopping, crafting, building and much more
/// </summary>
public sealed class ActivityGroup : Arch.System.Group<float>
{
    public ActivityGroup(World world, EntityPrototyperHierarchy prototyperHierarchy) : base(
        new IntervallGroup(1.0f, new ChopSystem(world)),
        new BuildSystem(world, prototyperHierarchy),
        new PickupSystem(world)
    )
    {
    }
}

/// <summary>
///     The <see cref="ChopSystem"/> class
///     manages the chop process of trees, plants and rocks. 
/// </summary>
public sealed partial class ChopSystem : BaseSystem<World,float>
{
    public ChopSystem(World world) : base(world)
    {
    }
    
    /// <summary>
    ///     Chops the <see cref="ParallelOrigin.Core.ECS.Components.Interactions.Chop.Target"/> step by step and fires <see cref="Damage"/>-<see cref="Entity"/>s.
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="transform">Its <see cref="NetworkTransform"/>.</param>
    /// <param name="boxCollider">Its <see cref="BoxCollider"/>.</param>
    /// <param name="chop">Its <see cref="Chop"/>.</param>
    [Query]
    [None(typeof(Dead), typeof(Prefab))]
    private void Chop(in Entity en, ref NetworkTransform transform, ref BoxCollider boxCollider, ref Chop chop)
    {
        // Build entity by increasing health each step... 
        ref var targetCollider = ref chop.Target.Get<BoxCollider>();
        ref var targetTransform = ref chop.Target.Get<NetworkTransform>();
        
        var builderRect = new RectangleF((float)transform.Pos.X - boxCollider.Width / 2, (float)transform.Pos.Y - boxCollider.Height / 2, boxCollider.Width, boxCollider.Height);
        var structureRect = new RectangleF((float)targetTransform.Pos.X - targetCollider.Width / 2, (float)targetTransform.Pos.Y - targetCollider.Height / 2, targetCollider.Width, targetCollider.Height);

        // Only proceed when colliding
        if (!builderRect.IntersectsWith(structureRect)) return;
        
        // Spawn damage command
        ref var physicalDamage = ref en.Get<PhysicalDamage>();
        var damage = new Damage { Receiver = chop.Target, Sender = en };
        
        var cb = World.Record(); 
        var @event = cb.Create(new ComponentType[]{ typeof(Damage), typeof(PhysicalDamage), typeof(Chop)});
        cb.Set(in @event, damage);
        cb.Set(in @event, physicalDamage);
    }
}


public sealed partial class BuildSystem : BaseSystem<World,float>
{
    private readonly EntityPrototyperHierarchy _prototyperHierarchy;
    
    public BuildSystem(World world, EntityPrototyperHierarchy prototyperHierarchy) : base(world)
    {
        _prototyperHierarchy = prototyperHierarchy;
    }
    
    /// <summary>
    /// Prepares the build mechanism, spawns in the structure once in distance and withdraws the resources.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="identity">The <see cref="Identity"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    /// <param name="build">The <see cref="Build"/> cmp.</param>
    [Query]
    [None(typeof(Dead), typeof(Prefab))]
    private void BuildSetup(in Entity entity, ref Identity identity, ref NetworkTransform transform, ref Build build)
    {
        // Check if we reached the target
        var distance = Math.Sqrt(Math.Pow(build.Position.X - transform.Pos.X, 2) + Math.Pow(build.Position.Y - transform.Pos.Y, 2));
        if (!(distance <= build.Distance)) return;

        // Make sure structure to build was spawned in once the target was reached
        if (build.Entity.IsAlive()) return;
        
        // Spawn in structure, but disable it
        var structureEntity = _prototyperHierarchy.Clone(build.Type);
        ref var structure = ref structureEntity.Get<Structure>();
        ref var structureTransform = ref structureEntity.Get<NetworkTransform>();
        ref var health = ref structureEntity.Get<Health>();

        structure.Owner = new EntityLink(entity, identity.Id);
        health.CurrentHealth = 1;
        structureTransform.Pos = build.Position;
            
        // Substract items from inventory
        for (var index = 0; index < build.Ingredients.Length; index++)
        {
            ref var ingredient = ref build.Ingredients[index];
            InventoryCommandSystem.Add(new InventoryCommand(ingredient.Type, ingredient.Amount, in entity, InventoryOperation.SUBSTRACT));
        }
    }
    
    /// <summary>
    /// Builds the referenced building step by step. 
    /// </summary>
    /// <param name="state">The delta time.</param>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    /// <param name="boxCollider">The <see cref="BoxCollider"/>.</param>
    /// <param name="movement">The entities <see cref="Movement"/>.</param>
    /// <param name="build">The <see cref="Build"/>.</param>
    [Query]
    [None(typeof(Dead), typeof(Prefab))]
    private void Build(float state, in Entity entity, ref NetworkTransform transform, ref BoxCollider boxCollider, ref Movement movement, ref Build build)
    {
        // Force entity to build position
        movement.Target = build.Position;

        // Build entity by increasing health each step... 
        ref var structureHealth = ref build.Entity.Get<Health>();
        ref var structureCollider = ref build.Entity.Get<BoxCollider>();
        ref var structureTransform = ref build.Entity.Get<NetworkTransform>();
        
        var builderRect = new RectangleF((float)transform.Pos.X - boxCollider.Width / 2, (float)transform.Pos.Y - boxCollider.Height / 2, boxCollider.Width, boxCollider.Height);
        var structureRect = new RectangleF((float)structureTransform.Pos.X - structureCollider.Width / 2, (float)structureTransform.Pos.Y - structureCollider.Height / 2, structureCollider.Width, structureCollider.Height);

        // Only proceed when colliding
        if (!builderRect.IntersectsWith(structureRect)) return;
        
        structureHealth.CurrentHealth += state * build.Duration;
        
        var cb = World.Record();
        cb.Add(in build.Entity, new DirtyNetworkHealth());

        if (structureHealth.CurrentHealth < 100.0f) return;

        // Remove the build component to stop building that structure. 
        cb.Remove<Build>(in entity);
    }
}

/// <summary>
///     A system which iterates over <see cref="Collisions" /> which are currently <see cref="Chop" />ping to decrease the health of the entity being chopped.
/// </summary>
public sealed partial class PickupSystem : BaseSystem<World,float>
{
    public PickupSystem(World world) : base(world)
    {
    }
    
    [Query]
    [None(typeof(Dead), typeof(Prefab))]
    private void Update(in Entity en, ref NetworkTransform transform, ref BoxCollider boxCollider, ref Pickup pickup)
    {
        // Build entity by increasing health each step... 
        ref var targetCollider = ref pickup.Target.Get<BoxCollider>();
        ref var targetTransform = ref pickup.Target.Get<NetworkTransform>();
        ref var item = ref pickup.Target.Get<Item>();
        ref var onGround = ref pickup.Target.Get<OnGround>();
        
        var builderRect = new RectangleF((float)transform.Pos.X - boxCollider.Width / 2, (float)transform.Pos.Y - boxCollider.Height / 2, boxCollider.Width, boxCollider.Height);
        var structureRect = new RectangleF((float)targetTransform.Pos.X - targetCollider.Width / 2, (float)targetTransform.Pos.Y - targetCollider.Height / 2, targetCollider.Width, targetCollider.Height);

        // Only proceed when colliding
        if (!builderRect.IntersectsWith(structureRect)) return;
        
        // Spawn inventory command
        InventoryCommandSystem.Add(new InventoryCommand(onGround.PickupType, item.Amount, in en, InventoryOperation.ADD));
        
        var cb = World.Record();
        cb.Add(in pickup.Target, new Destroy());
        cb.Remove<Pickup>(in en);
  
        Program.Logger.ZLogInformation(Logs.Action, "Picked up", $"Received {item.Amount}/{onGround.PickupType}", pickup.Target, en);
    }
}