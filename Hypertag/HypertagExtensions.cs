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
}
