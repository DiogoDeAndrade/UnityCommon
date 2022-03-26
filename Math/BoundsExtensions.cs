using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BoundsExtensions
{
    public static Vector3 GetCorner(this Bounds b, int idx)
    {
        switch (idx)
        {
            case 0: return new Vector3(b.min.x, b.min.y, b.min.z);
            case 1: return new Vector3(b.min.x, b.min.y, b.max.z);
            case 2: return new Vector3(b.max.x, b.min.y, b.min.z);
            case 3: return new Vector3(b.max.x, b.min.y, b.max.z);
            case 4: return new Vector3(b.min.x, b.max.y, b.min.z);
            case 5: return new Vector3(b.min.x, b.max.y, b.max.z);
            case 6: return new Vector3(b.max.x, b.max.y, b.min.z);
            case 7: return new Vector3(b.max.x, b.max.y, b.max.z);
        }

        return b.center;    
    }

    public static Bounds ConvertToLocal(this Bounds b, Transform transform)
    {
        var corner0 = b.GetCorner(0).xyz1();
        Bounds localBounds = new Bounds(transform.worldToLocalMatrix * corner0, Vector3.zero);
        for (int i = 1; i < 8; i++)
        {
            localBounds.Encapsulate(transform.worldToLocalMatrix * b.GetCorner(i).xyz1());
        }

        return localBounds;
    }
};
