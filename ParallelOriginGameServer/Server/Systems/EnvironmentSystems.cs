using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using Arch.System.SourceGenerator;
using CommunityToolkit.HighPerformance;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.ThirdParty;
using ZLogger;
using Chunk = ParallelOrigin.Core.ECS.Components.Chunk;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which stores systems which controll the chunks and the terrain generation itself.
/// </summary>
public sealed class EnvironmentGroup : Group<float>
{
    public EnvironmentGroup(World world, EntityPrototyperHierarchy entityPrototyper, GeoTiff biomeTiff) : base(
        
        // Load, track and clear chunks
        new ChunkSystem(world),

        // Environment generation
        new BiomeSystem(world, biomeTiff, entityPrototyper), // Chooses and assigns biome for each new chunk
        new GeneratorSystem(world),
        new SpawnerSystem(world, entityPrototyper),
        
        // Assign entities their chunk, handle enter, switch and leave. 
        new ChunkAssignmentSystem(world)
    )
    {
    }
}

/// <summary>
///     The <see cref="ChunkSystem"/>
///     manages the loading, assigning and deloading of <see cref="Chunk"/>s. 
/// </summary>
public partial class ChunkSystem : BaseSystem<World,float>
{
    private static readonly byte ChunkZoom = 13;
    private const float Alive = 60 * 1;

    public ChunkSystem(World world) : base(world)
    {
    }

    /// <summary>
    ///     Loads the chunks around <see cref="ChunkLoader"/>s.
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="chunkLoader">The <see cref="ChunkLoader"/></param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [None<Prefab,Inactive>]
    private void LoadChunks(in Entity en, ref ChunkLoader chunkLoader, ref NetworkTransform transform)
    {
        var oldCurrent = chunkLoader.Current;

        var grid = TileExtensions.ToGrid(in transform.Pos.X, in transform.Pos.Y, in ChunkZoom);
        chunkLoader.Current = grid;
        chunkLoader.Previous = oldCurrent;

        // Chunkloader moved into a new grid/tile
        if (chunkLoader.Current == chunkLoader.Previous) return;

        // Leave the old chunks 
        // Enter or load or create the new chunks 
        World.LeaveChunks(in en, in chunkLoader.Previous);
        World.EnterChunks(en, in chunkLoader.Current);

        Program.Logger.ZLogInformation(Logs.ChunkSwitch, en, chunkLoader.Previous.X, chunkLoader.Previous.Y, chunkLoader.Current.X, chunkLoader.Current.Y);
    }
    
    /// <summary>
    ///     Clears inactive <see cref="ChunkLoader"/>s.
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="loader">The <see cref="ChunkLoader"/>.</param>
    [Query]
    [None<Inactive>]
    private void ClearInactiveChunkLoaders(in Entity en, ref ChunkLoader loader)
    {
        // Prevent cleaning already cleaned chunk loaders
        if (loader.Current.Equals(Grid.Zero)) return;

        // Find the chunk entitys this loader is in
        var chunks = World.GetChunks(in loader.Current);

        // Remove the loader from the chunk so that the chunk could be disposed properly. 
        foreach (var chunkEntity in chunks)
        {
            ref var chunk = ref chunkEntity.Get<Chunk>();
            chunk.LoadedBy.Remove(en);
        }

        loader.Current = Grid.Zero;

        Program.Logger.ZLogInformation(Logs.SingleAction, "Removed Chunkloader", "Entity became inactive", en);
    }
    
    /// <summary>
    ///     Deloads the chunks.
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="ch">The <see cref="Chunk"/>.</param>
    [Query]
    [None<Destroy, Prefab>]
    private void DeloadChunks(in Entity en, ref Chunk ch)
    {
        // Either add destroy after or remove it... 
        if (ch.LoadedBy.Count > 0)
        {
            if (!en.Has<DestroyAfter>()) return;

            var b = World.Record();
            b.Remove<DestroyAfter>(in en);

            Program.Logger.ZLogInformation(Logs.Chunk, "Marked for destruction is loaded again", en, ch.Grid.X, ch.Grid.Y);
            return;
        }

        // When chunk is already marked with destroy after, it should not get marked again
        if (en.Has<DestroyAfter>()) return;

        var cb = World.Record();
        cb.Add(in en,new DestroyAfter { Seconds = Alive });

        Program.Logger.ZLogInformation(Logs.Chunk, "Marked for destruction", en, ch.Grid.X, ch.Grid.Y);
    }
    
