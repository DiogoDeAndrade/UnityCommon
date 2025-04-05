using UnityEngine;

namespace UC
{

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

        public static Bounds ToLocal(this Bounds b, Transform transform)
        {
            var corner0 = b.GetCorner(0).xyz1();
            Bounds localBounds = new Bounds(transform.worldToLocalMatrix * corner0, Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                localBounds.Encapsulate(transform.worldToLocalMatrix * b.GetCorner(i).xyz1());
            }

            return localBounds;
        }

        public static Bounds ToWorld(this Bounds b, Transform transform)
        {
            var min = b.min;
            var max = b.max;

            var corner = b.GetCorner(0);
            corner = transform.TransformPoint(corner);
            Bounds worldBounds = worldBounds = new Bounds(corner, Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                corner = transform.TransformPoint(b.GetCorner(i));
                worldBounds.Encapsulate(corner);
            }
            return worldBounds;
        }

        public static bool ContainsMinInclusive(this Bounds b, Vector3 p)
        {
            return ((b.min.x <= p.x) && (b.max.x > p.x) &&
                    (b.min.y <= p.y) && (b.max.y > p.y) &&
                    (b.min.z <= p.z) && (b.max.z > p.z));
        }

        public static bool IntersectTriangle(this Bounds b, Triangle triangle)
        {
            return AABB.Intersects(b.min, b.max, triangle.GetVertex(0), triangle.GetVertex(1), triangle.GetVertex(2));
        }

        public static Vector3 Random(this Bounds b)
        {
            return Vector3.right * UnityEngine.Random.Range(b.min.x, b.max.x) +
                   Vector3.up * UnityEngine.Random.Range(b.min.y, b.max.y) +
                   Vector3.forward * UnityEngine.Random.Range(b.min.z, b.max.z);
        }

        public static void DrawGizmo(this Bounds b)
        {
            Vector3[] p = new Vector3[8];

            for (int i = 0; i < 8; i++) p[i] = b.GetCorner(i);

            Gizmos.DrawLine(p[0], p[1]);
            Gizmos.DrawLine(p[1], p[3]);
            Gizmos.DrawLine(p[3], p[2]);
            Gizmos.DrawLine(p[2], p[0]);

            Gizmos.DrawLine(p[4], p[5]);
            Gizmos.DrawLine(p[5], p[7]);
            Gizmos.DrawLine(p[7], p[6]);
            Gizmos.DrawLine(p[6], p[4]);

            Gizmos.DrawLine(p[0], p[4]);
            Gizmos.DrawLine(p[1], p[5]);
            Gizmos.DrawLine(p[2], p[6]);
            Gizmos.DrawLine(p[3], p[7]);
        }

    };
}