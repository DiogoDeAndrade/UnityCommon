using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Hypertag", menuName = "Unity Common/Hypertag")]
public class Hypertag : ScriptableObject
{
    public static T FindFirstObjectWithHypertag<T>(Hypertag tag) where T : Component
    {
        List<T> ret = new List<T>();

        var objects = HypertaggedObject.Get(tag);
        foreach (var obj in objects)
        {
            var c = obj.GetComponent<T>();
            if (c)
            {
                return c;
            }
        }

        return null;
    }

    public static List<T> FindObjectsWithHypertag<T>(Hypertag tag) where T : Component
    {
        List<T> ret = new List<T>();

        var objects = HypertaggedObject.Get(tag);
        foreach (var obj in objects)
        {
            var c = obj.GetComponent<T>();
            if (c)
            {
                ret.Add(c);
            }
        }

        return ret;
    }

    public static List<T> FindObjectsWithHypertag<T>(Hypertag[] tags) where T : Component
    {
        List<T> ret = new List<T>();

        var objects = HypertaggedObject.Get(tags);
        foreach (var obj in objects)
        {
            var c = obj.GetComponent<T>();
            if (c)
            {
                ret.Add(c);
            }
        }

        return ret;
    }
}
