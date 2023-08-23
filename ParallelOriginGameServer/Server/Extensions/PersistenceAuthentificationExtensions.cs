using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.ECS.Components;
using ParallelOriginGameServer.Server.Persistence;
using ParallelOriginGameServer.Server.Prototyper;
using Character = ParallelOriginGameServer.Server.Persistence.Character;
using Identity = ParallelOriginGameServer.Server.Persistence.Identity;
using Type = ParallelOriginGameServer.Server.Persistence.Type;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the <see cref="GameDbContext" />
/// </summary>
public static class PersistenceAuthentificationExtensions
{
    /// <summary>
    ///     Checks if an player exists in the database.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="username"></param>
    /// <returns></returns>
    public static async Task<bool> AccountExists(this GameDbContext context, string username)
    {
        var list = await context.Accounts.FromSqlRaw("Select * from account where Username = {0}", username).ToListAsync();
        return list.Count > 0;
    }

    /// <summary>
    ///     Checks if an player exists in the database.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="username"></param>
    /// <returns></returns>
    public static async Task<bool> EmailExists(this GameDbContext context, string email)
    {
        var list = await context.Accounts.FromSqlRaw("Select * from account where Email = {0}", email).ToListAsync();
        return list.Count > 0;
    }

    /// <summary>
    ///     Checks for login credentials.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    /// <returns></returns>
    public static async Task<Account>? Login(this GameDbContext context, string username, string password)
    {
        var list = await context.Accounts.FromSqlRaw("Select * from account where Username = {0} and Password = {1}", username, password).ToListAsync();
        return list.Count <= 0 ? null : list[0];
    }

    /// <summary>
    ///     Registers an account, if already a user with that username registered... return false
    ///     After sucessfull registration true is returned.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    /// <returns></returns>
    public static Account Register(this GameDbContext context, string username, string password, string email, Gender gender, Vector2d position = default)
    {
        // Check if account or mail already exists to prevent another registration...
        var accountExists = context.AccountExists(username);
        accountExists.Wait();
        var emailExists = context.EmailExists(email);
        emailExists.Wait();

        if (accountExists.Result || emailExists.Result) return null;

        // Create identity and position
        var identity = new Identity { Tag = "player", Type = "1:1" };
        var transform = new Transform { X = (float)position.X, Y = (float)position.Y, RotX = 0, RotY = 0, RotZ = 0 };

        // Create character
        var character = new Character
        {
            Identity = identity,
            Transform = transform,
            Chunk = null,
            Inventory = new HashSet<InventoryItem>(32),
            Health = 100.0f
        };

        // Create account and save
        var account = new Account
        {
            Username = username,
            Password = password,
            Email = email,
            Character = character,
            Gender = gender,
            Type = Type.NORMAL,
            Registered = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            AcceptedGdpr = DateTime.UtcNow
        };

        // Add some starting items
        var startWoodItem = new InventoryItem
        {
            Identity = new Identity { Id = (long)RandomExtensions.GetUniqueULong(), Type = Types.Gold, Tag = Tags.Item },
            Character = character,
            Amount = 10,
            Level = 0
        };
        character.Inventory.Add(startWoodItem);

        // Add to context
        context.Accounts.Add(account);
        context.Identities.Add(identity);
        context.Characters.Add(character);
        context.InventoryItems.Add(startWoodItem);

        context.SaveChanges();
        return account;
    }
}