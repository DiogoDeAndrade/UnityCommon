using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HypertaggedExtension
{
    public static HypertaggedObject FindObjectWithHypertag(this Object go, Hypertag tag)
    {
        var objects = Object.FindObjectsByType<HypertaggedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var obj in objects)
        {
            if (obj.hypertag == tag) return obj;
        }

        return null;
    }

    public static T FindObjectOfTypeWithHypertag<T>(this Object go, Hypertag tag) where T : Component
    {
        var objects = Object.FindObjectsByType<HypertaggedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

    public static List<T> FindObjectsOfTypeWithHypertag<T>(this Object go, Hypertag tag) where T : Component
    {
        List<T> ret = new List<T>();
        var objects = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var obj in objects)
        {
            if (obj.HasHypertag(tag)) ret.Add(obj);
        }

        return ret;
    }
    public static void FindObjectsOfTypeWithHypertag<T>(this Object go, Hypertag tag, List<T> ret) where T : Component
    {
        var objects = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var obj in objects)
        {
            if (obj.HasHypertag(tag)) ret.Add(obj);
        }
    }

    public static T FindObjectOfTypeWithHypertag<T>(this MonoBehaviour go, Hypertag tag) where T : Component
    {
        var objects = Object.FindObjectsByType<HypertaggedObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

    public static bool HasHypertag(this Component go, Hypertag tag)
    {
        var obj = go.GetComponent<HypertaggedObject>();
        if (obj == null) return false;

        return obj.hypertag == tag;
    }
}
