using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParallelOrigin.Core.Base.Classes;
using ParallelOriginGameServer.Server.Persistence;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     Contains extensions for the <see cref="GameDbContext" /> which target the environment of the game.
/// </summary>
public static class PersistenceEnvironmentExtensions
{
    /// <summary>
    ///     Checks which chunks exist and returns them in an list.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="grids"></param>
    /// <returns></returns>
    public static Task<HashSet<Grid>> ChunksExist(this GameDbContext context, HashSet<Grid> grids)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            foreach (var grid in grids)
                sb.Append('(').Append(grid.X).Append(',').Append(grid.Y).Append(')').Append(',');

            sb.Length--;

            var query = $"select X,Y from chunk where (X,Y) in({sb})";
            var existingGrids = context.Chunks.FromSqlRaw(query).IgnoreAutoIncludes().Select(chunk => new Grid(chunk.X, chunk.Y)).ToHashSet();
            return existingGrids;
        });
    }

    /// <summary>
    ///     Searches for all chunks in the list and returns them if being found in the database.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="grids"></param>
    /// <returns></returns>
    public static Task<HashSet<Chunk>> GetChunks(this GameDbContext context, HashSet<Grid> grids)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            foreach (var grid in grids)
                sb.Append('(').Append(grid.X).Append(',').Append(grid.Y).Append(')').Append(',');

            if (sb.Length == 0) return new HashSet<Chunk>(0);
            sb.Length--;

            var query = $"select * from chunk where (X,Y) in({sb})";
            var chunks = context.Chunks.FromSqlRaw(query).Take(grids.Count).AsSplitQuery().ToHashSet();
            return chunks;
        });
    }

    /// <summary>
    ///     Searches an chunk by its x/y pair and return it properly.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    public static Chunk GetChunk(this GameDbContext context, ushort x, ushort y)
    {
        var chunk = context.Chunks.Where(chunk => chunk.X == x && chunk.Y == y).Take(1).FirstOrDefault();
        return chunk;
    }
}