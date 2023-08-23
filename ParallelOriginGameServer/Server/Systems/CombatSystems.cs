using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using Arch.System.SourceGenerator;
using Collections.Pooled;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Persistence;
using ParallelOriginGameServer.Server.ThirdParty;
using ZLogger;
using Character = ParallelOrigin.Core.ECS.Components.Character;
using Range = ParallelOrigin.Core.ECS.Components.Combat.Range;

namespace ParallelOriginGameServer.Server.Systems;


/// <summary>
///     A system which runs combat related systems like health, combat flow, damage calculation and much more
/// </summary>
public sealed class CombatGroup : Arch.System.Group<float>
{
    public CombatGroup(World world) : base(
        
        new AttackSystem(world),
        new DamageSystem(world)
        /*
        new CancelAttackSystem(world, parallelRunner),
        new AttackSystem(world, parallelRunner),
        new PhysicalDamageCalculationSystem(world, parallelRunner),
        
        new PhysicalDamageSystem(world, parallelRunner), // Substract damage from health
        new HealSystem(world, parallelRunner), // Adds to health
        
        new OnChopGiveItems(world, parallelRunner),
        new OnDeathDropItems(world, parallelRunner, prototyperHierarchy), // Drop items on the group when entity was killed... 
        
        new KillSystem(world, parallelRunner), // marks killed entities as dead
        new RespawnDeadSystem(world, parallelRunner), // Respawns entity on natural way 
        new DestroyDeadSystem(world, parallelRunner)*/
    )
    {
    }
}

/// <summary>
///     The <see cref="AttackSystem"/> class
///     manages the attack/combat logic of entities and fires damage events. 
/// </summary>
public sealed partial class AttackSystem : BaseSystem<World,float>
{
    public AttackSystem(World world) : base(world)
    {
    }
    
    /// <summary>
    ///     Cancels ongoing attacks once an <see cref="Entity"/> is out of reach.
    /// </summary>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    /// <param name="range">The <see cref="Range"/>.</param>
    /// <param name="inCombat">The <see cref="InCombat"/>.</param>
    [Query]
    [None<Dead, Prefab>]
    private void CancelAttack(ref NetworkTransform transform, ref Range range, ref InCombat inCombat)
    {
        // To skip entities non in combat 
        var entities = inCombat.Entities.Get();
        if (entities.Count <= 0) return;

        var pool = ObjectPool<PooledSet<Entity>>.Get();
        foreach (var defenderEntity in entities)
        {
            // Defender outside range, remove from attack list 
            ref var health = ref defenderEntity.Get<Health>();
            ref var entityTransform = ref defenderEntity.Get<NetworkTransform>();

            var distance = Math.Sqrt(Math.Pow(transform.Pos.X - entityTransform.Pos.X, 2) + Math.Pow(transform.Pos.Y - entityTransform.Pos.Y, 2));
            if (health.IsDead() || distance >= range.Value.Value)
            {
                pool.Add(defenderEntity);   
            }
        }

        entities.ExceptWith(pool);
        pool.Dispose();
        ObjectPool<PooledSet<Entity>>.Return(pool);
    }
    
    /// <summary>
    ///     Controlls the attack flow by firing damage event-entities once an entity attack and marks it as attacking.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="attackSpeed">Its <see cref="AttackSpeed"/>.</param>
    /// <param name="inCombat">The <see cref="InCombat"/>.</param>
    /// <param name="physicalDamage">Its <see cref="PhysicalDamage"/>.</param>
    [Query]
    [None<Dead, Prefab>]
    private void Attack([Data] float state, in Entity en, ref AttackSpeed attackSpeed, ref InCombat inCombat, ref PhysicalDamage physicalDamage)
    {
        // Avoid entities which do not attack anyone curently.
        var entities = inCombat.Entities.Get();
        if (entities.Count <= 0) return;

        // Intervall
        inCombat.Intervall += state;
        if (inCombat.Intervall <= attackSpeed.Value.Value) return;

        inCombat.Intervall = 0; // Reset
        var record = World.Record();
        
        // Attack 
        foreach (var defenderEntity in entities)
        {
            var damage = new Damage { Receiver = defenderEntity, Sender = en };
            var damageEntity = record.Create(new ComponentType[] { typeof(Damage), typeof(PhysicalDamage)});
            record.Set(damageEntity, damage);
            record.Set(damageEntity, physicalDamage);
        }

        // Mark entity as moved 
        if (!en.Has<Attacks>())
        {
            record.Add<Attacks>(en);
        }
    }
    
    /// <summary>
    ///     Calculates the physical damage.
    /// </summary>
    /// <param name="damage">The <see cref="Damage"/>.</param>
    /// <param name="physicalDamage">Its <see cref="PhysicalDamage"/>.</param>
    [Query]
    [None<Prefab>]
    private void CalculatePhysicalDamage(ref Damage damage, ref PhysicalDamage physicalDamage)
    {
        ref var receiver = ref damage.Receiver;
        var resistence = receiver.GetOrDefault<PhysicalResistence>();

        var damange = physicalDamage.Value.Value * (100 / (100 + resistence.Value.Value));
        physicalDamage.Value.Value = damange;
    }
    
