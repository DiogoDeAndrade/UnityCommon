using UnityEngine;

namespace UC
{

    public static class Line
    {
        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        public static bool Approximately(float a, float b, float tolerance = 1e-5f)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }

        public static float CrossProduct2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - b.x * a.y;
        }

        public static bool Intersect2d(Vector3 p1start, Vector3 p1end, Vector3 p2start, Vector3 p2end, out Vector3 intersection)
        {
            Vector2 i = Vector2.zero;
            bool ret = Intersect(p1start.xy(), p1end.xy(), p2start.xy(), p2end.xy(), out i);

            intersection = i;

            return ret;
        }

        public static bool Intersect(Vector2 p1start, Vector2 p1end, Vector2 p2start, Vector2 p2end, out Vector2 intersection)
        {
            // Consider:
            //   p1start = p
            //   p1end = p + r
            //   p2start = q
            //   p2end = q + s
            // We want to find the intersection point where :
            //  p + t*r == q + u*s
            // So we need to solve for t and u
            var p = p1start;
            var r = p1end - p1start;
            var q = p2start;
            var s = p2end - p2start;
            var qminusp = q - p;

            float cross_rs = CrossProduct2D(r, s);

            if (Approximately(cross_rs, 0f))
            {
                // Parallel lines
                if (Approximately(CrossProduct2D(qminusp, r), 0f))
                {
                    // Co-linear lines, could overlap
                    float rdotr = Vector2.Dot(r, r);
                    float sdotr = Vector2.Dot(s, r);
                    // this means lines are co-linear
                    // they may or may not be overlapping
                    float t0 = Vector2.Dot(qminusp, r / rdotr);
                    float t1 = t0 + sdotr / rdotr;
                    if (sdotr < 0)
                    {
                        // lines were facing in different directions so t1 > t0, swap to simplify check
                        Swap(ref t0, ref t1);
                    }

                    if (t0 <= 1 && t1 >= 0)
                    {
                        // Nice half-way point intersection
                        float t = Mathf.Lerp(Mathf.Max(0, t0), Mathf.Min(1, t1), 0.5f);
                        intersection = p + t * r;
                        return true;
                    }
                    else
                    {
                        // Co-linear but disjoint
                        intersection = Vector2.zero;
                        return false;
                    }
                }
                else
                {
                    // Just parallel in different places, cannot intersect
                    intersection = Vector2.zero;
                    return false;
                }
            }
            else
            {
                // Not parallel, calculate t and u
                float t = CrossProduct2D(qminusp, s) / cross_rs;
                float u = CrossProduct2D(qminusp, r) / cross_rs;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
                {
                    intersection = p + t * r;
                    return true;
                }
                else
                {
                    // Lines only cross outside segment range
                    intersection = Vector2.zero;
                    return false;
                }
            }
        }

        /*public static bool Intersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, out Vector3 intersection)
        {
            Vector3 lineVec3 = b1 - a1;
            Vector3 lineVec1 = a2 - a1;
            Vector3 lineVec2 = b2 - b1;
            Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
            Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

            float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

            //is coplanar, and not parallel
            if ((Mathf.Abs(planarFactor) < 0.0001f) && (crossVec1and2.sqrMagnitude > 0.0001f))
            {
                float s1 = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;

                if ((s1 >= 0) && (s1 <= 1))
                {
                    float s2 = Vector3.Dot(Vector3.Cross(-lineVec3, lineVec1), -crossVec1and2) / crossVec1and2.sqrMagnitude;

                    intersection = b1 + (lineVec2 * Mathf.Max(0.0f, Mathf.Min(s2, 1.0f)));

                    return (s2 >= 0) && (s2 <= 1);
                }
                else
                {
                    intersection = a1 + (lineVec1 * Mathf.Max(0.0f, Mathf.Min(s1, 1.0f)));
                    return false;
                }
            }
            else
            {
                intersection = Vector3.zero;
                return false;
            }
        }*/

        public static bool Raycast(Vector3 origin, Vector3 dir, float range, Vector3 p0, Vector3 p1, out Vector3 intersection, out float tRay)
        {
            Vector3 da = dir * range;  // Unnormalized direction of the ray
            Vector3 db = p1 - p0;
            Vector3 dc = p0 - origin;

            intersection = origin;
            tRay = float.MaxValue;

            if (Mathf.Abs(Vector3.Dot(dc, Vector3.Cross(da, db))) >= 0.01f) // Lines are not coplanar
                return false;

            float s = Vector3.Dot(Vector3.Cross(dc, db), Vector3.Cross(da, db)) / Vector3.Cross(da, db).sqrMagnitude;

            if (s >= 0.0 && s <= 1.0)   // Means we have an intersection
            {
                intersection = origin + s * da;
                tRay = s * range;

                // See if this lies on the segment
                if ((intersection - p0).sqrMagnitude + (intersection - p1).sqrMagnitude <= (p0 - p1).sqrMagnitude + 1e-3)
                {
                    return true;
                }
            }

            return false;
        }
        public static bool Raycast(Vector3 origin, Vector3 dir, float range, Vector3 p0, Vector3 p1, out Vector3 intersection, out float tRay, out float tLine)
        {
            Vector3 da = dir * range;  // Unnormalized direction of the ray
            Vector3 db = p1 - p0;
            Vector3 dc = p0 - origin;

            intersection = origin;
            tRay = float.MaxValue;
            tLine = float.MaxValue;

            if (Mathf.Abs(Vector3.Dot(dc, Vector3.Cross(da, db))) >= 0.01f) // Lines are not coplanar
                return false;

            float s = Vector3.Dot(Vector3.Cross(dc, db), Vector3.Cross(da, db)) / Vector3.Cross(da, db).sqrMagnitude;

            if (s >= 0.0 && s <= 1.0)   // Means we have an intersection
            {
                intersection = origin + s * da;
                tRay = s * range;

                // See if this lies on the segment
                tLine = Vector3.Dot(intersection - p0, db.normalized) / db.magnitude;
                if ((tLine >= 0) && (tLine <= 1))
                {
                    return true;
                }
            }

            return false;
        }

        public static float Distance(Vector3 p0, Vector3 p1, Vector3 p)
        {
            Vector3 ab = p1 - p0;
            Vector3 av = p - p0;

            if (Vector3.Dot(av, ab) <= 0)
            {
                return ab.magnitude;
            }
            Vector3 bv = p - p1;
            if (Vector3.Dot(bv, ab) >= 0)
            {
                return bv.magnitude;
            }

            return Vector3.Cross(ab, av).magnitude / ab.magnitude;
        }

        public static Vector3 GetClosestPoint(Vector3 p0, Vector3 p1, Vector3 p)
        {
            Vector3 dp = (p1 - p0).normalized;
            float len = (p1 - p0).magnitude;
            float t = Mathf.Clamp(Vector3.Dot(dp, p - p0), 0, len);

            return p0 + dp * t;
        }
    }
}