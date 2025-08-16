using System;
using UnityEditor;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class SDFPlane : SDF
    {
        public Vector3  normal = Vector3.up;
        public float    d;
        public Vector2  size = Vector2.one * 5.0f;

        public override Bounds GetBounds()
        {
            // Fallbacks and normalization
            Vector3 n = normal;
            if (n == Vector3.zero) n = Vector3.up;
            n.Normalize();

            // Build an orthonormal frame where:
            //   up = n (plane normal)
            //   forward = any perpendicular to n
            //   right = up x forward
            Vector3 up = n;
            Vector3 forward = n.Perpendicular().normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;

            // Half extents on each local axis (tiny thickness along normal)
            float hx = Mathf.Abs(size.x) * 0.5f;
            float hz = Mathf.Abs(size.y) * 0.5f;
            float hy = 0.005f; // small thickness to avoid zero-size bounds

            // Local-space center of the rectangle: move along normal by d
            Vector3 center = up * d;

            // 8 corners of the oriented thin box in local space
            Vector3[] corners = new Vector3[8];
            int k = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        corners[k++] = center
                            + right * (sx * hx)
                            + up * (sy * hy)
                            + forward * (sz * hz);
                    }

            // Enclose corners in a Bounds
            Bounds ret = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                ret.Encapsulate(corners[i]);

#if UNITY_6000_0_OR_NEWER
            // Convert from local to world using the owner transform (matches your DrawGizmos path)
            ret = ret.ToWorld(ownerGameObject.transform);
#endif
            return ret;
        }

        public override float Sample(Vector3 worldPoint)
        {
            return Vector3.Dot(ToLocalPoint(worldPoint), normal) - d;
        }

#if UNITY_6000_0_OR_NEWER
        public override void DrawGizmos()
        {
            Gizmos.matrix = ownerGameObject.transform.localToWorldMatrix * Matrix4x4.TRS(normal * d, Quaternion.LookRotation(normal.Perpendicular(), normal), Vector3.one);
            Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, 0.1f, size.y));
            DebugHelpers.DrawArrow(Vector3.zero, Vector3.up, Mathf.Min(size.x, size.y) * 0.5f, Mathf.Min(size.x, size.y) * 0.25f, Vector3.right);
        }
#endif
    }
}
