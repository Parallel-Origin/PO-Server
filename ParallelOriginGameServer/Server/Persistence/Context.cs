using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ParallelOrigin.Core.ECS.Components;
using ParallelOriginGameServer.Server.Prototyper;

namespace ParallelOriginGameServer.Server.Persistence;

// To update existing database instance manually. 
// dotnet ef migrations add <NAME>
// dotnet ef database update

/// <summary>
///     The <see cref="DbContext" /> class
///     represents the database-acess-layer of our game.
/// </summary>
public class GameDbContext : DbContext
{
    /// <summary>
    ///     The connection string for the posgresql database instance.
    /// </summary>
    private const string ConnectionString = "DATABASE-STRING";
    
    /// <summary>
    ///     If true, a in memory database is used.
    /// </summary>
    private readonly bool _inMemory;
    
    /// <summary>
    ///     Initializes a new <see cref="DbContext"/> for a database instance.
    /// <remarks>Requires <see cref="ConnectionString"/> being set.</remarks>
    /// </summary>
    public GameDbContext() : this(false)
    {
    }
    
    /// <summary>
    ///     Initializes a new <see cref="DbContext"/>.
    /// </summary>
    /// <param name="inMemory">If true, an in memory database will be used.</param>
    public GameDbContext(bool inMemory)
    {
        _inMemory = inMemory;
    }
    
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Chunk> Chunks { get; set; }
    public DbSet<Identity> Identities { get; set; }
    public DbSet<Character> Characters { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<Structure> Structures { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<Mob> Mobs { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!_inMemory)
        {
            optionsBuilder.UseNpgsql(ConnectionString,options =>
                {
                    options.UseAdminDatabase("postgres");
                    options.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                }
            ).UseSnakeCaseNamingConvention();
        }
        else
        {
            optionsBuilder.UseSqlite("DataSource=file::memory:?cache=shared").UseSnakeCaseNamingConvention();
            //optionsBuilder.UseInMemoryDatabase("parallelorigin").UseSnakeCaseNamingConvention();
        }

        //optionsBuilder.EnableSensitiveDataLogging();
        //optionsBuilder.EnableDetailedErrors();
        //optionsBuilder.UseLoggerFactory(Program.factory);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.UseIdentityByDefaultColumns();
        
        // Define account
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("account");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityByDefaultColumn();

            entity.HasOne(e => e.Character).WithOne().HasForeignKey<Account>(e => e.CharacterId);
            entity.Navigation(e => e.Character).AutoInclude();
        });
        
        // Define identity, basically all entities ingame. 
        modelBuilder.Entity<Identity>(entity => { entity.ToTable("identity"); });
        
        // Define chunk and relations. 
        modelBuilder.Entity<Chunk>(entity =>
        {
            entity.ToTable("chunk");
            entity.HasKey(e => e.IdentityId);
            entity.HasOne(x => x.Identity).WithOne().HasForeignKey<Chunk>(x => x.IdentityId);

            entity.HasMany(e => e.ContainedCharacters).WithOne(e => e.Chunk).IsRequired(false);
            entity.HasMany(e => e.ContainedResources).WithOne(e => e.Chunk);
            entity.HasMany(e => e.ContainedStructures).WithOne(e => e.Chunk);
            entity.HasMany(e => e.ContainedItems).WithOne(e => e.Chunk);
            entity.HasMany(e => e.ContainedMobs).WithOne(e => e.Chunk);
            entity.Navigation(e => e.Identity).AutoInclude();
            entity.Navigation(e => e.ContainedCharacters).AutoInclude();
            entity.Navigation(e => e.ContainedResources).AutoInclude();
            entity.Navigation(e => e.ContainedStructures).AutoInclude();
            entity.Navigation(e => e.ContainedItems).AutoInclude();
            entity.Navigation(e => e.ContainedMobs).AutoInclude();
        });

