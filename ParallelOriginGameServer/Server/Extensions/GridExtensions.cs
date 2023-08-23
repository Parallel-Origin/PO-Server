using System.Collections.Generic;
using ParallelOrigin.Core.Base.Classes;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     Stores extensions for the <see cref="Grid" /> and possible even <see cref="Chunk" />
/// </summary>
public static class GridExtensions
{
    /// <summary>
    ///     Fills a passed list with all grids surrounding the current grid.
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="range"></param>
    /// <param name="surroundingGrids"></param>
    public static HashSet<Grid> GetSurroundingGrids(this in Grid grid, in byte range)
    {
        var surroundingGrids = new HashSet<Grid>((range * 2 + 1) ^ 2);
        for (var index = grid.X - range; index <= grid.X + range; index++)
        for (var yindex = grid.Y - range; yindex <= grid.Y + range; yindex++)
        {
            var surroundingGrid = new Grid((ushort)index, (ushort)yindex);
            surroundingGrids.Add(surroundingGrid);
        }

        return surroundingGrids;
    }

    /// <summary>
    ///     Fills a passed list with all grids surrounding the current grid.
    /// </summary>
    /// <param name="grid"></param>
    /// <param name="range"></param>
    /// <param name="surroundingGrids"></param>
    public static void GetSurroundingGrids(this ref Grid grid, in byte range, ref List<Grid> surroundingGrids)
    {
        surroundingGrids.Clear();
        for (var index = grid.X; index < grid.X + range; index++)
        for (var yindex = grid.Y; yindex < grid.Y + range; yindex++)
        {
            var surroundingGrid = new Grid(index, yindex);
            surroundingGrids.Add(surroundingGrid);
        }
    }
}