    /// <summary>
    ///     Destroys <see cref="Chunk"/>s when marked as destroy.
    /// </summary>
    /// <param name="ch"></param>
    [Query]
    [All<Destroy>, None<Prefab>]
    private void DestroyChunk(ref Chunk ch)
    {
        foreach (var entity in ch.Contains.Get())
        {
            // Players, being part of the chunk shouldnt be marked for destruction
            if (entity.Has<Character>()) continue;

            var cb = World.Record();
            cb.Add(in entity, new Destroy());
        }
    }
    
    /// <summary>
    ///     Assigns <see cref="NetworkTransform"/>-Values to the <see cref="Chunk"/>s.
    /// </summary>
    /// <param name="ch">The <see cref="Chunk"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [None<Prefab>]
    private void ChunkTransform(ref Chunk ch, ref NetworkTransform transform)
    {
        var grid = TileExtensions.GridToTile(in ch.Grid, in ChunkZoom);
        transform.Chunk = ch.Grid;
        transform.Pos = grid.Middle;
    }
}

/// <summary>
///     The <see cref="ChunkAssignmentSystem"/> class
///     manages the entering and leaving of <see cref="Chunk"/>s for entities like players, mobs and resources. 
/// </summary>
public sealed partial class ChunkAssignmentSystem : BaseSystem<World, float>
{
    private static readonly byte ChunkZoom = 13;
    
    public ChunkAssignmentSystem(World world) : base(world)
    {
    }
    
    /// <summary>
    ///     Makes an <see cref="Entity"/> enter and switch chunks based on their movement.
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [None<Destroy, Inactive, Prefab>]
    private void EnterAndSwitchChunk(in Entity en, ref NetworkTransform transform)
    {
        // Calculate the grid we are in and assign it.
        var grid = TileExtensions.ToGrid(in transform.Pos.X, in transform.Pos.Y, in ChunkZoom);

        // Entity changed chunk... move entity from old chunk into the new one
        if (transform.Chunk != grid)
            World.SwitchChunks(in en, in transform.Chunk, in grid);

        transform.Chunk = grid;
    }
    
    /// <summary>
    ///     Makes an <see cref="Entity"/> leave a <see cref="Chunk"/> once marked for destroy. 
    /// </summary>
    /// <param name="en"></param>
    /// <param name="transform"></param>
    [Query]
    [All<Destroy>, None<Chunk, Prefab>]
    private void LeaveChunk(in Entity en, ref NetworkTransform transform)
    {
        var chunkEntity = World.GetChunk(in transform.Chunk);

        // Mostly the chunk is getting deleted before the entity inside of it
        if (!chunkEntity.IsAlive()) return;
        if (chunkEntity.Has<Destroy>()) return;

        ref var chunk = ref chunkEntity.Get<Chunk>();
        chunk.Contains.Get().Remove(en);
    }
}

/// <summary>
///     The <see cref="BiomeSystem"/> class
///     manages the creation of <see cref="Chunk"/>-Biomes.
/// </summary>
public sealed partial class BiomeSystem : BaseSystem<World,float>
{
    private readonly GeoTiff _biomeTiff;
    private readonly EntityPrototyperHierarchy _entityPrototyperHierarchy;

    public BiomeSystem(World world, GeoTiff biomeTiff, EntityPrototyperHierarchy entityPrototyperHierarchy) : base(world)
    {
        this._biomeTiff = biomeTiff;
        this._entityPrototyperHierarchy = entityPrototyperHierarchy;
    }
    
