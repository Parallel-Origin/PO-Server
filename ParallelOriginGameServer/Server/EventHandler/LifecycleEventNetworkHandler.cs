using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.ECS.Events;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server;

public static class LifecycleEventNetworkHandler
{

    /// <summary>
    ///     Receives an <see cref="LogedIn"/> event paired with an <see cref="Entity"/>.
    ///     Sends the loged in entity to its client for spawning it in and loading the map.
    /// </summary>
    /// <param name="event"></param>
    [Event]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnLoginSendCharacter(LoginEvent @event)
    {
        var prototyper = ServiceLocator.Get<EntityPrototyperHierarchy>();
        var network = ServiceLocator.Get<ServerNetwork>();
        var components = @event.Entity.Get<Character, Identity, Inventory, Mesh, NetworkTransform, Movement, Health, BuildRecipes>();
        
        // Send the whole character & map command :)
        var peer = components.t0.Peer.Get();
        var entityCommand = new EntityCommand<Identity, Character, Inventory, Mesh, NetworkTransform, Movement, Health, BuildRecipes>();
        entityCommand.Command = new EntityCommand { Id = components.t1.Id, Type =  components.t1.Type, Opcode = EntityOpCode.Create };
        entityCommand.T1Component = components.t1;
        entityCommand.T2Component = components.t0;
        entityCommand.T3Component = components.t2;
        entityCommand.T4Component = components.t3;
        entityCommand.T5Component = components.t4;
        entityCommand.T6Component = components.t5;
        entityCommand.T7Component = components.t6;
        entityCommand.T8Component = components.t7;

        var mapCommand = new MapCommand { Position = components.t4.Pos };
        network.Send(peer, ref mapCommand);
        network.Send(peer, ref entityCommand);

        // Send item entities 
        // Fill batch
        for (var index = 0; index < components.t2.Items.Count; index++)
        {
            var entity = (Entity)components.t2.Items[index].Entity;
            ref var entityId = ref entity.Get<Identity>();
            ref var item = ref entity.Get<Item>();
            ref var entityMesh = ref entity.Get<Mesh>();
            ref var sprite = ref entity.Get<Sprite>();

            // Construct command
            var command = new EntityCommand<Identity, Item, Mesh, Sprite>();
            command.Command = new EntityCommand { Id = entityId.Id, Type = entityId.Type, Opcode = EntityOpCode.Create };
            command.T1Component = entityId;
            command.T2Component = item;
            command.T3Component = entityMesh;
            command.T4Component = sprite;

            network.Send(peer, ref command);
        }


        // Send recipe entities to the client 
        // Fill batch
        for (var index = 0; index < components.t7.Recipes.Length; index++)
        {
            var type = components.t7.Recipes[index];
            var entity = prototyper.Get(type);
            ref var entityId = ref entity.Get<Identity>();
            ref var entityMesh = ref entity.Get<Mesh>();
            ref var sprite = ref entity.Get<Sprite>();
            ref var recipe = ref entity.Get<Recipe>();
            ref var buildingRecipe = ref entity.Get<BuildingRecipe>();
            ref var localisation = ref entity.Get<Localizations>();

            // Construct command
            var command = new EntityCommand<Identity, Mesh, Sprite, Recipe, BuildingRecipe, Localizations>();
            command.Command = new EntityCommand { Id = entityId.Id, Type = entityId.Type, Opcode = EntityOpCode.Create };
            command.T1Component = entityId;
            command.T2Component = entityMesh;
            command.T3Component = sprite;
            command.T4Component = recipe;
            command.T5Component = buildingRecipe;
            command.T6Component = localisation;

            network.Send(peer, ref command);
        }
    }
}