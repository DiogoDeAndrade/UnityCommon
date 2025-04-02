using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HypertaggedExtension
{
    public static bool HasHypertag(this Component go, Hypertag tag)
    {
        foreach (var obj in go.GetComponents<HypertaggedObject>())
        {
            if (obj.hypertag == tag) return true;
        }

        return false;
    }
    public static bool HasHypertags(this Component go, Hypertag[] tags)
    {
        foreach (var obj in go.GetComponents<HypertaggedObject>())
        {
            if (obj.HasHypertags(tags)) return true;
        }

        return false;
    }

    public static T GetComponentInChildrenWithHypertag<T>(this Component go, Hypertag tag) where T : Component
    {
        T obj = go.GetComponentInChildren<T>();
        if (obj == null) return null;

        if (obj.HasHypertag(tag)) return obj;

        return null;
    }

    public static T GetComponentInChildrenWithHypertag<T>(this Component go, Hypertag[] tags) where T : Component
    {
        T obj = go.GetComponentInChildren<T>();
        if (obj == null) return null;

        if (obj.HasHypertags(tags)) return obj;

        return null;
    }
}
