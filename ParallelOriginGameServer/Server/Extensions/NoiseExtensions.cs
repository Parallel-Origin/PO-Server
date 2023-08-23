using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.ECS.Components.Environment;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An interface providing noise
/// </summary>
public interface INoise
{
    float Noise { get; }
}

/// <summary>
///     A extensions for <see cref="Span{T}" /> featuring entities.
/// </summary>
public static partial class NoiseExtensions
{
    /// <summary>
    ///     Fills an <see cref="noiseData" /> array with noise values based on the passed <see cref="noise" />
    /// </summary>
    /// <param name="noiseData"></param>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    /// <param name="zoom"></param>
    /// <param name="resolution"></param>
    /// <param name="noise"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillNoise(this NoiseGeocoordinates[,] noiseData, FastNoiseLite noise, double lat, double lon, byte zoom)
    {
        // Convert position to tile and calculate wide and height that tile is based on the resolution we want. 
        var size = noiseData.GetLength(0);
        var tile = TileExtensions.GeoToTile(lat, lon, zoom);
        var verticalSteps = tile.Range.X / size;
        var horizontalSteps = tile.Range.Y / size;

        // Gather noise data
        for (var x = 0; x < size; x++)
        for (var y = 0; y < size; y++)
        {
            // Map resolution to geo location, we begin in the left bottom of the chunk
            var geoX = tile.South + x * verticalSteps;
            var geoY = tile.West + y * horizontalSteps;

            // Put a new noise geo coordinate with its resolution coordinate into the array 
            var coordinates = new Vector2d(geoX, geoY);
            var noiseGeocoordinates = new NoiseGeocoordinates(coordinates, noise.GetNoise((float)geoX, (float)geoY));
            noiseData[x, y] = noiseGeocoordinates;
        }
    }


    /// <summary>
    ///     Saves the generated bitmap for debugging.
    /// </summary>
    /// <param name="array"></param>
    /// <param name="en"></param>
    public static void ToBitMap(this NoiseGeocoordinates[,] array, string path)
    {
        var bit = new Bitmap(32, 32);

        // Gather noise data
        for (var x = 0; x < 32; x++)
        for (var y = 0; y < 32; y++)
        {
            var noiseGeocoordinates = array[x, y];
            var calc = (int)((noiseGeocoordinates.Noise + 1) / 2 * 255);
            bit.SetPixel(x, y, Color.FromArgb(calc, calc, calc));
        }

        bit.Save(path);
        bit.Dispose();
    }
}

/// <summary>
///     Should return the noise of <see cref="T" />
/// </summary>
/// <typeparam name="T"></typeparam>
public delegate float GetNoise<T>(ref T t);

/// <summary>
///     Should return wether <see cref="T" /> is being filtered and should count.
/// </summary>
/// <typeparam name="T"></typeparam>
public delegate bool Filter<T>(ref T t);

/// <summary>
///     A extensions for <see cref="Span{T}" /> featuring filter methods for noise entities.
/// </summary>
public static partial class NoiseExtensions
{
    /// <summary>
    ///     Checks for each passed object if its noise is within the <see cref="threshold" /> and returns a filtered <see cref="ReadOnlySpan{T}" />
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="threshold"></param>
    /// <param name="withinTresholdEntities"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> WithinThreshold<T>(this ReadOnlySpan<T> entities, float threshold, ref Span<T> withinTresholdEntities) where T : INoise
    {
        var counter = 0;
        for (var index = 0; index < entities.Length; index++)
        {
            ref readonly var entity = ref entities[index];

            // if beneath the treshold, continue
            if (!(threshold >= entity.Noise)) continue;

            // Otherwhise add to the filtered span
            withinTresholdEntities[counter] = entity;
            counter++;
        }

        return withinTresholdEntities[..counter];
    }

    /// <summary>
    ///     Checks for each passed object if its noise is within the <see cref="threshold" /> and returns a filtered <see cref="ReadOnlySpan{T}" />
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="threshold"></param>
    /// <param name="withinTresholdEntities"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> WithinThreshold<T>(this T[] entities, float threshold, GetNoise<T> getNoise, T[] withinTresholdEntities)
    {
        var counter = 0;
        for (var index = 0; index < entities.Length; index++)
        {
            ref var entity = ref entities[index];

            // if beneath the treshold, continue
            if (!(threshold >= getNoise(ref entity))) continue;

            // Otherwhise add to the filtered span
            withinTresholdEntities[counter] = entity;
            counter++;
        }

        return withinTresholdEntities[..counter];
    }

    /// <summary>
    ///     Checks for each passed object if its <see cref="Filter{T}" /> returns true and if so its being put into the array and returned in a <see cref="ReadOnlySpan{T}" />
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="threshold"></param>
    /// <param name="withinTresholdEntities"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> Filter<T>(this T[] entities, Filter<T> filter, T[] withinTresholdEntities)
    {
        var counter = 0;
        for (var index = 0; index < entities.Length; index++)
        {
            ref var entity = ref entities[index];

            // if beneath the treshold, continue
            if (!filter(ref entity)) continue;

            // Otherwhise add to the filtered span
            withinTresholdEntities[counter] = entity;
            counter++;
        }

        return withinTresholdEntities[..counter];
    }
}