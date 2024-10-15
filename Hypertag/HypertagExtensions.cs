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
}
