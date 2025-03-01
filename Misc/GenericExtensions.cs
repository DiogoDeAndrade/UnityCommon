using System;
using System.Collections.Generic;
using System.Linq;

public static class GenericExtensions
{
    static public T Random<T>(this List<T> l, bool withReplacement = true)
    {
        if ((l == null) || (l.Count == 0)) return default(T);

        var idx = UnityEngine.Random.Range(0, l.Count);
        var ret = l[idx];

        if (!withReplacement)
        {
            l.RemoveAt(idx);
        }

        return ret;
    }

    static public T Random<T>(this List<T> l, System.Random rnd, bool withReplacement = true)
    {
        if ((l == null) || (l.Count == 0)) return default(T);

        var idx = rnd.Next() % l.Count;
        var ret = l[idx];

        if (!withReplacement)
        {
            l.RemoveAt(idx);
        }

        return ret;
    }

    static public void Resize<T>(this List<T> l, int newSize)
    {
        if (newSize > l.Count)
        {
            for (int i = l.Count; i < newSize; i++) l.Add(default(T));
        }
        else if (newSize < l.Count)
        {
            while (l.Count > newSize) l.RemoveAt(l.Count - 1);
        }
    }

    static public T PopLast<T>(this List<T> l)
    {
        if (l.Count == 0) return default(T);

        var ret = l[l.Count - 1];
        l.RemoveAt(l.Count - 1);
        return ret;
    }

    static public T PopFirst<T>(this List<T> l)
    {
        if (l.Count == 0) return default(T);

        var ret = l[0];
        l.RemoveAt(0);
        return ret;
    }

    static public void Replace<T>(this List<T> l, T find, T replace) where T : IEquatable<T>
    {
        if (l == null) return;
        for (int i = 0; i < l.Count; i++)
        {
            if (l[i].Equals(find))
            {
                l[i] = replace;
            }
        }
    }

    static public void RemoveAll<K, V>(this Dictionary<K, V> dictionary, Func<KeyValuePair<K, V>, bool> criteria)
    {
        // First, create a list of keys whose values are null.
        List<K> keysToRemove = dictionary
            .Where(criteria)
            .Select(kvp => kvp.Key)
            .ToList();

        // Now, remove each key from the dictionary.
        foreach (K key in keysToRemove)
        {
            dictionary.Remove(key);
        }
    }
}
