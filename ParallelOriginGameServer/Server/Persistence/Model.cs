using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using ParallelOrigin.Core.ECS.Components;

namespace ParallelOriginGameServer.Server.Persistence;

/// <summary>
///     Different account types
/// </summary>
public enum Type
{
    NORMAL,
    MODERATION,
    ADMIN,
    OWNER
}

/// <summary>
///     Represents an account ingame.
/// </summary>
public class Account
{
    [Key] public long Id { get; set; }

    public string Username { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }


    public long CharacterId { get; set; }
    public Character Character { get; set; }
    public Gender? Gender { get; set; }

    public Type Type { get; set; }

    public DateTime? Registered { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? AcceptedGdpr { get; set; }
}

/// <summary>
///     Represents an identity in our database.
/// </summary>
public class Identity
{
    [Key] public long Id { get; set; }

    public string Type { get; set; }
    public string Tag { get; set; }
}

/// <summary>
///     Represents an transform in the world.
/// </summary>
public class Transform
{
    // Default constructor because : https://github.com/dotnet/efcore/issues/20882
    // Hopefully solves the issue where we get an exception upon saving a mob which tells us transform_x is null ? 
    public Transform()
    {
        X = 1;
        Y = 1;
        RotX = 1;
        RotY = 1;
        RotZ = 1;
    }

    public float X { get; set; }
    public float Y { get; set; }

    public float RotX { get; set; }
    public float RotY { get; set; }
    public float RotZ { get; set; }
    public float RotW { get; set; }
}

/// <summary>
///     Represents an chunk which is basically a grid in the game world with many other entities inside it.
/// </summary>
public class Chunk
{
    public long IdentityId { get; set; }
    public Identity Identity { get; set; }

    public ushort X { get; set; }
    public ushort Y { get; set; }

    public DateTime CreatedOn { get; set; }
    public DateTime? RefreshedOn { get; set; }

    public ISet<Character> ContainedCharacters { get; set; }
    public ISet<Resource> ContainedResources { get; set; }
    public ISet<Structure> ContainedStructures { get; set; }
    public ISet<Item> ContainedItems { get; set; }
    public ISet<Mob> ContainedMobs { get; set; }
}

/// <summary>
///     Represents an character ingame with all his attributes.
/// </summary>
public class Character
{
    public long IdentityId { get; set; }
    public Identity Identity { get; set; }

    public Transform Transform { get; set; }
    public long? ChunkId { get; set; }
    public Chunk? Chunk { get; set; }

    public ISet<InventoryItem> Inventory { get; set; }

    public float Health { get; set; }
}

/// <summary>
///     Represents an resource in the world like trees or stones.
/// </summary>
public class Resource
{
    public long IdentityId { get; set; }
    public Identity Identity { get; set; }

    public Transform Transform { get; set; }
    public long ChunkId { get; set; }
    public Chunk Chunk { get; set; }

    public DateTime? HarvestedOn { get; set; }
}

/// <summary>
///     Represents an structure in the world like buildings or ownerless structures
/// </summary>
public class Structure
{
    public long IdentityId { get; set; }
    public Identity Identity { get; set; }

    public Transform Transform { get; set; }
    public long ChunkId { get; set; }
    public Chunk Chunk { get; set; }

    public long? CharacterId { get; set; }
    public Character? Character { get; set; }

    public float? Health { get; set; }
}

/// <summary>
///     Represents an item on the ground
/// </summary>
public class Item
{
    public long IdentityId { get; set; }
    public Identity Identity { get; set; }

    public Transform Transform { get; set; }
    public long ChunkId { get; set; }
    public Chunk Chunk { get; set; }

    public ulong Amount { get; set; }
    public byte Level { get; set; }
}

/// <summary>
///     Represents an structure in the world like buildings or ownerless structures
/// </summary>
public class Mob
{
    public long IdentityId { get; set; }
    public Identity Identity { get; set; }

    public Transform Transform { get; set; }

    public long ChunkId { get; set; }
    [JsonIgnore] public Chunk Chunk { get; set; }

    public float? Health { get; set; }
}

/// <summary>
///     Represents an item inside an inventory
/// </summary>
public class InventoryItem
{
    public long IdentityId { get; set; }
    public Identity Identity { get; set; }

    public long CharacterId { get; set; }
    public Character Character { get; set; }

    public ulong Amount { get; set; }
    public byte Level { get; set; }
    public bool Equiped { get; set; }
}