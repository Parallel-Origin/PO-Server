using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ParallelOrigin.Core.Base.Classes;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     A extension for the <see cref="Tile" />
/// </summary>
public static class TileExtensions
{
    private const int EARTH_RADIUS = 6378137; //no seams with globe example
    private const double ORIGIN_SHIFT = 2 * Math.PI * EARTH_RADIUS / 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToLongitude(in int x, in byte z)
    {
        return x / Math.Pow(2.0, z) * 360.0 - 180;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToLatitude(in int y, in byte z)
    {
        var n = Math.PI - 2.0 * Math.PI * y / Math.Pow(2.0, z);
        return Math.Atan(Math.Sinh(n)) * (180 / Math.PI);
        ;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToLongitude(in ushort x, in byte z)
    {
        return x / Math.Pow(2.0, z) * 360.0 - 180;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToLatitude(in ushort y, in byte z)
    {
        var n = Math.PI - 2.0 * Math.PI * y / Math.Pow(2.0, z);
        return Math.Atan(Math.Sinh(n)) * (180 / Math.PI);
        ;
    }

    /// <summary>
    ///     Calculates a grid x and grid y position from certain geocoordinates based on the zoom.
    /// </summary>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Grid ToGrid(in double lat, in double lon, in byte zoom)
    {
        var xtile = (int)Math.Floor((lon + 180.0) / 360.0 * (1 << zoom));
        var ytile = (int)Math.Floor((1.0 - Math.Log(Math.Tan(lat * (Math.PI / 180)) + 1.0 / Math.Cos(lat * (Math.PI / 180))) / 3.141592653589793) / 2.0 * (1 << zoom));

        if (xtile < 0) xtile = 0;
        if (xtile >= 1 << zoom) xtile = (1 << zoom) - 1;
        if (ytile < 0) ytile = 0;
        if (ytile >= 1 << zoom) ytile = (1 << zoom) - 1;

        return new Grid((ushort)xtile, (ushort)ytile);
    }

    /// <summary>
    ///     Converts a grid position into a <see cref="Tile" /> based on a zoom
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Tile GridToTile(in int x, in int y, in byte zoom)
    {
        var north = ToLatitude(in y, in zoom);
        var south = ToLatitude(y + 1, in zoom);

        var west = ToLongitude(in x, in zoom);
        var east = ToLongitude(x + 1, in zoom);

        var range = new Vector2d((float)(north - south), (float)(east - west));
        var middle = new Vector2d((float)north - range.X / 2, (float)west + range.Y / 2);

        return new Tile(north, south, east, west, range, middle);
    }

    /// <summary>
    ///     Converts a grid position into a <see cref="Tile" /> based on a zoom
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Tile GridToTile(in Grid grid, in byte zoom)
    {
        var north = ToLatitude(in grid.Y, in zoom);
        var south = ToLatitude(grid.Y + 1, in zoom);

        var west = ToLongitude(in grid.X, in zoom);
        var east = ToLongitude(grid.X + 1, in zoom);

        var range = new Vector2d((float)(north - south), (float)(east - west));
        var middle = new Vector2d((float)north - range.X / 2, (float)west + range.Y / 2);

        return new Tile(north, south, west, east, range, middle);
    }

    /// <summary>
    ///     Converts a grid position into a <see cref="Tile" /> based on a zoom
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Tile ToTile(this ref Grid grid, in byte zoom)
    {
        var north = ToLatitude(in grid.Y, in zoom);
        var south = ToLatitude(grid.Y + 1, in zoom);

        var west = ToLongitude(in grid.X, in zoom);
        var east = ToLongitude(grid.X + 1, in zoom);

        var range = new Vector2d((float)(north - south), (float)(east - west));
        var middle = new Vector2d((float)north - range.X / 2, (float)west + range.Y / 2);

        return new Tile(north, south, west, east, range, middle);
    }

    /// <summary>
    ///     Converts a <see cref="Tile" /> into its <see cref="Grid" />
    /// </summary>
    /// <param name="tile"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Grid TileToGrid(in Tile tile, in byte zoom)
    {
        return ToGrid(tile.Middle.X, tile.Middle.Y, zoom);
    }

    /// <summary>
    ///     Converts a <see cref="Tile" /> into its <see cref="Grid" />
    /// </summary>
    /// <param name="tile"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Grid ToGrid(this ref Tile tile, in byte zoom)
    {
        return ToGrid(tile.Middle.X, tile.Middle.Y, zoom);
    }

    /// <summary>
    ///     Converts geocoordinates directly into a tile
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Tile GeoToTile(in double x, in double y, in byte zoom)
    {
        var coordinates = ToGrid(in x, in y, in zoom);
        return GridToTile(coordinates.X, coordinates.Y, zoom);
    }

    /// <summary>
    ///     Returns a random position from inside the <see cref="Tile" />
    /// </summary>
    /// <param name="tile"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d RandomInTile(ref this Tile tile)
    {
        var randomX = RandomExtensions.GetRandom((float)tile.North, (float)tile.South);
        var randomY = RandomExtensions.GetRandom((float)tile.East, (float)tile.West);

        return new Vector2d { X = randomX, Y = randomY };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 GeoToMeters(in double lat, in double lon)
    {
        var posx = lon * ORIGIN_SHIFT / 180;
        var posy = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
        posy = posy * ORIGIN_SHIFT / 180;

        return new Vector2((float)posx, (float)posy);
    }
}