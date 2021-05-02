using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TransformExtensions
{
    public static Vector3 Center(this Transform t)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr)
        {
            return sr.bounds.center;
        }

        return t.position;
    }
}
