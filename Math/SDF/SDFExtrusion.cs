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

            // Convert all segments to world space
            List<Line> worldSegments = new();
            foreach (var seg in segments)
            {
                TransformEndpointsLocalToWorld(seg, offset, rotation, M, out var aW, out var bW);

                worldSegments.Add(new Line { p0 = aW, p1 = bW });
            }

            // Check all segments points and updated bounds appropriately
            foreach (var seg in worldSegments)
            {
                float[] ts = { 0f, 0.5f, 1f };
                foreach (float t in ts)
                {
                    var tangentFrame = seg.GetTangentSpace(t);  

                    foreach (var p in profile)
                    {
                        Vector3 worldP = tangentFrame.center + p.x * tangentFrame.right + p.y * tangentFrame.up;

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

            // Convert all segments to world space, and find closest one
            Line        closestSegment = null;
            float       closestDistanceToSegment = float.PositiveInfinity;
            List<Line>  worldSegments = new();
            foreach (var seg in segments)
            {
                TransformEndpointsLocalToWorld(seg, offset, rotation, M, out var aW, out var bW);

                var worldLine = new Line { p0 = aW, p1 = bW };
                worldSegments.Add(worldLine);

                float d = worldLine.GetDistance(worldPoint);
                if (d < closestDistanceToSegment)
                {
                    closestSegment = worldLine;
                    closestDistanceToSegment = d;
                }
            }

            // Found the best segment, now compute distance to profile at that segment/point
            float bestDistance = float.PositiveInfinity;

            Vector3 cp; // Closest point on segment
            cp = closestSegment.GetClosestPoint(worldPoint, out float t);

            var tangentFrame = closestSegment.GetTangentSpace(t);

            var q2 = tangentFrame.WorldToFrame(worldPoint);

            float distance = 0.0f;

            if ((t < 1e-6) || (t > (1.0f - 1e-6)))
            {
                // We're at an endpoint of the segment, now let's check if there's other segments that share this endpoint
                List<Line> sharedLines = new();
                sharedLines.Add(closestSegment);
                foreach (var seg in worldSegments)
                {
                    if (seg == closestSegment) continue;
                    if ((Vector3.Distance(seg.p0, cp) < 1e-6) || (Vector3.Distance(seg.p1, cp) < 1e-6))
                    {
                        sharedLines.Add(seg);
                    }
                }

                if (sharedLines.Count > 1)
                {
                    // Reference frame: from the closest segment at the clamped endpoint - use this as the basic "up"
                    var refFrame = closestSegment.GetTangentSpace(Mathf.Clamp01(t));
                    Vector3 refUp = refFrame.up;

                    // Build contributors (line dir + its endpoint frame)
                    var contributors = new List<(Line line, TangentFrame frame)>();
                    foreach (var sline in sharedLines)
                    {
                        // Get a stable frame for this line at the endpoint that equals 'vertex'
                        float tEnd = ((sline.p0 - cp).sqrMagnitude <= 1e-9f) ? 0f : 1f;
                        var tf = sline.GetTangentSpace(tEnd);

                        contributors.Add((sline, tf));
                    }

                    tangentFrame = WeightedMergeFrames(cp, contributors, refUp, worldPoint);

                    q2 = tangentFrame.WorldToFrame(worldPoint);

                    if (extrusionShape == null)
                    {
                        // Make extrusion shape be a circle with the given thickness
                        float r = tangentFrame.WorldToFrame(worldPoint).magnitude;
                        distance = r - thickness;
                    }
                    else
                    {
                        distance = extrusionShape.GetSignedDistance(q2);
                        if (!extrusionShape.isClosed) distance -= thickness;
                    }
                }
                else
                {
                    tangentFrame = closestSegment.GetTangentSpace(Mathf.Clamp01(t));

                    q2 = tangentFrame.WorldToFrame(worldPoint);
                    float axialDistance = GetAxialAbsDistance(tangentFrame, worldPoint);

                    if (extrusionShape != null)
                    {
                        // Closest point is on an endpoint of a segment, do distance from the plane itself
                        if (extrusionShape.IsInside(q2, thickness))
                        {
                            // Point is inside the defined profile
                            distance = axialDistance;
                        }
                        else
                        {
                            if (extrusionShape.isClosed)
                            {
                                // Outside closed polygon: distance to closest rim point in plane, then Euclidean to world
                                var pt2d = extrusionShape.GetClosestPoint(q2);
                                var rim = tangentFrame.FrameToWorld(pt2d);
                                distance = Vector3.Distance(rim, worldPoint);
                            }
                            else
                            {
                                // Outside open+thickness set:
                                // planar signed distance to curve, shrink by thickness, then combine with axial
                                float d2 = extrusionShape.GetSignedDistance(q2) - thickness;
                                distance = Mathf.Sqrt(d2 * d2 + axialDistance * axialDistance);
                            }
                        }
                    }
                    else
                    {
                        // There's no extrusion shape, treat cross-section as a *filled disk* of radius = thickness.
                        float r = tangentFrame.WorldToFrame(worldPoint).magnitude;

                        if (r <= thickness)
                        {
                            // inside the disk => distance is to the cap plane
                            distance = axialDistance;
                        }
                        else
                        {
                            // outside the disk => distance is to the rim circle in 3D
                            float dr = r - thickness;
                            distance = Mathf.Sqrt(dr * dr + axialDistance * axialDistance);
                        }
                    }
                }
            }
            else
            {
                if (extrusionShape == null)
                {
                    // Make extrusion shape be a circle with the given thickness
                    float r = tangentFrame.WorldToFrame(worldPoint).magnitude;
                    distance = r - thickness;
                }
                else
                {
                    distance = extrusionShape.GetSignedDistance(q2);
                    if (!extrusionShape.isClosed) distance -= thickness;
                }
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
            }

            return bestDistance;
        }

        float GetAxialAbsDistance(TangentFrame tangentFrame, Vector3 p)
        {
            return Mathf.Abs(Vector3.Dot(p - tangentFrame.center, tangentFrame.dir));
        }

        static TangentFrame WeightedMergeFrames(Vector3 vertex, List<(Line line, TangentFrame frame)> contributors, Vector3 refUp, Vector3 samplePoint)
        
        {
            // Unit vector from vertex to sample (used only for angle-based weights)
            Vector3 u = samplePoint - vertex;
            if (u.sqrMagnitude < 1e-12f) u = contributors[0].frame.dir;
            u.Normalize();

            // --- Hemisphere alignment to a reference direction (first frame) ---
            Vector3 refDir = contributors[0].frame.dir.normalized;

            int n = contributors.Count;
            var dirs = new Vector3[n];
            var ups = new Vector3[n];
            for (int i = 0; i < n; ++i)
            {
                Vector3 di = contributors[i].frame.dir.normalized;
                Vector3 ui = contributors[i].frame.up;

                // Flip into the same hemisphere as refDir to avoid wrap-around
                if (Vector3.Dot(di, refDir) < 0f) { di = -di; ui = -ui; }

                dirs[i] = di;
                ups[i] = ui;
            }

            // --- Inverse-angle weights (scale-free) ---
            const float eps = 1e-6f;
            const float p = 2.0f; // 1..3 reasonable
            float sumW = 0f;
            var w = new float[n];
            for (int i = 0; i < n; ++i)
            {
                float theta = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dirs[i], u), -1f, 1f)); // radians
                float wi = Mathf.Pow(eps + theta, -p);
                w[i] = wi; sumW += wi;
            }
            if (sumW <= 0f) { for (int i = 0; i < n; ++i) w[i] = 1f / n; }
            else { for (int i = 0; i < n; ++i) w[i] /= sumW; }

            // --- Blend direction: SLERP for 2, normalized sum for >2 ---
            Vector3 dirBlend;
            if (n == 2)
            {
                float dot = Mathf.Clamp(Vector3.Dot(dirs[0], dirs[1]), -1f, 1f);
                float omega = Mathf.Acos(dot);
                float t = w[1] / (w[0] + w[1] + 1e-9f);
                if (omega < 1e-5f)
                    dirBlend = (dirs[0] + t * (dirs[1] - dirs[0])).normalized;
                else
                {
                    float s0 = Mathf.Sin((1f - t) * omega) / Mathf.Sin(omega);
                    float s1 = Mathf.Sin(t * omega) / Mathf.Sin(omega);
                    dirBlend = (s0 * dirs[0] + s1 * dirs[1]).normalized;
                }
            }
            else
            {
                Vector3 d = Vector3.zero;
                for (int i = 0; i < n; ++i) d += w[i] * dirs[i];
                dirBlend = (d.sqrMagnitude > 1e-12f) ? d.normalized : dirs[0];
            }

            // --- Blend up: rotate each up so its dir -> dirBlend, sign-align to refUp, average, orthonormalize ---
            Vector3 upBlend = Vector3.zero;
            for (int i = 0; i < n; ++i)
            {
                Vector3 ui = RotateVectorFromTo(ups[i], dirs[i], dirBlend);
                if (Vector3.Dot(ui, refUp) < 0f) ui = -ui;
                upBlend += w[i] * ui;
            }
            if (upBlend.sqrMagnitude < 1e-12f) upBlend = refUp;
            upBlend.Normalize();

            Vector3 rightBlend = Vector3.Cross(dirBlend, upBlend);
            if (rightBlend.sqrMagnitude < 1e-12f)
            {
                rightBlend = Vector3.Cross(dirBlend, Vector3.right);
                if (rightBlend.sqrMagnitude < 1e-8f) rightBlend = Vector3.Cross(dirBlend, Vector3.up);
            }
            rightBlend.Normalize();
            upBlend = Vector3.Cross(rightBlend, dirBlend).normalized;

            return new TangentFrame
            {
                center = vertex,
                dir = dirBlend,
                up = upBlend,
                right = rightBlend
            };
        }


        // Minimal rotation that maps `fromDir` onto `toDir` and applies it to `v`.
        static Vector3 RotateVectorFromTo(Vector3 v, Vector3 fromDir, Vector3 toDir)
        {
            fromDir = fromDir.normalized; toDir = toDir.normalized;
            float c = Mathf.Clamp(Vector3.Dot(fromDir, toDir), -1f, 1f);
            if (c > 0.9999f) return v;                    // nearly identical
            if (c < -0.9999f)                              // 180 degrees flip: pick any axis perpendicular fromDir
            {
                Vector3 axis = fromDir.Perpendicular();
                return Quaternion.AngleAxis(180f, axis) * v;
            }
            Vector3 axisN = Vector3.Cross(fromDir, toDir).normalized;
            float angDeg = Mathf.Acos(c) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(angDeg, axisN) * v;
        }


        /*static float AngleWeight(Vector3 a, Vector3 b, float cutoffDeg = 70f, float softnessDeg = 10f)
        {
            // 1. Hard reject beyond cutoff
            float cosang = Mathf.Clamp(Vector3.Dot(a.normalized, b.normalized), -1f, 1f);
            float ang = Mathf.Acos(cosang) * Mathf.Rad2Deg;
            if (ang >= cutoffDeg + softnessDeg) return 0.0f;
            if (ang <= cutoffDeg - softnessDeg) return 1.0f;
            
            // 2. Smooth falloff near cutoff (cosine window)
            float t = Mathf.InverseLerp(cutoffDeg + softnessDeg, cutoffDeg - softnessDeg, ang);
            return 0.5f * (1f + Mathf.Cos(Mathf.PI * (1f - t)));
        }

        static float AxialFalloff(float s, float sigma) // s in world units from the vertex along the segment
        {
            // Gaussian-like weight; sigma = radius within which we want blending (e.g., thickness or a few voxels)
            float x = Mathf.Max(0f, s);
            return Mathf.Exp(-(x * x) / (2f * sigma * sigma));
        }

        static (Vector3 center, Vector3 dir, Vector3 up, Vector3 right) WeightedMergeFrames(Vector3 vertex, List<(Line line, Vector3 dir, (Vector3 center, Vector3 dir, Vector3 up, Vector3 right) frame)> contributors, Vector3 refUp, float axialSigma, Vector3 samplePoint)
        {
            // 1) dominant direction = normalized sum of outgoing dirs
            Vector3 Ddom = Vector3.zero;
            foreach (var c in contributors) Ddom += c.dir;
            Ddom.SafeNormalize();

            // 2) accumulate weighted frames
            Vector3 D = Vector3.zero, U = Vector3.zero, R = Vector3.zero;
            float W = 0f;

            foreach (var c in contributors)
            {
                // angle weight vs dominant
                float wa = AngleWeight(c.dir, Ddom); if (wa <= 0f) continue;

                // axial distance of the sample to the vertex along this segment
                var v2s = samplePoint - vertex;
                float s = Mathf.Abs(Vector3.Dot(v2s, c.dir)); // along outgoing dir, >=0 near the vertex

                float wd = AxialFalloff(s, axialSigma);

                float w = wa * wd; if (w <= 1e-6f) continue;

                // align up/right with a reference to avoid destructive cancellation
                Vector3 u = c.frame.up; if (Vector3.Dot(u, refUp) < 0f) u = -u;
                Vector3 r = c.frame.right; // keep right consistent with (dir, up) later

                D += w * c.dir;
                U += w * u;
                R += w * r;
                W += w;
            }

            if (W <= 1e-6f)
            {
                // fallback: just use dominant direction and reference up
                Vector3 up = refUp;
                Vector3 right;
                Orthonormalize(ref Ddom, ref up, out right);
                return (vertex, Ddom, up, right);
            }

            D /= W; U /= W; R /= W;

            // 3) Orthonormalize to ensure a clean right-handed frame
            Vector3 dir = D, up2 = U, right2;
            Orthonormalize(ref dir, ref up2, out right2);
            return (vertex, dir, up2, right2);
        }

        static void Orthonormalize(ref Vector3 dir, ref Vector3 up, out Vector3 right)
        {
            dir.SafeNormalize();
            // project up away from dir
            up = (up - Vector3.Dot(up, dir) * dir);
            up.SafeNormalize();
            right = Vector3.Cross(dir, up);
            right.SafeNormalize();

            // fix handedness if needed
            if (Vector3.Dot(Vector3.Cross(dir, up), right) < 0f) right = -right;
        }*/


#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
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
                    var tangentFrame = wLine.GetTangentSpace(t);  // world-space basis. 

                    var R = Quaternion.LookRotation(tangentFrame.dir, tangentFrame.up);
                    Handles.matrix = Matrix4x4.TRS(tangentFrame.center, R, Vector3.one);

                    Handles.color = Color.cyan;
                    extrusionShape.DrawHandles();
                }

                Handles.matrix = prev;
            }
        }
#endif
    }
}
