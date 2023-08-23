using System;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOriginGameServer.Server.Systems;

namespace ParallelOriginGameServer.Server.Prototyper;

/// <summary>
///     An <see cref="Prototyper{I,T}" /> for <see cref="Biome" />'s
///     Those will not be spawned, this prototyper only acts as some sort of one time initialisation of the different biome entities.
/// </summary>
public class BiomePrototyper : Prototyper<Entity>
{
    //////////////////////
    /// Mobs
    //////////////////////

    // Represents a spawning time frame of the whole day
    private static readonly ValueTuple<TimeSpan, TimeSpan> AllDay = new(new TimeSpan(0, 0, 0), new TimeSpan(24, 0, 0));

    // Array of common entities for biomes used to spawn them in
    private static readonly BiomeEntity[] CommonMobs =
    {
        new() { Type = Types.Wolve, Weight = 100.0f, Pack = true, PackSizeMin = 2, PackSizeMax = 5, Times = new[] { AllDay } }
    };

    //////////////////////
    /// Resources
    //////////////////////
    
    // Array of common wood land resources
    private static readonly BiomeEntity[] WoodlandResources =
    {
        new() { Type = Types.SouthernTree, Weight = 100.0f, ForestNoise = 0.4f },
        new() { Type = Types.StonePile, Weight = 5.0f, ForestNoise = 0.4f }
    };

    // Array of common grassland resources
    private static readonly BiomeEntity[] GrasslandResources =
    {
        new() { Type = Types.Bush, Weight = 100.0f, ForestNoise = 0.5f, RockCondition = NoiseCondition.SMALLER, RockNoise = 0.6f },
        new() { Type = Types.SouthernTree, Weight = 15.0f, ForestNoise = 0.6f, RockCondition = NoiseCondition.SMALLER, RockNoise = 0.5f },
        new() { Type = Types.StonePile, Weight = 100.0f, ForestCondition = NoiseCondition.SMALLER, ForestNoise = 0.5f, RockNoise = 0.6f }
    };
    
    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();

        // Woodland biome
        Register(1, () => world.Create<Identity, Woodland, Biome, ForestLayer>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Biome, Type = Types.Woodland },
                new Woodland { Resolution = 32, SpawnableResources = WoodlandResources, SpawmableMobs = CommonMobs },
                new Biome { Weight = 100.0f, BiomeCode = 22 }
            );
        });

        // Grassland biome
        Register(2, () => world.Create<Identity, Grassland, Biome, ForestLayer, RockLayer>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Id = 0, Tag = Tags.Biome, Type = Types.Grassland }, 
                new Grassland { Resolution = 32, SpawnableResources = GrasslandResources, SpawmableMobs = CommonMobs },
                new Biome{ Weight = 100.0f, BiomeCode = 22 }
            );
        });
    }

    public override void AfterInstanced(short typeId, ref Entity clonedInstance)
    {
        base.AfterInstanced(typeId, ref clonedInstance);
        clonedInstance.Add<Prefab>();
    }
}