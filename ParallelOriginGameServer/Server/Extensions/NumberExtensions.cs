using System.Runtime.CompilerServices;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     A class adding extensions to several number based primitives
/// </summary>
public static class NumberExtensions
{
    /// <summary>
    ///     Checks if the primitive number is between two other values.
    /// </summary>
    /// <param name="number"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Between(this ref float number, float min, float max)
    {
        return number > min && number < max;
    }
}