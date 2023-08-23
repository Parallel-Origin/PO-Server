using System;
using System.Collections.Generic;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     A class containing dictionary extensions
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    ///     Only updates/sets the value if it either does not exist in the dic or if it differs from the previous value.
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <param name="val"></param>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    /// <returns>True if the value was set or updated, otherwhise false</returns>
    public static bool SetOnce<K, V>(this IDictionary<K, V> dictionary, K key, V val) where V : IEquatable<V>
    {
        if (dictionary.ContainsKey(key) && dictionary[key].Equals(val)) return false;

        dictionary[key] = val;
        return true;
    }
}