using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ParallelOrigin.Core.Base.Classes;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the buildin <see cref="System.Random" />
/// </summary>
public static class RandomExtensions
{
    //Function to get random number, prevent allocating one new random instance each time
    private static readonly Random Random = new();

    /// <summary>
    ///     Reuturns a random int between
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRandom(int min, int max)
    {
        lock (Random)
        {
            return Random.Next(min, max);
        }
    }

    /// <summary>
    ///     Returns a random float between
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetRandom(float min, float max)
    {
        lock (Random)
        {
            return (float)(Random.NextDouble() * (max - min) + min);
        }
    }

    /// <summary>
    ///     Returns a unique unsigned long
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetUniqueULong()
    {
        lock (Random)
        {
            var buffer = new byte[sizeof(ulong)];
            Random.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }

    /// <summary>
    ///     Returns a unique unsigned long
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetUniqueLong()
    {
        lock (Random)
        {
            var buffer = new byte[sizeof(ulong)];
            Random.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
    }

    /// <summary>
    ///     Returns a random <see cref="Vector2d" /> between two positions.
    /// </summary>
    /// <param name="leftDown"></param>
    /// <param name="rightUp"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d GetRandom(Vector2d leftDown, Vector2d rightUp)
    {
        var randomX = GetRandom((float)leftDown.X, (float)rightUp.X);
        var randomY = GetRandom((float)leftDown.Y, (float)rightUp.Y);

        return new Vector2d(randomX, randomY);
    }
    
    /// <summary>
    ///     Returns a random <see cref="Vector2d" /> within an intervall.
    /// </summary>
    /// <param name="leftDown"></param>
    /// <param name="rightUp"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d GetRandomVector2d(float min, float max)
    {
        var randomX = GetRandom((float)min, (float)max);
        var randomY = GetRandom((float)min, (float)max);

        return new Vector2d(randomX, randomY);
    }

    /// <summary>
    ///     Creates a random rotation
    /// </summary>
    /// <param name="random"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion Quaternion()
    {
        lock (Random)
        {
            var randomX = Random.Next(0, 360) * (Math.PI / 180);
            var randomY = Random.Next(0, 360) * (Math.PI / 180);
            var randomZ = Random.Next(0, 360) * (Math.PI / 180);

            return System.Numerics.Quaternion.CreateFromYawPitchRoll((float)randomX, (float)randomY, (float)randomZ);
        }
    }

    /// <summary>
    ///     Creates a random rotation
    /// </summary>
    /// <param name="random"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion QuaternionStanding()
    {
        lock (Random)
        {
            var randomX = Random.Next(0, 360) * (Math.PI / 180);
            return System.Numerics.Quaternion.CreateFromYawPitchRoll((float)randomX, 0, 0);
        }
    }
}