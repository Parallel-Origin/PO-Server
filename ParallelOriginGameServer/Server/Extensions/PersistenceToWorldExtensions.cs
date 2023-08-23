using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using ConcurrentCollections;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.LowLevel;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Persistence;
using Chunk = ParallelOriginGameServer.Server.Persistence.Chunk;
using Identity = ParallelOrigin.Core.ECS.Components.Identity;
using Item = ParallelOrigin.Core.ECS.Components.Item;
using Mob = ParallelOriginGameServer.Server.Persistence.Mob;
using Resource = ParallelOriginGameServer.Server.Persistence.Resource;
using Structure = ParallelOriginGameServer.Server.Persistence.Structure;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the <see cref="Mapper" /> and for several components to ease the mapping.
/// </summary>
public static class PersistenceToWorldExtensions
{
    /// <summary>
    ///     Converts an <see cref="Account" /> to an fully functional character/player entity.
    /// </summary>
    /// <param name="acc"></param>
    /// <returns></returns>
    public static Entity ToEcs(this Account acc)
    {
        var accCharacter = acc.Character;

        // Clone
        var entityPrototyper = ServiceLocator.Get<EntityPrototyperHierarchy>();
        var en = entityPrototyper.Clone(accCharacter.Identity.Type);

        // Get components 
        ref var identity = ref en.Get<ParallelOrigin.Core.ECS.Components.Identity>();
        ref var character = ref en.Get<ParallelOrigin.Core.ECS.Components.Character>();
        ref var health = ref en.Get<Health>();
        ref var transform = ref en.Get<NetworkTransform>();
        ref var rotation = ref en.Get<NetworkRotation>();
        ref var movement = ref en.Get<Movement>();
        ref var inventory = ref en.Get<Inventory>();

        // Database data to component data 
        identity.Id = accCharacter.Identity.Id;

        character.Name = acc.Username;
        character.Password = acc.Password;
        character.Email = acc.Email;
        
        health.CurrentHealth = accCharacter.Health;

        transform.Pos = new Vector2d(accCharacter.Transform.X, accCharacter.Transform.Y);
        rotation.Value = new Quaternion(accCharacter.Transform.RotX, accCharacter.Transform.RotY, accCharacter.Transform.RotZ, accCharacter.Transform.RotW);
        movement.Target = transform.Pos;

        /*
        // Create items & assign it to them 
        inventory.items = new UnsafeList<EntityLink>(accCharacter.Inventory.Count);
        foreach (var inventoryItem in acc.Character.Inventory)
        {
            var itemEntity = inventoryItem.ToECS();
            ref var itemEntityId = ref itemEntity.Get<Identity>();
            ref var inInventory = ref itemEntity.Get<InInventory>();
            inInventory.inventory = en;
            inventory.items.Add(new EntityLink(in itemEntity, itemEntityId.id));
        }*/

        // Assign components 
        en.Set(identity); // To trigger events like the one for the initialisation system
        en.Set(new Model { ModelDto = acc });
        return en;
    }

    /// <summary>
    ///     Creates a new chunk entity from an <see cref="Persistence.Chunk" />
    /// </summary>
    /// <param name="world"></param>
    /// <param name="grid"></param>
    public static Entity ToEcs(this Chunk chunkDto)
    {
        var world = ServiceLocator.Get<World>();

        // Create chunk components
        var identity = new Identity { Id = chunkDto.IdentityId, Tag = chunkDto.Identity.Tag, Type = chunkDto.Identity.Type };

        var grid = new Grid(chunkDto.X, chunkDto.Y);
        var chunk = new ParallelOrigin.Core.ECS.Components.Chunk(grid);
        chunk.CreatedOn = chunkDto.CreatedOn;

        // Assign components 
        var entity = world.Create<Identity,ParallelOrigin.Core.ECS.Components.Chunk,NetworkTransform,Model>();
        entity.Set(identity, chunk, new Model { ModelDto = chunkDto });
        return entity;
    }

