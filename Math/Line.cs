using System;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class Line
    {
        public Vector3 p0;
        public Vector3 p1;

        public void Invert()
        {
            Vector3 tmp = p0;
            p0 = p1;
            p1 = tmp;
        }

        public void To2D()
        {
            p0.z = 0.0f;
            p1.z = 0.0f;
        }

        public float GetDistance(Vector3 p)
        {
            return LineHelpers.Distance(p0, p1, p);
        }

        public float GetDistance(Vector3 p, out Vector3 closestPoint)
        {
            return LineHelpers.Distance(p0, p1, p, out closestPoint);
        }

        public Vector3 GetClosestPoint(Vector3 p)
        {
            return LineHelpers.GetClosestPoint(p0, p1, p);
        }

        public bool Intersect(Line otherLine, out Vector3 intersection)
        {
            return LineHelpers.Intersect2d(p0, p1, otherLine.p0, otherLine.p1, out intersection);
        }

        public bool Intersect(Vector3 otherP0, Vector3 otherP1, out Vector3 intersection)
        {
            return LineHelpers.Intersect2d(p0, p1, otherP0, otherP1, out intersection);
        }

        public bool Raycast(Vector3 origin, Vector3 dir, float range, Vector3 p0, Vector3 p1, out Vector3 intersection, out float tRay)
        {
            return LineHelpers.Raycast(origin, dir, range, p0, p1, out intersection, out tRay);
        }

        public bool Raycast(Vector3 origin, Vector3 dir, float range, Vector3 p0, Vector3 p1, out Vector3 intersection, out float tRay, out float tLine)
        {
            return LineHelpers.Raycast(origin, dir, range, p0, p1, out intersection, out tRay, out tLine);
        }

        public (Vector3 center, Vector3 dir, Vector3 up, Vector3 right) GetTangentSpace(float t)
        {
            // Clamp t for safety
            t = Mathf.Clamp01(t);

            // Center point along the line
            Vector3 center = Vector3.Lerp(p0, p1, t);

            // Tangent (dir) is along the segment
            Vector3 dir = (p1 - p0).normalized;
            if (dir.sqrMagnitude < 1e-6f)
            {
                // Degenerate line: return defaults
                return (center, Vector3.forward, Vector3.up, Vector3.right);
            }

            // Try to align up with world-up, but keep orthogonal to dir
            Vector3 worldUp = Vector3.up;
            Vector3 up = worldUp - Vector3.Dot(worldUp, dir) * dir;

            // If dir is nearly vertical, use another fallback axis
            if (up.sqrMagnitude < 1e-6f)
            {
                worldUp = Vector3.forward;
                up = worldUp - Vector3.Dot(worldUp, dir) * dir;
            }

            up.Normalize();

            // Right vector is orthogonal to both dir and up
            Vector3 right = Vector3.Cross(dir, up).normalized;

            // Recompute up for full orthogonality (stable frame)
            up = Vector3.Cross(right, dir).normalized;

            return (center, dir, up, right);
        }


    }
}
