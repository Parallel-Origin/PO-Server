using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using ParallelOrigin.Core.ECS.Components.Environment;
using QuadTrees.Common;
using QuadTrees.QTreePointF;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the <see cref="Aoi" /> and <see cref="QuadTrees" />
/// </summary>
public static class AoiExtensions
{
    /// <summary>
    ///     A functional lambda which simply checks if the referenced object is valid for some condition.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public delegate bool Predicate<T>(ref T obj);

    /// <summary>
    ///     Extracted out of a quadtree as an internal to modify certain keys and quadtree nodes like the move method does
    /// </summary>
    public static Dictionary<QuadEntity, QuadTreeObject<QuadEntity, QuadTreePointFNode<QuadEntity>>> WrappedDictionary { get; set; }

    /// <summary>
    ///     Calculates how many entities with a certain component <see cref="T" /> exist in the <see cref="aoi" />
    /// </summary>
    /// <param name="aoi"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EntitiesWith<T>(this IEnumerable<Entity> aoi) where T : struct
    {
        var delta = 0;
        foreach (var quadEntity in aoi)
        {
            ref readonly var entity = ref quadEntity;
            if (entity.Has<T>())
                delta++;
        }

        return delta;
    }
}