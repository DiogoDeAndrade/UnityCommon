using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MatrixExtensions 
{
    public static Vector3[] TransformPositions(this Matrix4x4 matrix, Vector3[] src)
    {
        Vector3[] ret = new Vector3[src.Length];

        for (int i = 0; i < src.Length; i++)
        {
            ret[i] = matrix * src[i].xyz1();
        }

        return ret;
    }

    public static Vector3[] TransformDirection(this Matrix4x4 matrix, Vector3[] src)
    {
        Vector3[] ret = new Vector3[src.Length];

        for (int i = 0; i < src.Length; i++)
        {
            ret[i] = matrix * src[i].xyz0();
        }

        return ret;
    }


    public static Vector4[] TransformTangents(this Matrix4x4 matrix, Vector4[] src)
    {
        Vector4[] ret = new Vector4[src.Length];

        for (int i = 0; i < src.Length; i++)
        {
            var tmp = matrix * src[i].xyz().xyz0();
            ret[i] = new Vector4(tmp.x, tmp.y, tmp.z, src[i].w);
        }

        return ret;
    }
}