    /// <summary>
    ///     Deals the physical damage to its target.
    /// </summary>
    /// <param name="damage">The <see cref="Damage"/>.</param>
    /// <param name="physicalDamage">The <see cref="PhysicalDamage"/>.</param>
    [Query]
    [None<Prefab>]
    private void DealPhysicalDamage(ref Damage damage, ref PhysicalDamage physicalDamage)
    {
        ref var receiver = ref damage.Receiver;
        if (!receiver.IsAlive()) return;

        ref var health = ref receiver.Get<Health>();
        if (receiver.Has<Character>() && health.CurrentHealth - physicalDamage.Value.Value <= 0)
        {
            return;
        }

        health.CurrentHealth -= physicalDamage.Value.Value;
        damage.Killed = health.CurrentHealth <= 0;

        // Update network health 
        var record = World.Record();
        record.Add<DirtyNetworkHealth>(in receiver);

        Program.Logger.ZLogInformation(Logs.Damage, "Physical", physicalDamage.Value.Value, damage.Receiver, damage.Sender);
        Program.Logger.ZLogInformation(Logs.Health, damage.Receiver, health.CurrentHealth);
        if (damage.Killed)
        {
            Program.Logger.ZLogInformation(Logs.Killed, damage.Receiver, damage.Sender);
        }
    }
}

/// <summary>
///     The <see cref="DamageSystem"/> class
///     manages the damage dealing and healing from damage aswell as the death and respawning of entities. 
/// </summary>
public sealed partial class DamageSystem : BaseSystem<World, float>
{
    public DamageSystem(World world) : base(world)
    {
    }
    
    /// <summary>
    ///     Handles a <see cref="Damage"/>-<see cref="Entity"/> and killts its <see cref="Damage.Receiver"/> when <see cref="Damage.Killed"/> is set and
    ///     it has <see cref="Chop"/> attached.
    /// </summary>
    /// <param name="damage">The <see cref="Damage"/>.</param>
    [Query]
    [All<Chop>, None<Prefab>]
    private void ChoppedGiveItems(ref Damage damage)
    {
        if (!damage.Killed) return;

        // Remove chop to finish this action and clear up
        var record = World.Record();
        record.Remove<Chop>(in damage.Sender);

        // Add/Update players inventory
        ref var chopLoot = ref damage.Receiver.Get<Loot>();
        var drop = chopLoot.LootHandle.Get().Get();
        InventoryCommandSystem.Add(new InventoryCommand(drop.Type, drop.Amount, in damage.Sender, InventoryOperation.ADD));

        Program.Logger.ZLogInformation(Logs.Action, "Chopped", $"Received {drop.Amount}/{drop.Type}", damage.Sender, damage.Receiver);
    }
    
    /// <summary>
    ///     Handles a <see cref="Damage"/>-<see cref="Entity"/> and kills its <see cref="Damage.Receiver"/> when <see cref="Damage.Killed"/> is set. 
    /// </summary>
    /// <param name="damage">The <see cref="Damage"/>.</param>
    [Query]
    [None<Prefab>]
    private void KillEntity(ref Damage damage)
    {
        // If entity is dead... flag for being destroyed next frame
        if (!damage.Killed) return;

        ref var receiver = ref damage.Receiver;
        var record = World.Record();
        record.Add<Dead>(in receiver);
    }
    
    
    /// <summary>
    ///     Adds <see cref="Destroy"/> to a <see cref="Dead"/>-<see cref="Entity"/> to despawn it. 
    /// </summary>
    /// <param name="en"></param>
    [Query]
    [All<Dead>, None<Destroy, Prefab, OnDeathRespawn>]
    private void DestroyDeadEntity(in Entity en)
    {
        var record = World.Record();
        record.Add<Destroy>(in en);
    }
}

/*



/// <summary>
///     A system iterating over <see cref="Heal" /> commands to heal the referenced entity by a certain amount
/// </summary>
[Without(typeof(Prefab))]
public sealed partial class HealSystem : AEntitySetSystem<float>
{
    [Update]
    private void Update(ref Heal heal)
    {
        ref var receiver = ref heal.receiver;
        ref var health = ref receiver.Get<Health>();

        health.currentHealth += heal.value;
        if (health.currentHealth >= health.maxHealth)
            health.maxHealth = health.currentHealth;
    }
}


/// <summary>
///     A system which iterates over <see cref="Damage" /> events, checks if the entity was killed and a resource and grants the player items for the kill.
/// </summary>
[Without(typeof(Prefab))]
public sealed partial class OnDeathDropItems : AEntitySetSystem<float>
{

    [ConstructorParameter] private EntityPrototyperHierarchy _prototyperHierarchy;

    [Update]
    private void Update(ref Damage damage)
    {
        if (!damage.killed) return;
        if (!damage.receiver.Has<Drops>()) return;

        // Get components
        ref var transform = ref damage.receiver.Get<NetworkTransform>();
        ref var chopLoot = ref damage.receiver.Get<Drops>();

        // Get loot
        var loot = new List<WeightedItem>();
        chopLoot.drops.Get(loot);

        // Drop loot
        for (var index = 0; index < loot.Count; index++)
        {
            // Onto the floor
            var lootItem = loot[index];
            var entity = _prototyperHierarchy.Clone(lootItem.type);
            entity.Set(transform);

            Program.Logger.ZLogInformation(Logs.ACTION, "Dropped", $"{lootItem.amount}/{lootItem.type}", damage.sender, damage.receiver);
        }
    }
}

/// <summary>
///     A system which respawns a <see cref="Dead" /> entity if it has the <see cref="OnDeathRespawn" /> component.
///     Resets health upon respawn
/// </summary>
[With(typeof(Dead))]
[Without(typeof(Prefab))]
public sealed partial class RespawnDeadSystem : AEntitySetSystem<float>
{
    [Update]
    private void Update(float delta, in Entity en, ref Health health, ref OnDeathRespawn respawn)
    {
        respawn.intervall += delta;
        if (respawn.intervall < respawn.timeInMs) return;

        respawn.intervall = 0;
        health.currentHealth = health.maxHealth;

        var record = World.Record(in en);
        record.Disable<Dead>();
    }
}
*/