    /// <summary>
    ///     Choses a <see cref="Chunk"/>-Biome for a <see cref="Chunk"/> and assigns it.
    /// </summary>
    /// <param name="entity">The <see cref="Entity"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [All<Created, Chunk>, None<Prefab>]
    private void ChooseChunkBiome(in Entity entity, ref NetworkTransform transform)
    {
        // Query the biome code based on real world data.
        var biomeCode = (byte)22;
        try
        {
            biomeCode = _biomeTiff.GetPixelValue(1, transform.Pos.X, transform.Pos.Y);
        }
        catch
        {
            /* ignored */
        }

        // Filter biomes by their biomecode 
        var query = new QueryDescription().WithAll<Biome, Prefab>();
        var biomePrefabs = new List<Entity>();
        World.GetEntities(in query, biomePrefabs);
        
        Span<Entity> filteredBiomes = stackalloc Entity[biomePrefabs.Count];
        var biomesWithId = GetByBiomeId(biomeCode, biomePrefabs.AsSpan(), ref filteredBiomes);

        // Choose biome based on its weights
        var choosedBiomeEntity = WeightTableExtensions<Biome>.Get(biomesWithId);

        // Mark chunk with certain biome components required for generating the biome
        if (choosedBiomeEntity.Has<Woodland>())
        {
            entity.Add(choosedBiomeEntity.Get<Woodland>());
            entity.Add(choosedBiomeEntity.Get<ForestLayer>());
            Program.Logger.ZLogInformation(Logs.Biome, entity, transform.Pos.X, transform.Pos.Y, biomeCode);
            return;
        }

        if (choosedBiomeEntity.Has<Grassland>())
        {
            entity.Add(choosedBiomeEntity.Get<Grassland>());
            entity.Add(choosedBiomeEntity.Get<ForestLayer>());
            entity.Add(choosedBiomeEntity.Get<RockLayer>());
            Program.Logger.ZLogInformation(Logs.Biome, entity, transform.Pos.X, transform.Pos.Y, biomeCode);
            return;
        }

        // Default biome is just grassland
        var defaultBiome = _entityPrototyperHierarchy.Get("biome:2");
        entity.Add(defaultBiome.Get<Grassland>());
        entity.Add(defaultBiome.Get<ForestLayer>());
        entity.Add(defaultBiome.Get<RockLayer>());
        Program.Logger.ZLogInformation(Logs.Biome, entity, transform.Pos.X, transform.Pos.Y, biomeCode);
    }

    /// <summary>
    ///     Filters a list of <see cref="Entity" /> biomes by their biomecode and returns those with the fitting biomecode.
    /// </summary>
    /// <param name="biomeCode">The biomecode.</param>
    /// <param name="biomePrefabs">The prefab-entities.</param>
    /// <param name="filteredBiomePrefabs">The filtered biome-prefabs.</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<Entity> GetByBiomeId(byte biomeCode, ReadOnlySpan<Entity> biomePrefabs, ref Span<Entity> filteredBiomePrefabs)
    {
        var counter = 0;
        for (var index = 0; index < biomePrefabs.Length; index++)
        {
            ref readonly var biomeEntity = ref biomePrefabs[index];
            ref var biome = ref biomeEntity.Get<Biome>();

            if (biome.BiomeCode != biomeCode) continue;

            filteredBiomePrefabs[counter] = biomeEntity;
            counter++;
        }

        return filteredBiomePrefabs[..counter];
    }
}

/// <summary>
///     The <see cref="GeneratorSystem"/> class
///     generates the environment based on the <see cref="Chunk"/>-Biomes.
/// </summary>
public sealed partial class GeneratorSystem : BaseSystem<World,float>
{
    private static readonly byte ChunkZoom = 13;
    
    public GeneratorSystem(World world) : base(world)
    {
    }

    /// <summary>
    ///     Generates woodland noise for <see cref="Woodland"/>-<see cref="Chunk"/>s.
    /// </summary>
    /// <param name="woodland">The <see cref="Woodland"/>.</param>
    /// <param name="forestLayer">The <see cref="ForestLayer"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [All<Created,Chunk>, None<Prefab>]
    private void GenerateWoodLand(ref Woodland woodland, ref ForestLayer forestLayer, ref NetworkTransform transform)
    {
        var sw = new Stopwatch();
        sw.Start();

        // Create noise specification
        var noise = new FastNoiseLite(1325);
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(200.00f); // Higher frequency = Noise will appear more often, kinda like zoomed out

        // Create noise 2D array, noise value mapped to its geocoordinate
        var noiseData = new NoiseGeocoordinates[woodland.Resolution, woodland.Resolution];
        noiseData.FillNoise(noise, transform.Pos.X, transform.Pos.Y, ChunkZoom);
        forestLayer.NoiseData = noiseData;

        sw.Stop();
        //noiseData.ToBitMap(@"C:\Users\Lars\Desktop\img_"+en.Get<Identity>().id+"_Woodland.bmp");
    }
    
