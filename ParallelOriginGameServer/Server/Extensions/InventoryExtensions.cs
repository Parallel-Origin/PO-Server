using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.Extensions;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for everything <see cref="Inventory" /> related
/// </summary>
public static class InventoryExtensions
{
    /// <summary>
    ///     Searches an item by its type in the inventory and returns it.
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static int GetIndexByType(this ref Inventory inventory, in string type)
    {
        var entities = inventory.Items;
        for (var index = 0; index < inventory.Items.Count; index++)
        {
            var itemRef = entities[index];
            var itemEntity = (Entity)itemRef.Entity;
            ref var identity = ref itemEntity.Get<Identity>();

            if (identity.Type.Equals(type))
                return index;
        }

        return -1;
    }

    /// <summary>
    ///     Searches an item inside the inv based on its type and returns it
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Entity GetItemByType(this ref Inventory inventory, in string type)
    {
        var index = inventory.GetIndexByType(type);
        if (index == -1) return default;

        var itemRef = inventory.Items[index];
        return itemRef.Entity;
    }

    /// <summary>
    ///     Checks if the inventory has a certain item type with a certain amount and returns either true or false.
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static bool Has(this ref Inventory inventory, in string type, in uint amount)
    {
        var entity = inventory.GetItemByType(type);
        if (!entity.IsAlive()) return false;

        ref var item = ref entity.Get<Item>();
        return item.Amount >= amount;
    }

    /// <summary>
    ///     Checks if the inventory contains enough ingredients of a certain recipe to fullfill it.
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public static bool Has(this ref Inventory inventory, ref Recipe recipe)
    {
        ref var ingredients = ref recipe.Ingredients;
        for (var index = 0; index < ingredients.Length; index++)
        {
            ref var ingredient = ref ingredients[index];
            if (!inventory.Has(ingredient.Type, ingredient.Amount)) return false; // If it doesnt have a certain ingredient, stop
        }

        return true;
    }

    /// <summary>
    ///     Adds an item to an inventory.
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="hierarchy"></param>
    /// <param name="amount"></param>
    /// <param name="type"></param>
    public static Entity Add(this ref Inventory inventory, in EntityPrototyperHierarchy hierarchy, in Entity owner, in uint amount, in string type)
    {
        // Add
        var itemEntity = hierarchy.Clone(type);
        ref var identity = ref itemEntity.Get<Identity>();
        ref var item = ref itemEntity.Get<Item>();
        ref var inInventory = ref itemEntity.Get<InInventory>();

        item.Amount = amount;
        inInventory.Inventory = owner;
        inventory.Items.Add(new EntityLink(itemEntity, identity.Id));

        return itemEntity;
    }

    /// <summary>
    ///     Adds an item to an inventory.
    ///     If the type already exist its getting merged, otherwhise it creates a new item of that type using the hierarhcy.
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="hierarchy"></param>
    /// <param name="amount"></param>
    /// <param name="type"></param>
    /// <param name="entity">The entity which added as an item or the already existing entity which was merged into </param>
    public static bool AddOrMerge(this ref Inventory inventory, in EntityPrototyperHierarchy hierarchy, in Entity owner, in uint amount, in string type, out Entity entity)
    {
        var exists = inventory.GetIndexByType(in type);
        if (exists == -1)
        {
            // Add
            entity = inventory.Add(hierarchy, in owner, in amount, in type);
            return true;
        }

        // Merge
        entity = inventory.GetItemByType(in type);
        ref var item = ref entity.Get<Item>();

        item.Amount += amount;
        return false;
    }

    /// <summary>
    ///     Substracts an certain item amount from the inventory.
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <param name="entity"></param>
    /// The item entity from which an amount was substracted.
    /// <returns>True if it could be substracted, else false</returns>
    public static bool Substract(this ref Inventory inventory, in string type, in uint amount, out Entity entity)
    {
        entity = inventory.GetItemByType(type);
        if (!entity.IsAlive()) return false;

        ref var item = ref entity.Get<Item>();
        if (item.Amount < amount) return false;

        item.Amount -= amount;
        return true;
    }

    /// <summary>
    ///     Substracts an certain item amount from the inventory.
    /// </summary>
    /// <param name="inventory"></param>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <param name="entity"></param>
    /// The item entity from which an amount was substracted.
    /// <returns>True if it was removed, false if it only was substracted or untouchede</returns>
    public static bool SubstractOrRemove(this ref Inventory inventory, in string type, in uint amount, out Entity entity)
    {
        inventory.Substract(type, amount, out entity);
        ref var item = ref entity.Get<Item>();

        if (item.Amount != 0) return false;

        var reference = inventory.Items.Find(ref entity);
        inventory.Items.Remove(reference);

        return true;
    }
}