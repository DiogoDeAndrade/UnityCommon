using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class SDFExtrusion : SDF
    {
        public Vector3      offset = Vector3.zero;            
        public Quaternion   rotation = Quaternion.identity;   
        public List<Line>   segments;                         
        public PathXY       extrusionShape;                   
        public Vector2      extrusionShapeScale = Vector2.one;
        [ShowIf(nameof(needsThickness))]
        public float        thickness = 0.5f;

        bool needsThickness => (extrusionShape == null) || (!extrusionShape.isClosed);

        private static void TransformEndpointsLocalToWorld(Line src, Vector3 off, Quaternion rot, Matrix4x4 ownerLocalToWorld, out Vector3 p0W, out Vector3 p1W)
        {
            // apply local rotation+offset to endpoints, then promote to world through the owner's transform
            Vector3 p0L = rot * src.p0 + off;
            Vector3 p1L = rot * src.p1 + off;
            p0W = ownerLocalToWorld.MultiplyPoint3x4(p0L);
            p1W = ownerLocalToWorld.MultiplyPoint3x4(p1L);
        }

        public override Bounds GetBounds()
        {
            if ((segments == null) || (segments.Count == 0) || (extrusionShape == null))
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var profile = extrusionShape.GetPoints();
            if ((profile == null) || (profile.Count == 0))
            { 
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Matrix4x4 M = (ownerGameObject != null) ? ownerGameObject.transform.localToWorldMatrix : Matrix4x4.identity;

            Bounds bounds = new Bounds();
            bool first = true;

            foreach (var seg in segments)
            {
                TransformEndpointsLocalToWorld(seg, offset, rotation, M, out var aW, out var bW);

                // build a temporary world-space line so we can reuse GetTangentSpace
                Line wLine = new Line { p0 = aW, p1 = bW };

                float[] ts = { 0f, 0.5f, 1f };
                foreach (float t in ts)
                {
                    var (center, dir, up, right) = wLine.GetTangentSpace(t);  

                    foreach (var p in profile)
                    {
                        Vector2 ps = new Vector2(p.x * extrusionShapeScale.x, p.y * extrusionShapeScale.y);
                        Vector3 worldP = center + ps.x * right + ps.y * up;

                        if (first) 
                        { 
                            bounds = new Bounds(worldP, Vector3.zero); 
                            first = false; 
                        }
                        else 
                        { 
                            bounds.Encapsulate(worldP); 
                        }
                    }
                }
            }

            if (!extrusionShape.isClosed) bounds.Expand(thickness * 2.0f);

            return bounds;  
        }

        public override float Sample(Vector3 worldPoint)
        {
            if ((segments == null) || (segments.Count == 0))
            {
                return float.PositiveInfinity;
            }

            Matrix4x4 M = (ownerGameObject != null) ? ownerGameObject.transform.localToWorldMatrix : Matrix4x4.identity;

            List<Vector3> profile = null;
            bool hasClosedProfile = false;

            if (extrusionShape != null)
            {
                profile = extrusionShape.GetPoints();
                hasClosedProfile = extrusionShape.isClosed;
            }

            float best = float.PositiveInfinity;

            foreach (var seg in segments)
            {
                TransformEndpointsLocalToWorld(seg, offset, rotation, M, out var aW, out var bW);

                Vector3 T = bW - aW;
                float L = T.magnitude;
                if (L < 1e-6f) continue;
                Vector3 dir = T / L;

                Vector3 cp; // Closest point on segment
                LineHelpers.Distance(aW, bW, worldPoint, out cp);

                // Build an "up-ish" frame (dir = tangent, up = world up projected, right = dir × up)
                Vector3 worldUp = Vector3.up;
                Vector3 up = worldUp - Vector3.Dot(worldUp, dir) * dir;
                if (up.sqrMagnitude < 1e-6f) // near-vertical fallback
                {
                    worldUp = Vector3.forward;
                    up = worldUp - Vector3.Dot(worldUp, dir) * dir;
                }
                up.Normalize();
                Vector3 right = Vector3.Cross(dir, up).normalized;
                // Re-orthogonalize up for numerical stability
                up = Vector3.Cross(right, dir).normalized;

                Vector3 rel = worldPoint - cp;
                Vector2 q2 = new Vector2(Vector3.Dot(rel, right), Vector3.Dot(rel, up));

                float d2D;

                if ((profile != null) && (profile.Count > 0))
                {
                    if (hasClosedProfile)
                    {
                        d2D = SignedDistanceToPolygon2D(q2, profile, extrusionShapeScale);
                    }
                    else
                    {
                        d2D = DistanceToPolyline2D(q2, profile, extrusionShapeScale) - thickness;
                    }
                }
                else
                {
                    // No profile provided -> treat as a circular tube with radius = thickness
                    d2D = q2.magnitude - thickness;
                }

                if (d2D < best) best = d2D;
            }

            return best;
        }

        // Distance from a point to an open polyline (scaled)
        private static float DistanceToPolyline2D(in Vector2 p, List<Vector3> poly, in Vector2 scale)
        {
            float best = float.PositiveInfinity;

            if (poly.Count == 1)
            {
                Vector2 a = new Vector2(poly[0].x * scale.x, poly[0].y * scale.y);
                return Vector2.Distance(p, a);
            }

            for (int i = 1; i < poly.Count; ++i)
            {
                Vector2 a = new Vector2(poly[i - 1].x * scale.x, poly[i - 1].y * scale.y);
                Vector2 b = new Vector2(poly[i].x * scale.x, poly[i].y * scale.y);

                // point -> segment distance in 2D
                Vector2 ab = b - a;
                float ab2 = Vector2.Dot(ab, ab);
                if (ab2 < 1e-12f)
                {
                    best = Mathf.Min(best, Vector2.Distance(p, a));
                    continue;
                }
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
                Vector2 c = a + t * ab;
                best = Mathf.Min(best, Vector2.Distance(p, c));
            }
            return best;
        }

        // Signed distance to a closed polygon (scaled):
        // outside -> positive, inside -> negative. Uses edge distance with even-odd inside test.
        private static float SignedDistanceToPolygon2D(in Vector2 p, List<Vector3> poly, in Vector2 scale)
        {
            int n = poly.Count;
            if (n == 0) return float.PositiveInfinity;

            float dist = float.PositiveInfinity;

            bool inside = false;

            // We iterate edges (i -> j), where j = (i+1) % n.
            for (int i = 0, j = n - 1; i < n; j = i, ++i)
            {
                Vector2 Pi = new Vector2(poly[i].x * scale.x, poly[i].y * scale.y);
                Vector2 Pj = new Vector2(poly[j].x * scale.x, poly[j].y * scale.y);

                // Edge distance
                Vector2 e = Pj - Pi;
                float e2 = Vector2.Dot(e, e);
                float t = (e2 > 1e-12f) ? Mathf.Clamp01(Vector2.Dot(p - Pi, e) / e2) : 0f;
                Vector2 c = Pi + t * e;
                dist = Mathf.Min(dist, Vector2.Distance(p, c));

                // Inside test (ray cast on Y)
                // Toggle inside if edge straddles the horizontal ray to the right of p.
                bool cond = ((Pi.y > p.y) != (Pj.y > p.y)) &&
                            (p.x < (Pj.x - Pi.x) * (p.y - Pi.y) / (Pj.y - Pi.y + Mathf.Epsilon) + Pi.x);
                if (cond) inside = !inside;
            }

            return inside ? -dist : dist;
        }

#if UNITY_6000_0_OR_NEWER
        public override void DrawGizmos()
        {
            if (ownerGameObject == null)
            {
                Debug.LogWarning($"No owner object on SDFExtrusion {name}, cannot draw gizmos.");
                return;
            }

            if (segments == null || segments.Count == 0) return;

            Matrix4x4 M = ownerGameObject.transform.localToWorldMatrix;

            foreach (var seg in segments)
            {
                TransformEndpointsLocalToWorld(seg, offset, rotation, M, out var aW, out var bW);

                // draw the segment
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(aW, bW);

                if (!extrusionShape) continue;

                int count = 10;
                float tInc = 1.0f / (count - 1);
                var prev = Handles.matrix;

                Line wLine = new Line { p0 = aW, p1 = bW };

                for (int i = 0; i < count; ++i)
                {
                    float t = i * tInc;
                    var (center, dir, up, right) = wLine.GetTangentSpace(t);  // world-space basis. 

                    var R = Quaternion.LookRotation(dir, up);
                    var S = new Vector3(extrusionShapeScale.x, extrusionShapeScale.y, 1f);
                    Handles.matrix = Matrix4x4.TRS(center, R, S);

                    Handles.color = Color.cyan;
                    extrusionShape.DrawHandles();
                }

                Handles.matrix = prev;
            }
        }
#endif
    }
}
