using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using LiteNetLib;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.ECS.Events;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server;

/// <summary>
///     Handles all AOI related events and networks them to the client. 
/// </summary>
public static class AoiEventNetworkHandler
{
    
    /// <summary>
    ///     Handles <see cref="AoiEnteredEvent"/>s to send the networked entities to the client. 
    /// </summary>
    /// <param name="entered"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnAOIEnteredSendNetwork(ref AoiEnteredEvent entered)
    {
        var network = ServiceLocator.Get<ServerNetwork>();
        
        // Only interested in AOI events where a character received it
        ref readonly var charEntity = ref entered.Entity;
        if (!charEntity.Has<Character>() || charEntity.Has<Inactive>()) return;

        ref var character = ref charEntity.Get<Character>();
        var peer = character.Peer.Get();
        
        foreach (var entityRef in entered.Entities)
        {
            // Send players to other players
            var entity = (Entity)entityRef.Entity;
            if (entity.Has<Character>() && entity != charEntity)
            {
                ref var identity = ref entity.Get<Identity>();
                ref var characterr = ref entity.Get<Character>();
                ref var mesh = ref entity.Get<Mesh>();
                ref var health = ref entity.Get<Health>();
                ref var transform = ref entity.Get<NetworkTransform>();
                ref var rotation = ref entity.Get<NetworkRotation>();
                ref var movement = ref entity.Get<Movement>();

                var entityCommand = new EntityCommand<Identity, Character, Mesh, Health, NetworkTransform, NetworkRotation, Movement>();
                entityCommand.Command = new EntityCommand { Id = identity.Id, Type = identity.Type, Opcode = EntityOpCode.Create };
                entityCommand.T1Component = identity;
                entityCommand.T2Component = characterr;
                entityCommand.T3Component = mesh;
                entityCommand.T4Component = health;
                entityCommand.T5Component = transform;
                entityCommand.T6Component = rotation;
                entityCommand.T7Component = movement;

                network.Send(peer, ref entityCommand);
                continue;
            }
            
            // Send environment or meshes in total 
            if (entity.Has<Resource>() || entity.Has<Structure>() || entity.Has<Mob>())
            {
                ref var identity = ref entity.Get<Identity>();
                ref var mesh = ref entity.Get<Mesh>();
                ref var health = ref entity.Get<Health>();
                ref var transform = ref entity.Get<NetworkTransform>();
                ref var rotation = ref entity.Get<NetworkRotation>();

                var entityCommand = new EntityCommand<Identity, Mesh, Health, NetworkTransform, NetworkRotation>();
                entityCommand.Command = new EntityCommand { Id = identity.Id, Type = identity.Type, Opcode = EntityOpCode.Create };
                entityCommand.T1Component = identity;
                entityCommand.T2Component = mesh;
                entityCommand.T3Component = health;
                entityCommand.T4Component = transform;
                entityCommand.T5Component = rotation;

                network.Send(peer, ref entityCommand);
                continue;
            }
            
            // Send items on the ground
            if (entity.Has<Item>())
            {
                ref var identity = ref entity.Get<Identity>();
                ref var item = ref entity.Get<Item>();
                ref var mesh = ref entity.Get<Mesh>();
                ref var sprite = ref entity.Get<Sprite>();
                ref var transform = ref entity.Get<NetworkTransform>();

                var entityCommand = new EntityCommand<Identity, Item, Mesh, Sprite, NetworkTransform>();
                entityCommand.Command = new EntityCommand { Id = identity.Id, Type = identity.Type, Opcode = EntityOpCode.Create };
                entityCommand.T1Component = identity;
                entityCommand.T2Component = item;
                entityCommand.T3Component = mesh;
                entityCommand.T4Component = sprite;
                entityCommand.T5Component = transform;
                
                network.Send(peer, ref entityCommand);
            }
        }
    }
    
    /// <summary>
    ///     Receives a <see cref="AoiStayedEvent"/> and sends updates the transform to the client.
    /// </summary>
    /// <param name="stayed"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnAOIStayedSendTransform(ref AoiStayedEvent stayed)
    {
        var network = ServiceLocator.Get<ServerNetwork>();
        
        ref readonly var charEntity = ref stayed.Entity;
        if (!charEntity.Has<Character>() || charEntity.Has<Inactive>()) return;

        // Get component arreays
        ref var character = ref charEntity.Get<Character>();
        var peer = character.Peer.Get();
        foreach (var entityRef in stayed.Entities)
        {
            var entity = (Entity)entityRef;
            if (!entity.Has<DirtyNetworkTransform>()) continue;
            
            // Construct entity commands and put them into the batch 
            ref var entityIdentity = ref entity.Get<Identity>();
            ref var entityTransform = ref entity.Get<NetworkTransform>();
            ref var entityRotation = ref entity.Get<NetworkRotation>();

            var entityCommand = new EntityCommand<NetworkTransform, NetworkRotation>();
            entityCommand.Command = new EntityCommand { Id = entityIdentity.Id, Type = string.Empty, Opcode = EntityOpCode.Set };
            entityCommand.T1Component = entityTransform;
            entityCommand.T2Component = entityRotation;

            network.Send(peer, ref entityCommand, DeliveryMethod.Sequenced);   
        }
    }

