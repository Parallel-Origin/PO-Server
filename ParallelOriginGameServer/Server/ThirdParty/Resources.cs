using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Arch.LowLevel;

namespace ParallelOriginGameServer.Server.ThirdParty;

public static class Object<T> where T : new()
{
    public static T Instance = new T();
}

/// <summary>
///     Static implementation of <see cref="Arch.LowLevel.Resources{T}"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public static class Resources<T>
{
    /// <summary>
    ///     Shared instance.
    /// </summary>
    public static Arch.LowLevel.Resources<T> Shared = new(64);
}

public static class HandleExtensions
{
    /// <summary>
    ///     Checks whehter the <see cref="Handle{T}"/> is null.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <typeparam name="T">The resource.</typeparam>
    /// <returns>True if its null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNull<T>(this in Handle<T> handle)
    {
        return handle.Id == -1;
    }
    
    /// <summary>
    ///     Returns the assigned resource of a <see cref="Handle{T}"/>.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <typeparam name="T">The resource.</typeparam>
    /// <returns>The resource.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get<T>(this in Handle<T> handle)
    {
        return Resources<T>.Shared.Get(in handle);
    }
    
    /// <summary>
    ///     Returns the assigned resource of a <see cref="Handle{T}"/>.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <typeparam name="T">The resource.</typeparam>
    /// <returns>The resource.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Remove<T>(this in Handle<T> handle)
    {
        Resources<T>.Shared.Remove(in handle);
    }
    
    /// <summary>
    ///     Registers a resource and returns a handle to it.
    /// </summary>
    /// <param name="resource">The resource instance.</param>
    /// <typeparam name="T">The resource.</typeparam>
    /// <returns>The <see cref="Handle{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Handle<T> ToHandle<T>(this T resource)
    {
        return Resources<T>.Shared.Add(in resource);
    }
}