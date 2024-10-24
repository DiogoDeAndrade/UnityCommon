using System.Collections.Generic;

public static class GenericExtensions
{
    static public T Random<T>(this List<T> l) 
    {
        if ((l == null) || (l.Count == 0)) return default(T);
        
        return l[UnityEngine.Random.Range(0, l.Count)];
    }
}