    /// <summary>
    ///     Generates grassland noise for <see cref="Grassland"/>-<see cref="Chunk"/>s.
    /// </summary>
    /// <param name="grassland">The <see cref="Grassland"/>.</param>
    /// <param name="forestLayer">The <see cref="ForestLayer"/>.</param>
    /// <param name="rockLayer">The <see cref="RockLayer"/>.</param>
    /// <param name="transform">The <see cref="NetworkTransform"/>.</param>
    [Query]
    [All<Created,Chunk>, None<Prefab>]
    private void GenerateGrassland(ref Grassland grassland, ref ForestLayer forestLayer, ref RockLayer rockLayer, ref NetworkTransform transform)
    {
        // Create forest noise 
        var forestNoise = new FastNoiseLite(1325);
        forestNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        forestNoise.SetFrequency(75.00f); // Smaller frequency than woodland for spawning forest patches less common, should also do higher treshhold, so that the patches dont become too large 

        var rockNoise = new FastNoiseLite(1300);
        rockNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        rockNoise.SetFrequency(100.00f);

        // Create noise 2D array, noise value mapped to its geocoordinate
        var forestNoiseData = new NoiseGeocoordinates[grassland.Resolution, grassland.Resolution];
        forestNoiseData.FillNoise(forestNoise, transform.Pos.X, transform.Pos.Y, ChunkZoom);
        forestLayer.NoiseData = forestNoiseData;

        // Create noise 2D array, noise value mapped to its geocoordinate
        var rockNoiseData = new NoiseGeocoordinates[grassland.Resolution, grassland.Resolution];
        rockNoiseData.FillNoise(rockNoise, transform.Pos.X, transform.Pos.Y, ChunkZoom);
        rockLayer.NoiseData = rockNoiseData;

        //noiseData.ToBitMap(@"C:\Users\Lars\Desktop\img_"+en.Get<Identity>().id+"_Grassland.bmp");
    }
}

/// <summary>
///     The <see cref="SpawnerSystem"/> class
///     spawns in the resource-entities based on the chunk-biomes and noise maps.
/// </summary>
public sealed partial class SpawnerSystem : BaseSystem<World,float>
{
    private readonly EntityPrototyperHierarchy _prototyperHierarchy;

    public SpawnerSystem(World world, EntityPrototyperHierarchy prototyperHierarchy) : base(world)
    {
        this._prototyperHierarchy = prototyperHierarchy;
    }
    
    /// <summary>
    ///     Spawns in <see cref="Woodland"/> entities.
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="ch">The <see cref="Chunk"/>.</param>
    /// <param name="woodland">The <see cref="Woodland"/>.</param>
    /// <param name="layer">The <see cref="ForestLayer"/>.</param>
    [Query]
    [All<Created,Woodland>, None<Prefab>]
    private void SpawnWoodland(in Entity en, ref Chunk ch, ref Woodland woodland, ref ForestLayer layer)
    {
        // Chunk should (re)generate when there no resources in it
        if (ch.Contains.Get().EntitiesWith<Resource>() != 0) return;

        var noiseData = layer.NoiseData;
        var filteredEntities = ArrayPool<BiomeEntity>.Shared.Rent(woodland.SpawnableResources.Length);

        // Iterate over the noise to spawn in trees based on their spawning behaviour 
        var size = woodland.Resolution;
        var counter = 0;
        for (var x = 1; x < size; x++)
        for (var y = 1; y < size; y++)
        {
            // Check all spawnable entities and spawn them in if the noise fits
            ref var noiseGeocordinates = ref noiseData[x, y];
            var withinThreshold = woodland.SpawnableResources.WithinThreshold(noiseGeocordinates.Noise, (ref BiomeEntity entity) => entity.ForestNoise, filteredEntities);
            var choosedEntity = WeightTableExtensions<BiomeEntity>.Get(withinThreshold);

            // If there cant spawn anything on the noise
            if (string.IsNullOrEmpty(choosedEntity.Type)) continue;

            var newEntity = _prototyperHierarchy.Clone(choosedEntity.Type);
            ref var entityTransform = ref newEntity.Get<NetworkTransform>();
            ref var entityRotation = ref newEntity.Get<NetworkRotation>();

            entityTransform.Pos = noiseGeocordinates.Geocoordinates;
            entityRotation.Value = RandomExtensions.QuaternionStanding();

            counter++;
        }

        ArrayPool<BiomeEntity>.Shared.Return(filteredEntities);
        Program.Logger.ZLogInformation(Logs.Spawning, "Woodland", counter, en);
    }
    
