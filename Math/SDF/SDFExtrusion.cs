using NaughtyAttributes;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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
                    var (center, dir, up, right) = seg.GetTangentSpace(t);  

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

            (var center, var dir, var up, var right) = closestSegment.GetTangentSpace(t);

            Vector3 rel = worldPoint - center;
            Vector2 q2 = new Vector2(Vector3.Dot(rel, right), Vector3.Dot(rel, up));

            // Rescale q2 based on scale
            if (extrusionShapeScale.x != 0.0f) q2.x /= extrusionShapeScale.x;
            else q2.x = 0.0f;
            if (extrusionShapeScale.y != 0.0f) q2.y /= extrusionShapeScale.y;
            else q2.y = 0.0f;

            float distance = 0.0f;
            if (extrusionShape == null)
            {
                // Make extrusion shape be a circle with the given thickness
                distance = q2.magnitude - thickness;
            }
            else
            {
                distance = extrusionShape.GetSignedDistance(q2);
                if (!extrusionShape.isClosed) distance -= thickness;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                /*bestLine = seg;
                bestPoint = cp;
                bestT = t;
                bestPlaneNormal = dir;*/
            }

            // Find closest point on any segment
            /*float   bestDistance = float.PositiveInfinity;
            Line    bestLine = null;
            Vector3 bestPoint = Vector3.zero;
            float   bestT = 0.0f;
            Vector3 bestPlaneNormal = Vector2.zero;

            foreach (var seg in worldSegments)
            {
                Vector3 T = seg.p1 - seg.p0;
                float L = T.magnitude;
                if (L < 1e-6f) continue;

                Vector3 cp; // Closest point on segment
                cp = seg.GetClosestPoint(worldPoint, out float t);

                (var center, var dir, var up, var right) = seg.GetTangentSpace(t);

                Vector3 rel = worldPoint - center;
                Vector2 q2 = new Vector2(Vector3.Dot(rel, right), Vector3.Dot(rel, up));

                // Rescale q2 based on scale
                if (extrusionShapeScale.x != 0.0f) q2.x /= extrusionShapeScale.x;
                else q2.x = 0.0f;
                if (extrusionShapeScale.y != 0.0f) q2.y /= extrusionShapeScale.y;
                else q2.y = 0.0f;

                float distance = 0.0f;
                if (extrusionShape == null)
                {
                    // Make extrusion shape be a circle with the given thickness
                    distance = q2.magnitude - thickness;
                }
                else
                {
                    distance = extrusionShape.GetSignedDistance(q2);
                    if (!extrusionShape.isClosed) distance -= thickness;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLine = seg;
                    bestPoint = cp;
                    bestT = t;
                    bestPlaneNormal = dir;
                }
            }

            if (bestDistance != float.PositiveInfinity)
            {
                if ((bestT < 1e-6) || (bestT > (1.0f - 1e-6)))
                {
                    // Closest point is on an endpoint of a segment, we need to add the caps
                    if (bestDistance < 0.0f)
                    {
                        // The projected point is inside the profile, the distance is the distance to the plane of the segment
                        if (bestT <= 0.0f) bestPlaneNormal = -bestPlaneNormal;
                        bestDistance = Vector3.Dot(worldPoint - bestPoint, bestPlaneNormal);
                    }
                    else
                    {
                        // The distance is the distance to the segment itself
                    }
                }
            }*/

            return bestDistance;
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