        // Define character and relations
        modelBuilder.Entity<Character>(entity =>
        {
            entity.ToTable("character");
            entity.HasKey(character => character.IdentityId);
            entity.HasOne(e => e.Identity).WithOne().HasForeignKey<Character>(e => e.IdentityId); // Hat identity welcher als Foreignkey für Charackter dient
            entity.OwnsOne(e => e.Transform);
            entity.HasOne(e => e.Chunk).WithMany(e => e.ContainedCharacters).IsRequired(false);
            entity.HasMany(e => e.Inventory).WithOne(e => e.Character);
            entity.Navigation(character => character.Identity).AutoInclude();
            entity.Navigation(character => character.Inventory).AutoInclude();
        });

        // Define resource and relations
        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("resource");
            entity.HasKey(e => e.IdentityId);
            entity.HasOne(e => e.Identity).WithOne().HasForeignKey<Resource>(e => e.IdentityId);
            entity.OwnsOne(e => e.Transform);
            entity.HasOne(e => e.Chunk).WithMany(e => e.ContainedResources);
            entity.Navigation(e => e.Identity).AutoInclude();
            entity.Navigation(e => e.Transform).IsRequired();
        });

        // Define structure and relations
        modelBuilder.Entity<Structure>(entity =>
        {
            entity.ToTable("structure");
            entity.HasKey(e => e.IdentityId);
            entity.HasOne(e => e.Identity).WithOne().HasForeignKey<Structure>(e => e.IdentityId);
            entity.OwnsOne(e => e.Transform);
            entity.HasOne(e => e.Chunk).WithMany(e => e.ContainedStructures);
            entity.Navigation(e => e.Identity).AutoInclude();
            entity.Navigation(e => e.Transform).IsRequired();
        });

        // Define item and relation
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("item");
            entity.HasKey(e => e.IdentityId);
            entity.HasOne(e => e.Identity).WithOne().HasForeignKey<Item>(e => e.IdentityId);
            entity.OwnsOne(e => e.Transform);
            entity.HasOne(e => e.Chunk).WithMany(e => e.ContainedItems);
            entity.Navigation(e => e.Identity).AutoInclude();
            entity.Navigation(e => e.Transform).IsRequired();
        });

        // Define mob and relation
        modelBuilder.Entity<Mob>(entity =>
        {
            entity.ToTable("mob");
            entity.HasKey(e => e.IdentityId);
            entity.HasOne(e => e.Identity).WithOne().HasForeignKey<Mob>(e => e.IdentityId);
            entity.OwnsOne(e => e.Transform);
            entity.HasOne(e => e.Chunk).WithMany(e => e.ContainedMobs);
            entity.Navigation(e => e.Identity).AutoInclude();
            entity.Navigation(e => e.Transform).IsRequired();
        });

        // Define inventoryitem and relation
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("inventoryItem");
            entity.HasKey(e => e.IdentityId);
            entity.HasOne(e => e.Identity).WithOne().HasForeignKey<InventoryItem>(e => e.IdentityId);
            entity.HasOne(e => e.Character).WithMany(e => e.Inventory);
            entity.Navigation(e => e.Identity).AutoInclude();
        });
        
        
        ///////////////////////////
        /// Default data
        ///////////////////////////
        
        
        // Fille sample data.
        modelBuilder.Entity<Account>().HasData(
            new Account
            {
                Id = 1,
                Username = "Test",
                Email = "none@gmail.com",
                Password = "test",
                Gender = Gender.Male,
                Type = Type.OWNER,
                Registered = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                AcceptedGdpr = DateTime.UtcNow,
                CharacterId = 1,
            }
        );
        
        // Fill identity
        modelBuilder.Entity<Identity>().HasData(
            new Identity
            {
                Id = 1,
                Type = Types.DefaultCharacter,
                Tag = Tags.Character,
            }
        );
        
        // Fill character data.
        modelBuilder.Entity<Character>().HasData(
            new Character
            {
                IdentityId = 1,
                Inventory = new HashSet<InventoryItem>(),
                Health = 100.0f,
                Chunk = null,
            }
        );
        
        // Fill transform
        modelBuilder.Entity<Character>().OwnsOne(p => p.Transform).HasData(
            new
            {
                CharacterIdentityId = 1L,
                X = (float)51.855030,
                Y = (float)8.294480,
                RotX = (float)0,
                RotY = (float)0,
                RotW = (float)0,
                RotZ = (float)0,
            }
        );
    }
}