    /// <summary>
    ///     Receives a <see cref="AoiStayedEvent"/> and sends the entities health.
    /// </summary>
    /// <param name="stayed"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnAOIStayedSendHealth(ref AoiStayedEvent stayed)
    {      
        var network = ServiceLocator.Get<ServerNetwork>();

        ref readonly var charEntity = ref stayed.Entity;
        if (!charEntity.Has<Character>() || charEntity.Has<Inactive>()) return;
        
        // Get component arreays
        ref var character = ref charEntity.Get<Character>();
        var peer = character.Peer.Get();
        foreach (var entityRef in stayed.Entities)
        {
            var entity = (Entity)entityRef.Entity;
            if (!entity.Has<DirtyNetworkHealth>()) continue;
            ref var entityIdentity = ref entity.Get<Identity>();
            ref var entityHealth = ref entity.Get<Health>();

            var entityCommand = new EntityCommand<Health>();
            entityCommand.Command = new EntityCommand { Id = entityIdentity.Id, Type = string.Empty, Opcode = EntityOpCode.Set };
            entityCommand.T1Component = entityHealth;

            network.Send(peer, ref entityCommand, DeliveryMethod.ReliableUnordered);
        }
    }
    
    /// <summary>
    ///     Receives a <see cref="AoiStayedEvent"/> and sends the entities animation state.
    /// </summary>
    /// <param name="stayed"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnAOIStayedSendAnimation(ref AoiStayedEvent stayed)
    {      
        var network = ServiceLocator.Get<ServerNetwork>();

        ref readonly var charEntity = ref stayed.Entity;
        if (!charEntity.Has<Character>() || charEntity.Has<Inactive>()) return;

        // Get component arrays
        ref var character = ref charEntity.Get<Character>();
        foreach (var entityRef in stayed.Entities)
        {
            var entity = (Entity)entityRef.Entity;
            if (!entity.Has<Animation>()) continue;
            
            ref var entityIdentity = ref entity.Get<Identity>();
            ref var entityAnimation = ref entity.Get<Animation>();
            
            // Get managed stuff
            var boolParams = entityAnimation.BoolParams.Get();
            var triggers = entityAnimation.Triggers.Get();

            // Skip entities not having any set bool params or triggers. 
            if (boolParams.Tracked.Count <= 0 && triggers.Count <= 0) continue;
            SendAnimationCommand(network, ref character, ref entityIdentity, ref entityAnimation);   
        }
    }
    
    /// <summary>
    ///     Receives <see cref="AoiLeftEvent"/>s and destroys the left entities on the client. 
    /// </summary>
    /// <param name="left"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnAOILeftSendNetwork(ref AoiLeftEvent left)
    {
        var network = ServiceLocator.Get<ServerNetwork>();
        
        ref readonly var charEntity = ref left.Entity;
        if (!charEntity.Has<Character>() || charEntity.Has<Inactive>()) return;

        ref var character = ref charEntity.Get<Character>();
        var peer = character.Peer.Get();
        foreach (var leaver in left.Entities)
        {
            // Create command, batch it
            var entityCommand = new EntityCommand { Id = leaver.UniqueId, Type = string.Empty, Opcode = EntityOpCode.Delete };
            network.Send(peer, ref entityCommand);   
        }
    }
    
    /// <summary>
    /// Creates an animation command which contains a list of changed bool params and set triggers and writes them to the network stream. 
    /// </summary>
    /// <param name="character"></param>
    /// <param name="entityAnimation"></param>
    /// <param name="entityIdentity"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SendAnimationCommand(ServerNetwork network, ref Character character, ref Identity entityIdentity, ref Animation entityAnimation)
    {
        
        var peer = character.Peer.Get();
        var boolParams = entityAnimation.BoolParams.Get();
        var triggers = entityAnimation.Triggers.Get();
        
        // Write changed bool params to the batch
        for (var index = 0; index < boolParams.Tracked.Count; index++) {

            var kvp = boolParams.Tracked[index];
            
            var boolParamsCommand = new AnimationParamCommand { EntityLink = new EntityLink(entityIdentity.Id), BoolName = kvp.Key, Activated = kvp.Val };
            var stateFull = new Statefull<AnimationParamCommand> { State = kvp.State, Item = boolParamsCommand }; ;
            network.Send(peer, ref stateFull, DeliveryMethod.ReliableUnordered);
        }
        
        // Write new triggers to the batch 
        for (var index = 0; index < triggers.Count; index++) {

            var kvp = triggers[index];

            var trigger = new AnimationTriggerCommand { EntityLink = new EntityLink(entityIdentity.Id), TriggerName = kvp };
            network.Send(peer, ref trigger, DeliveryMethod.ReliableUnordered);
        }
    }
}