    /// <summary>
    ///     Spawns in <see cref="Grassland"/> entities.
    /// </summary>
    /// <param name="en">The <see cref="Entity"/>.</param>
    /// <param name="ch">The <see cref="Chunk"/>.</param>
    /// <param name="grassland">The <see cref="Grassland"/>.</param>
    /// <param name="forestLayer">The <see cref="ForestLayer"/>.</param>
    /// <param name="rockLayer">The <see cref="RockLayer"/>.</param>
    [Query]
    [All<Created,Grassland>, None<Prefab>]
    private void SpawnGrassland(in Entity en, ref Chunk ch, ref Grassland grassland, ref ForestLayer forestLayer, ref RockLayer rockLayer)
    {
        // Chunk should (re)generate when there no resources in it
        if (ch.Contains.Get().EntitiesWith<Resource>() != 0) return;

        var rockNoiseData = rockLayer.NoiseData;
        var forestNoiseData = forestLayer.NoiseData;
        
        var filteredResources = ArrayPool<BiomeEntity>.Shared.Rent(grassland.SpawnableResources.Length);
        var filteredMobs = ArrayPool<BiomeEntity>.Shared.Rent(grassland.SpawmableMobs.Length);

        // Iterate over the noise to spawn in rocks as the first layer
        var size = grassland.Resolution;
        var counter = 0;
        for (var x = 1; x < size; x++)
        for (var y = 1; y < size; y++)
        {
            // Check all spawnable entities and spawn them in if the noise fits
            var rockNoise = rockNoiseData[x, y];
            var forestNoise = forestNoiseData[x, y];
            var withinThreshold = grassland.SpawnableResources.Filter((ref BiomeEntity entity) => CanSpawn(forestNoise.Noise, rockNoise.Noise, ref entity), filteredResources);
            var choosedEntity = WeightTableExtensions<BiomeEntity>.Get(withinThreshold);

            // If there cant spawn anything on the noise
            if (string.IsNullOrEmpty(choosedEntity.Type)) continue;

            var newEntity = _prototyperHierarchy.Clone(choosedEntity.Type);
            ref var entityTransform = ref newEntity.Get<NetworkTransform>();
            ref var entityRotation = ref newEntity.Get<NetworkRotation>();

            entityTransform.Pos = rockNoise.Geocoordinates;
            entityRotation.Value = RandomExtensions.QuaternionStanding();

            counter++;
        }

        // Same for mobs
        for (var x = 1; x < size; x++)
        for (var y = 1; y < size; y++)
        {
            // Check all spawnable entities and spawn them in if the noise fits
            var rockNoise = rockNoiseData[x, y];
            var forestNoise = forestNoiseData[x, y];
            var chance = RandomExtensions.GetRandom(0, 100.0f);

            // If 1% chance wasnt met, skip the tile completly 
            if (chance >= 1.0f) continue;

            var withinThreshold = grassland.SpawmableMobs.Filter((ref BiomeEntity entity) => CanSpawn(forestNoise.Noise, rockNoise.Noise, ref entity), filteredMobs);
            var choosedEntity = WeightTableExtensions<BiomeEntity>.Get(withinThreshold);

            // If there cant spawn anything on the noise
            if (string.IsNullOrEmpty(choosedEntity.Type)) continue;

            var newEntity = _prototyperHierarchy.Clone(choosedEntity.Type);
            ref var entityTransform = ref newEntity.Get<NetworkTransform>();
            ref var entityRotation = ref newEntity.Get<NetworkRotation>();

            entityTransform.Pos = rockNoise.Geocoordinates;
            entityRotation.Value = RandomExtensions.QuaternionStanding();
            counter++;
        }

        ArrayPool<BiomeEntity>.Shared.Return(filteredResources);
        ArrayPool<BiomeEntity>.Shared.Return(filteredMobs);
        Program.Logger.ZLogInformation(Logs.Spawning, "Grassland", counter, en);
    }

    /// <summary>
    ///     Dertmines wether an certain <see cref="BiomeEntity" /> has the fitting threshholds to spawn or not.
    /// </summary>
    /// <param name="forest"></param>
    /// <param name="rock"></param>
    /// <param name="entity"></param>
    /// <returns></returns>
    private static bool CanSpawn(float forest, float rock, ref BiomeEntity entity)
    {
        bool forestBool;
        if (entity.ForestCondition == NoiseCondition.GREATER)
            forestBool = entity.ForestNoise <= forest;
        else forestBool = entity.ForestNoise >= forest;

        bool rockBool;
        if (entity.RockCondition == NoiseCondition.GREATER)
            rockBool = entity.RockNoise <= rock;
        else rockBool = entity.RockNoise >= rock;

        return forestBool && rockBool;
    }
}

