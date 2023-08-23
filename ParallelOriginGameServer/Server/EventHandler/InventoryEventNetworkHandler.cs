using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Events;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server;

public static class InventoryEventNetworkHandler
{
    /// <summary>
    ///     Listens for <see cref="ItemAddedEvent"/>s and forwards the changes to the local client.
    /// </summary>
    /// <param name="cmd"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnAddedSendItem(ItemAddedEvent cmd)
    {
        var network = ServiceLocator.Get<ServerNetwork>();
        ref var identity = ref cmd.Entity.Get<Identity>();
        ref var character = ref cmd.Entity.Get<Character>();
        var peer = character.Peer.Get();
        
        // Send new item and create it on the client
        var entity = cmd.Item;
        ref var entityId = ref entity.Get<Identity>();
        ref var item = ref entity.Get<Item>();
        ref var mesh = ref entity.Get<Mesh>();
        ref var sprite = ref entity.Get<Sprite>();

        // Construct command
        var command = new EntityCommand<Identity, Item, Mesh, Sprite>();
        command.Command = new EntityCommand { Id = entityId.Id, Type = entityId.Type, Opcode = EntityOpCode.Create };
        command.T1Component = entityId;
        command.T2Component = item;
        command.T3Component = mesh;
        command.T4Component = sprite;
        network.Send(peer, ref command);
        
        // Send over the list changes of the inventory
        var collectionUpdate = new CollectionCommand<Identity, Inventory, EntityLink>(ref identity, 1);
        collectionUpdate[State.Added, 0] = new EntityLink(entity, entityId.Id);
        network.Send(peer, ref collectionUpdate);
    }

    /// <summary>
    ///     Listens for <see cref="ItemUpdatedEvent"/>s and forwards the changes to the local client.
    /// </summary>
    /// <param name="event"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnUpdateSendItem(ItemUpdatedEvent @event)
    {
        var network = ServiceLocator.Get<ServerNetwork>();
        ref var identity = ref @event.Entity.Get<Identity>();
        ref var character = ref @event.Entity.Get<Character>();
        ref var inventory = ref @event.Entity.Get<Inventory>();
        var peer = character.Peer.Get();
        
        var entity = @event.Item;
        ref var entityId = ref entity.Get<Identity>();
        ref var item = ref entity.Get<Item>();

        // Construct command
        var command = new EntityCommand<Item>();
        command.Command = new EntityCommand { Id = entityId.Id, Type = entityId.Type, Opcode = EntityOpCode.Set };
        command.T1Component = item;
        network.Send(peer, ref command);
        
        // Send over the list changes of the inventory
        var collectionUpdate = new CollectionCommand<Identity, Inventory, EntityLink>(ref identity, 1);
        collectionUpdate[State.Updated, 0] = new EntityLink(entity, entityId.Id);
        network.Send(peer, ref collectionUpdate);
    }

    /// <summary>
    ///     Listens for <see cref="ItemUpdatedEvent"/>s and forwards the changes to the local client.
    /// </summary>
    /// <param name="event"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnRemoveSendItem(ItemRemovedEvent @event)
    {
        var network = ServiceLocator.Get<ServerNetwork>();
        ref var identity = ref @event.Entity.Get<Identity>();
        ref var character = ref @event.Entity.Get<Character>();
        var peer = character.Peer.Get();
        
        // Fill batch
        var entity = @event.Item;
        ref var entityId = ref entity.Get<Identity>();

        // Construct command
        var command = new EntityCommand { Id = entityId.Id, Type = entityId.Type, Opcode = EntityOpCode.Delete };
        network.Send(peer, ref command);
        
        // Send over the list changes of the inventory
        var collectionUpdate = new CollectionCommand<Identity, Inventory, EntityLink>(ref identity, 1);
        collectionUpdate[State.Removed, 0] = new EntityLink(entity, entityId.Id);     
        network.Send(peer, ref collectionUpdate);
    }
}