    /// <summary>
    ///     Creates an <see cref="Entity" /> from the passed <see cref="Persistence.Resource" />
    /// </summary>
    /// <param name="resourceDto"></param>
    /// <returns></returns>
    public static Entity ToEcs(this Resource resourceDto)
    {
        // Clone
        var entityPrototyper = ServiceLocator.Get<EntityPrototyperHierarchy>();
        var resourceEntity = entityPrototyper.Clone(resourceDto.Identity.Type);

        // Create components
        var identity = new Identity { Id = resourceDto.IdentityId, Tag = resourceDto.Identity.Tag, Type = resourceDto.Identity.Type };
        var resource = new ParallelOrigin.Core.ECS.Components.Resource();
        var networkTransform = new NetworkTransform { Pos = new Vector2d(resourceDto.Transform.X, resourceDto.Transform.Y) };
        var networkRotation = new NetworkRotation { Value = new Quaternion(resourceDto.Transform.RotX, resourceDto.Transform.RotY, resourceDto.Transform.RotZ, resourceDto.Transform.RotW) };

        // Assign components
        resourceEntity.Set(identity);
        resourceEntity.Set(resource);
        resourceEntity.Set(networkTransform);
        resourceEntity.Set(networkRotation);
        resourceEntity.Add(new Model { ModelDto = resourceDto });

        return resourceEntity;
    }

    /// <summary>
    ///     Creates an <see cref="Entity" /> from the passed <see cref="Persistence.Structure" />
    /// </summary>
    /// <param name="resourceDTO"></param>
    /// <returns></returns>
    public static Entity ToEcs(this Structure structureDto)
    {
        // Clone
        var world = ServiceLocator.Get<World>();
        var entityPrototyper = ServiceLocator.Get<EntityPrototyperHierarchy>();
        var structureEntity = entityPrototyper.Clone(structureDto.Identity.Type);

        // Create components
        var identity = new Identity { Id = structureDto.IdentityId, Tag = structureDto.Identity.Tag, Type = structureDto.Identity.Type };
        var networkTransform = new NetworkTransform { Pos = new Vector2d(structureDto.Transform.X, structureDto.Transform.Y) };
        var networkRotation = new NetworkRotation { Value = new Quaternion(structureDto.Transform.RotX, structureDto.Transform.RotY, structureDto.Transform.RotZ, structureDto.Transform.RotW) };

        ref var structure = ref structureEntity.Get<ParallelOrigin.Core.ECS.Components.Structure>();
        if (structureDto.Character != null)
        {
            var id = structureDto.Character.Identity.Id;
            var ownerEntity = world.GetById(id);

            //structure.color = Color.Chartreuse;
            structure.Owner = new EntityLink(ownerEntity, id);
        }

        ref var health = ref structureEntity.Get<Health>();
        health.CurrentHealth = (float)structureDto.Health;

        // Assign components
        structureEntity.Set(identity);
        structureEntity.Set(structure);
        structureEntity.Set(networkTransform);
        structureEntity.Set(networkRotation);
        structureEntity.Add(new Model { ModelDto = structureDto });

        return structureEntity;
    }

    /// <summary>
    ///     Creates an <see cref="Entity" /> from the passed <see cref="InventoryItem" />
    /// </summary>
    /// <param name="inventoryItem"></param>
    /// <returns></returns>
    public static Entity ToEcs(this InventoryItem inventoryItem)
    {
        // Clone
        var entityPrototyper = ServiceLocator.Get<EntityPrototyperHierarchy>();
        var itemEntity = entityPrototyper.Clone(inventoryItem.Identity.Type);

        // Get components
        ref var identity = ref itemEntity.Get<Identity>();
        ref var item = ref itemEntity.Get<Item>();

        // Database data fill into components
        identity.Id = inventoryItem.Identity.Id;
        item.Amount = (uint)inventoryItem.Amount;

        // Assign model
        itemEntity.Add(new Model { ModelDto = inventoryItem });
        return itemEntity;
    }

    /// <summary>
    ///     Creates an <see cref="Entity" /> from the passed <see cref="Persistence.Mob" />
    /// </summary>
    /// <param name="resourceDTO"></param>
    /// <returns></returns>
    public static Entity ToEcs(this Mob mobDto)
    {
        // Clone
        var entityPrototyper = ServiceLocator.Get<EntityPrototyperHierarchy>();
        var structureEntity = entityPrototyper.Clone(mobDto.Identity.Type);

        // Create components
        var identity = new Identity { Id = mobDto.IdentityId, Tag = mobDto.Identity.Tag, Type = mobDto.Identity.Type };
        var networkTransform = new NetworkTransform { Pos = new Vector2d(mobDto.Transform.X, mobDto.Transform.Y) };
        var networkRotation = new NetworkRotation { Value = new Quaternion(mobDto.Transform.RotX, mobDto.Transform.RotY, mobDto.Transform.RotZ, mobDto.Transform.RotW) };

        ref var health = ref structureEntity.Get<Health>();
        health.CurrentHealth = (float)mobDto.Health;

        // Assign components
        structureEntity.Set(identity);
        structureEntity.Set(networkTransform);
        structureEntity.Set(networkRotation);
        structureEntity.Add(new Model { ModelDto = mobDto });

        return structureEntity;
    }
}