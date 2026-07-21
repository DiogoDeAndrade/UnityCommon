using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

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

        public static Vector3 TransformNormal(this Matrix4x4 matrix, Vector3 normal)
        {
            const float epsilon = 1e-8f;
            const float epsilonSq = epsilon * epsilon;

            if (normal.sqrMagnitude <= epsilonSq) return normal;

            Vector3 transformedNormal;

            if (Mathf.Abs(matrix.determinant) > epsilon)
            {
                transformedNormal = matrix.inverse.transpose.MultiplyVector(normal);
            }
            else
            {
                // Degenerate fallback. This is not mathematically exact, but avoids
                // invalid values if an axis has been scaled to approximately zero.
                transformedNormal = matrix.MultiplyVector(normal);
            }

            if (transformedNormal.sqrMagnitude <= epsilonSq)
            {
                return normal.normalized;
            }

            return transformedNormal.normalized;
        }

        public static double GetMatrixAxisLength(this Matrix4x4 matrix, int column)
        {
            Vector4 axis = matrix.GetColumn(column);

            return Math.Sqrt(axis.x * axis.x + axis.y * axis.y + axis.z * axis.z);
        }

        public static Quaternion ExtractMatrixRotation(this Matrix4x4 matrix)
        {
            const float epsilon = 1e-8f;

            Vector3 forward = matrix.GetColumn(2);
            Vector3 up = matrix.GetColumn(1);

            if (forward.sqrMagnitude < epsilon)
                forward = Vector3.forward;
            else
                forward.Normalize();

            // Remove possible scale/shear contamination and ensure that up is perpendicular to forward.
            up = Vector3.ProjectOnPlane(up, forward);

            if (up.sqrMagnitude < epsilon)
            {
                Vector3 fallback = (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.95f) ? (Vector3.up) : (Vector3.right);

                up = Vector3.ProjectOnPlane(fallback, forward);
            }

            up.Normalize();

            return Quaternion.LookRotation(forward, up);
        }
    }
}