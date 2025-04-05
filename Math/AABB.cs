using UnityEngine;

namespace UC
{

    public static class AABB
    {
        // Adapted from https://github.com/mattatz/unity-intersections
        public static bool Intersects(Vector3 aabb_min, Vector3 aabb_max, Vector3 tri_p1, Vector3 tri_p2, Vector3 tri_p3)
        {
            float p0, p1, p2, r;

            Vector3 center = (aabb_min + aabb_max) * 0.5f;
            Vector3 extents = aabb_max - center + new Vector3(1e-3f, 1e-3f, 1e-3f);

            Vector3 v0 = tri_p1 - center,
                    v1 = tri_p2 - center,
                    v2 = tri_p3 - center;

            Vector3 f0 = v1 - v0,
                    f1 = v2 - v1,
                    f2 = v0 - v2;

            Vector3 a00 = new Vector3(0, -f0.z, f0.y),
                    a01 = new Vector3(0, -f1.z, f1.y),
                    a02 = new Vector3(0, -f2.z, f2.y),
                    a10 = new Vector3(f0.z, 0, -f0.x),
                    a11 = new Vector3(f1.z, 0, -f1.x),
                    a12 = new Vector3(f2.z, 0, -f2.x),
                    a20 = new Vector3(-f0.y, f0.x, 0),
                    a21 = new Vector3(-f1.y, f1.x, 0),
                    a22 = new Vector3(-f2.y, f2.x, 0);

            // Test axis a00
            p0 = Vector3.Dot(v0, a00);
            p1 = Vector3.Dot(v1, a00);
            p2 = Vector3.Dot(v2, a00);
            r = extents.y * Mathf.Abs(f0.z) + extents.z * Mathf.Abs(f0.y);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a01
            p0 = Vector3.Dot(v0, a01);
            p1 = Vector3.Dot(v1, a01);
            p2 = Vector3.Dot(v2, a01);
            r = extents.y * Mathf.Abs(f1.z) + extents.z * Mathf.Abs(f1.y);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a02
            p0 = Vector3.Dot(v0, a02);
            p1 = Vector3.Dot(v1, a02);
            p2 = Vector3.Dot(v2, a02);
            r = extents.y * Mathf.Abs(f2.z) + extents.z * Mathf.Abs(f2.y);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a10
            p0 = Vector3.Dot(v0, a10);
            p1 = Vector3.Dot(v1, a10);
            p2 = Vector3.Dot(v2, a10);
            r = extents.x * Mathf.Abs(f0.z) + extents.z * Mathf.Abs(f0.x);
            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a11
            p0 = Vector3.Dot(v0, a11);
            p1 = Vector3.Dot(v1, a11);
            p2 = Vector3.Dot(v2, a11);
            r = extents.x * Mathf.Abs(f1.z) + extents.z * Mathf.Abs(f1.x);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a12
            p0 = Vector3.Dot(v0, a12);
            p1 = Vector3.Dot(v1, a12);
            p2 = Vector3.Dot(v2, a12);
            r = extents.x * Mathf.Abs(f2.z) + extents.z * Mathf.Abs(f2.x);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a20
            p0 = Vector3.Dot(v0, a20);
            p1 = Vector3.Dot(v1, a20);
            p2 = Vector3.Dot(v2, a20);
            r = extents.x * Mathf.Abs(f0.y) + extents.y * Mathf.Abs(f0.x);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a21
            p0 = Vector3.Dot(v0, a21);
            p1 = Vector3.Dot(v1, a21);
            p2 = Vector3.Dot(v2, a21);
            r = extents.x * Mathf.Abs(f1.y) + extents.y * Mathf.Abs(f1.x);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            // Test axis a22
            p0 = Vector3.Dot(v0, a22);
            p1 = Vector3.Dot(v1, a22);
            p2 = Vector3.Dot(v2, a22);
            r = extents.x * Mathf.Abs(f2.y) + extents.y * Mathf.Abs(f2.x);

            if (Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r)
            {
                return false;
            }

            if (Mathf.Max(v0.x, v1.x, v2.x) < -extents.x || Mathf.Min(v0.x, v1.x, v2.x) > extents.x)
            {
                return false;
            }

            if (Mathf.Max(v0.y, v1.y, v2.y) < -extents.y || Mathf.Min(v0.y, v1.y, v2.y) > extents.y)
            {
                return false;
            }

            if (Mathf.Max(v0.z, v1.z, v2.z) < -extents.z || Mathf.Min(v0.z, v1.z, v2.z) > extents.z)
            {
                return false;
            }


            var normal = Vector3.Cross(f1, f0).normalized;
            return Intersects(aabb_min, aabb_max, normal, Vector3.Dot(normal, tri_p1));
        }

        // Adapted from https://github.com/mattatz/unity-intersections
        public static bool Intersects(Vector3 aabb_min, Vector3 aabb_max, Vector3 plane_normal, float plane_dist)
        {
            Vector3 center = (aabb_min + aabb_max) * 0.5f,
                    extents = aabb_max - center + new Vector3(1e-3f, 1e-3f, 1e-3f);

            var r = extents.x * Mathf.Abs(plane_normal.x) + extents.y * Mathf.Abs(plane_normal.y) + extents.z * Mathf.Abs(plane_normal.z);
            var s = Vector3.Dot(plane_normal, center) - plane_dist;

            return Mathf.Abs(s) <= r;
        }
    }
}