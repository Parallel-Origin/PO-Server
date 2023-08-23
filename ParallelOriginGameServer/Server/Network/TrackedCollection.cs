using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ParallelOrigin.Core.Network;

namespace ParallelOriginGameServer.Server.Network;

/// <summary>
///     The tracked item
/// </summary>
/// <typeparam name="T"></typeparam>
public struct TrackedListItem<T>
{
    public State State;
    public int Index;
    public T Item;

    public TrackedListItem(State state, int index, T item)
    {
        this.State = state;
        this.Index = index;
        this.Item = item;
    }
}

/// <summary>
///     The tracked item
/// </summary>
/// <typeparam name="T"></typeparam>
public struct TrackedDictionaryItem<K, V>
{
    public State State;
    public K Key;
    public V Val;

    public TrackedDictionaryItem(State state, K key, V val)
    {
        this.State = state;
        this.Key = key;
        this.Val = val;
    }
}

/// <summary>
///     A list which tracks the item states... mostly for network operation but can also be usefull in other cases.
/// </summary>
/// <typeparam name="T"></typeparam>
public class TrackedList<T> : IList<T>
{
    public TrackedList(int capacity)
    {
        List = new List<T>(capacity);
        Added = new List<TrackedListItem<T>>(4);
        Updated = new List<TrackedListItem<T>>(4);
        Removed = new List<TrackedListItem<T>>(4);
    }

    public List<TrackedListItem<T>> Added { get; set; }
    public List<TrackedListItem<T>> Updated { get; set; }
    public List<T> List { get; set; }
    public List<TrackedListItem<T>> Removed { get; set; }

    public void Add(T item)
    {
        List.Add(item);
        Added.Add(new TrackedListItem<T>(State.Added, List.Count - 1, item));
    }

    public void Insert(int index, T item)
    {
        if (List.Count - 1 == index)
            Added.Add(new TrackedListItem<T>(State.Added, index, item));

        if (List.Count - 1 > index)
            Updated.Add(new TrackedListItem<T>(State.Updated, index, item));

        List.Insert(index, item);
    }


    public bool Contains(T item)
    {
        return List.Contains(item);
    }

    public int IndexOf(T item)
    {
        return List.IndexOf(item);
    }

    public bool Remove(T item)
    {
        var index = List.IndexOf(item);
        if (index < 0) return false;

        Removed.Add(new TrackedListItem<T>(State.Removed, index, item));
        List.RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        var previousItem = List[index];
        List.RemoveAt(index);
        Removed.Add(new TrackedListItem<T>(State.Removed, index, previousItem));
    }

    public T this[int index]
    {
        get => List[index];
        set
        {
            List[index] = value;
            Updated.Add(new TrackedListItem<T>(State.Updated, index, value));
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        List.CopyTo(array, arrayIndex);
    }

    public void Clear()
    {
        List.Clear();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return List.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => List.Count;
    public bool IsReadOnly { get; }

    public void ClearTracked()
    {
        Added.Clear();
        Updated.Clear();
        Removed.Clear();
    }
}

/// <summary>
///     A dictionary which tracks its items... mostly used for networking.
/// </summary>
/// <typeparam name="K"></typeparam>
/// <typeparam name="V"></typeparam>
public class TrackedDictionary<K, V> : IDictionary<K, V>
{
    public TrackedDictionary(int capacity)
    {
        Dictionary = new Dictionary<K, V>(capacity);
        Tracked = new List<TrackedDictionaryItem<K, V>>(4);
    }

    public Dictionary<K, V> Dictionary { get; set; }
    public List<TrackedDictionaryItem<K, V>> Tracked { get; set; }

    public void Add(KeyValuePair<K, V> item)
    {
        Dictionary.Add(item.Key, item.Value);
        Tracked.Add(new TrackedDictionaryItem<K, V>(State.Added, item.Key, item.Value));
    }

    public void Add(K key, V value)
    {
        Dictionary.Add(key, value);
        Tracked.Add(new TrackedDictionaryItem<K, V>(State.Added, key, value));
    }


    public bool Contains(KeyValuePair<K, V> item)
    {
        return Dictionary.Contains(item);
    }

    public bool ContainsKey(K key)
    {
        return Dictionary.ContainsKey(key);
    }

    public bool TryGetValue(K key, out V value)
    {
        return Dictionary.TryGetValue(key, out value);
    }

    public bool Remove(KeyValuePair<K, V> item)
    {
        var contains = Dictionary.ContainsKey(item.Key);
        if (contains) Tracked.Add(new TrackedDictionaryItem<K, V>(State.Removed, item.Key, item.Value));

        return Dictionary.Remove(item.Key);
    }

    public bool Remove(K key)
    {
        var contains = Dictionary.ContainsKey(key);
        if (contains) Tracked.Add(new TrackedDictionaryItem<K, V>(State.Removed, key, default));

        return Dictionary.Remove(key);
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
    {
        return Dictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
    }

    public void Clear()
    {
        Dictionary.Clear();
    }

    public V this[K key]
    {
        get => Dictionary[key];
        set
        {
            if (!Dictionary.ContainsKey(key)) Tracked.Add(new TrackedDictionaryItem<K, V>(State.Added, key, value));
            else Tracked.Add(new TrackedDictionaryItem<K, V>(State.Updated, key, value));

            Dictionary[key] = value;
        }
    }

    public int Count => Dictionary.Count;
    public bool IsReadOnly { get; }

    public ICollection<K> Keys => Dictionary.Keys;
    public ICollection<V> Values => Dictionary.Values;

    public void ClearTracked()
    {
        Tracked.Clear();
    }
}