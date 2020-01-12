using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static public class VectorExtensions
{
    public static Vector3 x0z(this Vector3 inV)
    {
        return new Vector3(inV.x, 0.0f, inV.z);
    }

    public static Vector2 xz(this Vector3 inV)
    {
        return new Vector2(inV.x, inV.z);
    }

    public static Vector2 xy(this Vector3 inV)
    {
        return new Vector2(inV.x, inV.y);
    }

    public static Vector2 yz(this Vector3 inV)
    {
        return new Vector2(inV.y, inV.z);
    }

    public static Vector2 zx(this Vector3 inV)
    {
        return new Vector2(inV.z, inV.x);
    }

    public static Vector2 yx(this Vector3 inV)
    {
        return new Vector2(inV.y, inV.x);
    }

    public static Vector2 zy(this Vector3 inV)
    {
        return new Vector2(inV.z, inV.y);
    }
};

