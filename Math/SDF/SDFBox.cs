using System;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class SDFBox : SDF
    {
        public Vector3      offset;
        public Quaternion   rotation = Quaternion.identity;
        public Vector3      size = Vector3.one * 4.0f;

        public override Bounds GetBounds()
        {
            // Build the local TRS used in DrawGizmos
            var localTrs = Matrix4x4.TRS(offset, rotation, Vector3.one);

            // Half extents (abs handles negative size inputs gracefully)
            Vector3 he = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f;

            // Oriented box corners in local SDFBox space (box is centered at origin in that space)
            // Then move them by the local TRS (offset/rotation)
            Vector3[] corners = new Vector3[8];
            int k = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 c = new Vector3(sx * he.x, sy * he.y, sz * he.z);
                        corners[k++] = localTrs.MultiplyPoint3x4(c);
                    }

            // Enclose in a Bounds in the SDF's local space
            Bounds ret = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                ret.Encapsulate(corners[i]);

#if UNITY_6000_0_OR_NEWER
            // Convert from SDF local to world using the owner transform, matching DrawGizmos
            ret = ret.ToWorld(ownerGameObject.transform);
#endif
            return ret;
        }

        public override float Sample(Vector3 worldPoint)
        {
            // Transform world point into the box's local space (same TRS as DrawGizmos)
            Matrix4x4 ownerToWorld = ownerGameObject.transform.localToWorldMatrix;
            Matrix4x4 localTrs = Matrix4x4.TRS(offset, rotation, Vector3.one);
            Matrix4x4 boxToWorld = ownerToWorld * localTrs;
            Matrix4x4 worldToBox = boxToWorld.inverse;

            Vector3 p = worldToBox.MultiplyPoint3x4(worldPoint);

            // Box SDF with half extents
            Vector3 b = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f;

            // Standard SDF for a box (IQ style)
            Vector3 q = new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z)) - b;
            float outside = new Vector3(Mathf.Max(q.x, 0.0f), Mathf.Max(q.y, 0.0f), Mathf.Max(q.z, 0.0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0.0f);
            return outside + inside;
        }


#if UNITY_6000_0_OR_NEWER
        public override void DrawGizmos()
        {
            if (ownerGameObject == null)
            {
                Debug.LogWarning($"No owner object on SDFBox {name}, cannot draw gizmos.");
                return;
            }
            Gizmos.matrix = ownerGameObject.transform.localToWorldMatrix * Matrix4x4.TRS(offset, rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
        }
#endif
    }
}
