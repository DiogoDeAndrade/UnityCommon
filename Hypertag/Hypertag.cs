using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Hypertag")]
public class Hypertag : ScriptableObject
{
    public static T FindObjectOfType<T>(Hypertag tag) where T : Component
    {
        var objects = FindObjectsOfType<HypertaggedObject>();
        foreach (var obj in objects)
        {
            if (obj.hypertag == tag)
            {
                var ret = obj.GetComponent<T>();

                return ret;
            }
        }

        return null;
    }

    public static List<T> FindObjectsOfType<T>(Hypertag tag) where T : Component
    {
        List<T> ret = new List<T>();
        var objects = FindObjectsOfType<HypertaggedObject>();
        foreach (var obj in objects)
        {
            if (obj.hypertag == tag)
            {
                var c = obj.GetComponent<T>();
                if (c)
                {
                    ret.Add(c);
                }
            }
        }

        return ret;
    }
}