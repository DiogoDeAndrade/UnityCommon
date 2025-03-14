using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class VectorExtensions
{
    public static Vector3 x0z(this Vector3 inV)
    {
        return new Vector3(inV.x, 0.0f, inV.z);
    }
    public static Vector3 xy0(this Vector3 inV)
    {
        return new Vector3(inV.x, inV.y, 0.0f);
    }

    public static Vector2 xz(this Vector3 inV)
    {
        return new Vector2(inV.x, inV.z);
    }

    public static Vector2 xy(this Vector3 inV)
    {
        return new Vector2(inV.x, inV.y);
    }

    public static Vector3 xy0(this Vector2 inV)
    {
        return new Vector3(inV.x, inV.y, 0);
    }

    public static Vector3 xyz(this Vector2 inV, float z)
    {
        return new Vector3(inV.x, inV.y, z);
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

    public static float Random(this Vector2 limits)
    {
        return UnityEngine.Random.Range(limits.x, limits.y);
    }

    public static Vector2 RandomXY(this Vector2 v)
    {
        return new Vector2(UnityEngine.Random.Range(0.0f, v.x), UnityEngine.Random.Range(0.0f, v.y));
    }


    public static float Random(this Vector2 limits, System.Random random)
    {
        return random.Range(limits.x, limits.y);
    }

    public static Vector2 RandomXY(this Vector2 v, System.Random randomGenerator)
    {
        return new Vector2(randomGenerator.Range(0.0f, v.x), randomGenerator.Range(0.0f, v.y));
    }


    public static int Random(this Vector2Int limits)
    {
        return UnityEngine.Random.Range(limits.x, limits.y + 1);
    }

    public static Vector2Int RandomXY(this Vector2Int v)
    {
        return new Vector2Int(UnityEngine.Random.Range(0, v.x), UnityEngine.Random.Range(0, v.y));
    }

    public static Vector2Int RandomXY(this Vector2Int v, System.Random randomGenerator)
    {
        return new Vector2Int(randomGenerator.Range(0, v.x), randomGenerator.Range(0, v.y));
    }

    public static int Random(this Vector2Int limits, System.Random random)
    {
        return random.Range(limits.x, limits.y + 1);
    }

    public static Vector3 xyz(this Vector4 v)
    {
        return new Vector3(v.x, v.y, v.z);
    }

    public static Vector4 xyz0(this Vector3 v)
    {
        return new Vector4(v.x, v.y, v.z, 0);
    }

    public static Vector4 xyz1(this Vector3 v)
    {
        return new Vector4(v.x, v.y, v.z, 1);
    }

    public static void SafeNormalize(this Vector2 v)
    {
        if (v.sqrMagnitude > 1e-3)
        {
            v.Normalize();
        }
        else
        {
            v.Set(0, 0);
        }
    }

    public static Vector2 SafeNormalized(this Vector2 v)
    {
        if (v.sqrMagnitude > 1e-3)
        {
            return v.normalized;
        }
        else
        {
            return Vector2.zero;
        }
    }

    public static void SafeNormalize(this Vector3 v)
    {
        if (v.sqrMagnitude > 1e-3)
        {
            v.Normalize();
        }
        else
        {
            v.Set(0, 0, 0);
        }
    }
    public static Vector3 SafeNormalized(this Vector3 v)
    {
        if (v.sqrMagnitude > 1e-3)
        {
            return v.normalized;
        }
        else
        {
            return Vector3.zero;
        }
    }

    public static void SafeNormalize(this Vector4 v)
    {
        if (v.sqrMagnitude > 1e-3)
        {
            v.Normalize();
        }
        else
        {
            v.Set(0, 0, 0, 0);
        }
    }
    public static Vector4 SafeNormalized(this Vector4 v)
    {
        if (v.sqrMagnitude > 1e-3)
        {
            return v.normalized;
        }
        else
        {
            return Vector4.zero;
        }
    }

    public static Vector3 RotateZ(this Vector3 v, float angle)
    {
        float a = Mathf.Deg2Rad * angle;
        float c = Mathf.Cos(a);
        float s = Mathf.Sin(a);

        var x = c * v.x - s * v.y;
        var y = s * v.x + c * v.y;

        return new Vector3(x, y, v.z);
    }

    public static Vector2 Perpendicular(this Vector2 v)
    {
        return new Vector2(-v.y, v.x);
    }
    public static Vector3 PerpendicularXY(this Vector3 v)
    {
        return new Vector3(-v.y, v.x, v.z);
    }

    public static Vector2Int xy(this Vector3Int v)
    {
        return new Vector2Int(v.x, v.y);
    }

    public static Vector3Int xy0(this Vector2Int v)
    {
        return new Vector3Int(v.x, v.y, 0);
    }

    public static Vector2Int toInt(this Vector2 v)
    {
        return new Vector2Int((int)v.x, (int)v.y);
    }
    public static Vector3Int toInt(this Vector3 v)
    {
        return new Vector3Int((int)v.x, (int)v.y, (int)v.z);
    }
};

