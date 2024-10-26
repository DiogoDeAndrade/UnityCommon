using System.Collections.Generic;

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
}
