using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UC
{

    public class PathXY : MonoBehaviour
    {
        [SerializeField] public enum Type { Linear = 0, CatmulRom = 1, Circle = 2, Arc = 3, Polygon = 4, Bezier = 5 };

        [SerializeField]
        private Type type = Type.Linear;
        [SerializeField]
        private bool closed = false;
        [SerializeField, Range(3, 30)]
        private int nSides = 4;
        [SerializeField]
        private List<Vector3> points;

        [SerializeField]
        private bool worldSpace = true;

        private float primaryRadius;
        private Vector2 primaryDir;
        private float perpRadius;
        private Vector2 perpDir;
        private float secondaryRadius;
        private Vector2 secondaryDir;
        private float startAngle;
        private float endAngle;
        private bool dirty = true;
        private List<Vector3> fullPoints;
        private float linearLength;

        public List<Vector3> GetEditPoints() => (points == null) ? (null) : (new List<Vector3>(points));
        public void SetEditPoints(List<Vector3> inPoints)
        {
            points = new List<Vector3>(inPoints);

            dirty = true;
        }
        public int GetEditPointsCount() => (points != null) ? (points.Count) : (0);

        public Type GetPathType() => type;
        public void SetPathType(Type t) => type = t;
        public void SetWorldSpace(bool ws) => worldSpace = ws;

        public bool isWorldSpace => worldSpace;
        public bool isLocalSpace => !worldSpace;
        public bool isClosed => closed;
        public Type pathType => type;

        protected void Awake()
        {
            ComputeVariables();
        }

        public void AddSegment(int insertionPoint = -1)
        {
            if (points == null) points = new List<Vector3>();

            if (type == Type.Bezier)
            {
                if (points.Count >= 4)
                {
                    if ((closed) && (insertionPoint == -1))
                    {
                        int count = points.Count;

                        int baseAnchorIndex;
                        if (insertionPoint < 0)
                        {
                            // No insertion point: split last
                            baseAnchorIndex = count - 4;
                        }
                        else
                        {
                            // Find the starting anchor
                            baseAnchorIndex = (insertionPoint / 3) * 3;
                            baseAnchorIndex = Mathf.Clamp(baseAnchorIndex, 0, count - 4);
                        }

                        Vector3 p0 = points[baseAnchorIndex];
                        Vector3 p1 = points[baseAnchorIndex + 1];
                        Vector3 p2 = points[baseAnchorIndex + 2];
                        Vector3 p3 = points[baseAnchorIndex + 3];

                        Bezier.SplitCubic(p0, p1, p2, p3, 0.5f, out Vector3[] bezier1, out Vector3[] bezier2);

                        points[baseAnchorIndex] = bezier1[0];
                        points[baseAnchorIndex + 1] = bezier1[1];
                        points[baseAnchorIndex + 2] = bezier1[2];
                        points[baseAnchorIndex + 3] = bezier1[3];

                        // Insert second bezier right after
                        points.Insert(baseAnchorIndex + 4, bezier2[1]);
                        points.Insert(baseAnchorIndex + 5, bezier2[2]);
                        points.Insert(baseAnchorIndex + 6, bezier2[3]);
                    }
                    else
                    {
                        Vector3 p0 = points[points.Count - 1];
                        Vector3 pBefore = points[points.Count - 2];

                        Vector3 p1 = p0 - (pBefore - p0);
                        Vector3 p3 = p1 - (pBefore - p0);
                        Vector3 p2 = (p1 + p3) * 0.5f;

                        points.Add(p1);
                        points.Add(p2);
                        points.Add(p3);
                    }
                }
                else
                {
                    // Add 4 points
                    points.Add(Vector3.zero);
                    points.Add(Vector3.right * 10.0f + Vector3.up * 10.0f);
                    points.Add(Vector3.right * 20.0f + Vector3.down * 10.0f);
                    points.Add(Vector3.right * 30.0f);
                }
            }
            else AddPoint(insertionPoint);
        }

        public int AddPoint(int insertionPoint = -1)
        {
            if (points == null) points = new List<Vector3>();

            int ret = points.Count;

            if ((insertionPoint >= 0) && (insertionPoint < points.Count))
            {
                if (points.Count >= 2)
                {
                    Vector3 p1 = points[insertionPoint];
                    Vector3 p2 = points[(insertionPoint + 1) % points.Count];

                    points.Insert(insertionPoint + 1, (p1 + p2) * 0.5f);
                    ret = insertionPoint + 1;
                }
                else
                {
                    points.Add(points[points.Count - 1] + new Vector3(20, 20, 0));
                }
            }
            else
            {
                if (points.Count >= 2)
                {
                    // Get last two points and make the new point in that direction
                    Vector3 delta = points[points.Count - 1] - points[points.Count - 2];

                    points.Add(points[points.Count - 1] + delta);
                }
                else if (points.Count >= 1)
                {
                    // Create a point next to the last point
                    points.Add(points[points.Count - 1] + new Vector3(20, 20, 0));
                }
                else
                {
                    // Create point in (0,0,0)
                    points.Add(Vector3.zero);
                }
            }

            dirty = true;

            return ret;
        }

        

        private static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            Vector3 a = -0.5f * p0 + 1.5f * p1 - 1.5f * p2 + 0.5f * p3;
            Vector3 b = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
            Vector3 c = -0.5f * p0 + 0.5f * p2;
            Vector3 d = p1;

            return a * t3 + b * t2 + c * t + d;
        }

        private static Vector3 EvaluateCatmullRomDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;

            Vector3 a = -0.5f * p0 + 1.5f * p1 - 1.5f * p2 + 0.5f * p3;
            Vector3 b = p0 - 2.5f * p1 + 2f * p2 - 0.5f * p3;
            Vector3 c = -0.5f * p0 + 0.5f * p2;

            // Derivative calculation
            Vector3 derivative = 3 * a * t2 + 2 * b * t + c;

            return derivative;
        }

        public Vector2 EvaluateWorld(float t)
        {
            if (worldSpace) return EvaluateLocal(t);

            return transform.TransformPoint(EvaluateLocal(t));
        }

        public Vector2 EvaluateLocal(float t)
        {
            switch (type)
            {
                case Type.Linear:
                    {
                        float d = t * linearLength;

                        for (int i = 0; i < points.Count - ((closed) ? (0) : (1)); i++)
                        {
                            var delta = points[(i + 1) % points.Count] - points[i];
                            float thisDistance = delta.magnitude;
                            if (thisDistance >= d)
                            {
                                return points[i] + d * (delta / thisDistance);
                            }
                            else d -= thisDistance;
                        }

                        return points[0];
                    }
                case Type.CatmulRom:
                    {
                        int numPoints = points.Count;
                        int lastIndex = closed ? numPoints : numPoints - 2;
                        float scaledT = t * lastIndex; // Scale t to the number of segments
                        int i = Mathf.FloorToInt(scaledT) % numPoints; // Segment index, wrap around for closed curve

                        // Calculate t for the specific segment
                        t = scaledT - Mathf.FloorToInt(scaledT);

                        // The three points for the segment
                        Vector3 p0 = points[(i - 1 + numPoints) % numPoints];
                        Vector3 p1 = points[i];
                        Vector3 p2 = points[(i + 1) % numPoints];

                        // Generate a fake fourth point for Catmull-Rom equation
                        Vector3 p3 = points[(i + 2) % numPoints];

                        return EvaluateCatmullRom(p0, p1, p2, p3, t);
                    }
                case Type.Circle:
                    {
                        float angle = t * Mathf.PI * 2.0f;
                        Vector2 center = points[0];

                        return center + primaryRadius * primaryDir * Mathf.Cos(angle) + perpRadius * perpDir * Mathf.Sin(angle);
                    }
                case Type.Arc:
                    {
                        float angle = Mathf.Lerp(startAngle, endAngle, t);
                        float radius = Mathf.Lerp(primaryRadius, secondaryRadius, t);
                        Vector2 center = points[0];

                        return center + Vector2.right * radius * Mathf.Cos(angle) + Vector2.up * radius * Mathf.Sin(angle);
                    }
                case Type.Polygon:
                    {
                        float totalSides = (nSides - ((closed) ? (0) : (1)));
                        int side = Mathf.FloorToInt(totalSides * t);
                        float tInc = 1.0f / totalSides;
                        float angleInc = Mathf.PI * 2.0f / (totalSides + ((closed) ? (0) : (1)));
                        float angle = side * angleInc;
                        Vector2 center = points[0];

                        Vector3 p1 = center + primaryRadius * primaryDir * Mathf.Cos(angle) + perpRadius * perpDir * Mathf.Sin(angle);
                        Vector3 p2 = center + primaryRadius * primaryDir * Mathf.Cos(angle + angleInc) + perpRadius * perpDir * Mathf.Sin(angle + angleInc);

                        float remainingT = (t - side * tInc) / tInc;

                        return p1 + (p2 - p1) * remainingT;
                    }
                case Type.Bezier:
                    {
                        int numPoints = points.Count;
                        if (numPoints < 4) return points[0];

                        int numSegments = closed ? numPoints / 3 : (numPoints - 1) / 3;
                        if (numSegments == 0) return points[0];

                        float scaledT = t * numSegments;
                        int segment = Mathf.FloorToInt(scaledT);
                        segment = Mathf.Clamp(segment, 0, numSegments - 1);

                        t = scaledT - segment;

                        int i0 = (segment * 3) % numPoints;
                        int i1 = (i0 + 1) % numPoints;
                        int i2 = (i0 + 2) % numPoints;
                        int i3 = (i0 + 3) % numPoints;

                        Vector3 p0 = points[i0];
                        Vector3 p1 = points[i1];
                        Vector3 p2 = points[i2];
                        Vector3 p3 = points[i3];

                        return Bezier.ComputeCubic(p0, p1, p2, p3, t);
                    }
                default:
                    break;
            }

            return Vector2.zero;
        }

        public Vector2 EvaluateWorldDir(float t)
        {
            if (worldSpace) return EvaluateLocalDir(t);

            return transform.TransformVector(EvaluateLocalDir(t));
        }

        public Vector2 EvaluateLocalDir(float t)
        {
            switch (type)
            {
                case Type.Linear:
                    {
                        float d = t * linearLength;

                        for (int i = 0; i < points.Count - ((closed) ? (0) : (1)); i++)
                        {
                            var delta = points[(i + 1) % points.Count] - points[i];
                            float thisDistance = delta.magnitude;
                            if (thisDistance >= d)
                            {
                                return delta / thisDistance;
                            }
                            else d -= thisDistance;
                        }

                        if (points.Count > 2)
                        {
                            if (closed)
                            {
                                var delta = points[0] - points[points.Count - 1];
                                return delta.normalized;
                            }
                            else
                            {
                                var delta = points[points.Count - 1] - points[points.Count - 2];
                                return delta.normalized;
                            }
                        }

                        return points[0];
                    }
                case Type.CatmulRom:
                    {
                        int numPoints = points.Count;
                        int lastIndex = closed ? numPoints : numPoints - 2;
                        float scaledT = t * lastIndex; // Scale t to the number of segments
                        int i = Mathf.FloorToInt(scaledT) % numPoints; // Segment index, wrap around for closed curve

                        // Calculate t for the specific segment
                        t = scaledT - Mathf.FloorToInt(scaledT);

                        // The three points for the segment
                        Vector3 p0 = points[(i - 1 + numPoints) % numPoints];
                        Vector3 p1 = points[i];
                        Vector3 p2 = points[(i + 1) % numPoints];

                        // Generate a fake fourth point for Catmull-Rom equation
                        Vector3 p3 = points[(i + 2) % numPoints];

                        return EvaluateCatmullRomDerivative(p0, p1, p2, p3, t).normalized;
                    }
                case Type.Circle:
                    {
                        float angle = t * Mathf.PI * 2.0f;

                        return (-primaryDir * Mathf.Sin(angle) + perpDir * Mathf.Cos(angle)).normalized;
                    }
                case Type.Arc:
                    {
                        float angle = Mathf.Lerp(startAngle, endAngle, t);
                        float radius = Mathf.Lerp(primaryRadius, secondaryRadius, t);

                        return (Vector2.up * radius * Mathf.Cos(angle) - Vector2.right * radius * Mathf.Sin(angle)).normalized;
                    }
                case Type.Polygon:
                    {
                        float totalSides = (nSides - ((closed) ? (0) : (1)));
                        int side = Mathf.FloorToInt(totalSides * t);
                        float tInc = 1.0f / totalSides;
                        float angleInc = Mathf.PI * 2.0f / (totalSides + ((closed) ? (0) : (1)));
                        float angle = side * angleInc;
                        Vector2 center = points[0];

                        Vector3 p1 = center + primaryRadius * primaryDir * Mathf.Cos(angle) + perpRadius * perpDir * Mathf.Sin(angle);
                        Vector3 p2 = center + primaryRadius * primaryDir * Mathf.Cos(angle + angleInc) + perpRadius * perpDir * Mathf.Sin(angle + angleInc);

                        float remainingT = (t - side * tInc) / tInc;

                        return (p2 - p1).normalized;
                    }
                case Type.Bezier:
                    {
                        int numPoints = points.Count;
                        if (numPoints < 4) return Vector2.zero;

                        int numSegments = closed ? numPoints / 3 : (numPoints - 1) / 3;
                        if (numSegments == 0) return Vector2.zero;

                        float scaledT = t * numSegments;
                        int segment = Mathf.FloorToInt(scaledT);
                        segment = Mathf.Clamp(segment, 0, numSegments - 1);

                        t = scaledT - segment;

                        int i0 = (segment * 3) % numPoints;
                        int i1 = (i0 + 1) % numPoints;
                        int i2 = (i0 + 2) % numPoints;
                        int i3 = (i0 + 3) % numPoints;

                        Vector3 p0 = points[i0];
                        Vector3 p1 = points[i1];
                        Vector3 p2 = points[i2];
                        Vector3 p3 = points[i3];

                        return Bezier.ComputeCubicDerivative(p0, p1, p2, p3, t).normalized;
                    }
                default:
                    break;
            }

            return Vector2.zero;
        }

        public void InvertPath()
        {
            if ((type == PathXY.Type.Linear) || (type == PathXY.Type.CatmulRom) || (type == PathXY.Type.Bezier))
            {
                var newPoints = new List<Vector3>();
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    newPoints.Add(points[i]);
                }

                points = newPoints;
            }
            else if (type == PathXY.Type.Arc)
            {
                if (points.Count > 1)
                {
                    (points[1], points[2]) = (points[2], points[1]);
                }
            }
        }

        public void CenterPath()
        {
            if ((points == null) || (points.Count == 0)) return;

            Vector3 delta = Vector3.zero;

            if ((type == PathXY.Type.Linear) || (type == PathXY.Type.CatmulRom) || (type == Type.Bezier))
            {
                // Get extents of object
                Vector3 min = points[0];
                Vector3 max = points[0];

                foreach (var pt in points)
                {
                    min.x = Mathf.Min(min.x, pt.x);
                    min.y = Mathf.Min(min.y, pt.y);
                    min.z = Mathf.Min(min.z, pt.z);
                    max.x = Mathf.Max(max.x, pt.x);
                    max.y = Mathf.Max(max.y, pt.y);
                    max.z = Mathf.Max(max.z, pt.z);
                }

                delta = new Vector3(-(max.x + min.x) * 0.5f, -(max.y + min.y) * 0.5f, -(max.z + min.z) * 0.5f);
            }
            else if ((type == PathXY.Type.Arc) || (type == PathXY.Type.Circle) || (type == PathXY.Type.Polygon))
            {
                delta = -points[0];
            }

            for (int i = 0; i < points.Count; i++)
            {
                points[i] = points[i] + delta;
            }
        }

        public void ConvertToLocalSpace()
        {
            var matrix = transform.worldToLocalMatrix;

            for (int i = 0; i < points.Count; i++)
            {
                Vector4 pt = points[i];
                pt.w = 1;

                points[i] = matrix * pt;
            }

            worldSpace = false;
        }

        public void ConvertToWorldSpace()
        {
            var matrix = transform.localToWorldMatrix;

            for (int i = 0; i < points.Count; i++)
            {
                Vector4 pt = points[i];
                pt.w = 1;

                points[i] = matrix * pt;
            }

            worldSpace = true;
        }

        public List<Vector3> GetPoints()
        {
            if ((!dirty) && (fullPoints == null)) return fullPoints;

            ComputeVariables();

            fullPoints = new List<Vector3>();
            if (points == null) return fullPoints;

            int steps = 20;
            switch (type)
            {
                case Type.Linear:
                    steps = 0;
                    fullPoints = new List<Vector3>(points);
                    if ((closed) && (points.Count > 1)) fullPoints.Add(points[0]);
                    break;
                case Type.CatmulRom:
                    if (points.Count < 3)
                    {
                        steps = 0;
                        fullPoints = new List<Vector3>(points);
                    }
                    else
                    {
                        steps = points.Count * 20;
                    }
                    break;
                case Type.Circle:
                case Type.Arc:
                    {
                        if (points.Count < ((type == Type.Circle) ? (2) : (3)))
                        {
                            steps = 0;
                            fullPoints = new List<Vector3>(points);
                        }
                        else
                        {
                            steps = 180;
                        }
                    }
                    break;
                case Type.Polygon:
                    {
                        if (points.Count < 2)
                        {
                            steps = 0;
                            fullPoints = new List<Vector3>(points);
                        }
                        else
                        {
                            steps = 0;

                            float angle = 0.0f;
                            float angleInc = Mathf.PI * 2.0f / nSides;
                            for (int i = 0; i < nSides; i++)
                            {
                                Vector2 center = points[0];
                                fullPoints.Add(center + primaryDir * primaryRadius * Mathf.Cos(angle) + perpDir * perpRadius * Mathf.Sin(angle));
                                angle += angleInc;
                            }
                            if (closed)
                            {
                                fullPoints.Add(fullPoints[0]);
                            }
                        }
                    }
                    break;
                case Type.Bezier:
                    if (points.Count < 4)
                    {
                        steps = 0;
                        fullPoints = new List<Vector3>(points);
                    }
                    else
                    {
                        steps = points.Count * 20;
                    }
                    break;
                default:
                    break;
            }

            if (steps > 0)
            {
                var prevPoint = EvaluateLocal(0.0f);
                fullPoints.Add(prevPoint);

                float tInc = 1.0f / steps;
                float t = tInc;

                for (int i = 0; i < steps; i++)
                {
                    var newPt = EvaluateLocal(t);
                    if (Vector3.SqrMagnitude(newPt - prevPoint) > 1.0f)
                    {
                        fullPoints.Add(newPt);
                        prevPoint = newPt;
                    }

                    t += tInc;
                }
            }

            if (isLocalSpace)
            {
                for (int i = 0; i < fullPoints.Count; i++)
                {
                    fullPoints[i] = transform.TransformPoint(fullPoints[i]);
                }
            }

            dirty = false;

            return fullPoints;
        }

        private void ComputeVariables()
        {
            // Compute length
            linearLength = 0.0f;
            if ((points != null) && (points.Count > 1))
            {
                for (int i = 0; i < points.Count - ((closed) ? (0) : (1)); i++)
                {
                    linearLength += Vector3.Distance(points[i], points[(i + 1) % points.Count]);
                }

                // Compute generic values for circle, arc and polygon
                if (points.Count >= 2)
                {
                    primaryRadius = Vector3.Distance(points[0], points[1]);
                    primaryDir = (points[1] - points[0]).normalized;
                    perpDir = new Vector2(primaryDir.y, -primaryDir.x);
                    perpRadius = primaryRadius;
                    if (points.Count >= 3)
                    {
                        secondaryDir = points[2] - points[0];
                        if (Vector2.Dot(perpDir, secondaryDir) < 0) perpDir = -perpDir;
                        perpRadius = Vector2.Dot(perpDir, secondaryDir);

                        secondaryRadius = secondaryDir.magnitude;
                        secondaryDir /= secondaryRadius;

                        startAngle = Mathf.Atan2(primaryDir.y, primaryDir.x);
                        endAngle = Mathf.Atan2(secondaryDir.y, secondaryDir.x);

                        if (endAngle < startAngle) endAngle += Mathf.PI * 2.0f;
                    }
                }
            }
        }

        public Vector3 upAxis => primaryDir;
        public float upExtent => primaryRadius;
        public Vector3 rightAxis => perpDir;
        public float rightExtent => perpRadius;

        public void SetDirty()
        {
            dirty = true;
        }

        public (float distance, int point) GetDistance(Ray ray, float startMinDist)
        {
            float minDist = startMinDist;
            int pointIndex = -1;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];

                Vector3 originToPoint = p - ray.origin;

                // Project originToPoint onto the ray direction to find the closest point on the ray
                float t = Vector3.Dot(originToPoint, ray.direction);

                // If t is negative, the closest point is the origin of the ray
                Vector3 closestPointOnRay = t < 0 ? ray.origin : ray.origin + t * ray.direction;

                float d = Vector3.Distance(closestPointOnRay, p);
                if (d < minDist)
                {
                    minDist = d;
                    pointIndex = i;
                }
            }

            if (pointIndex != -1)
            {
                // Make it so that the points have priority (distance to points is 10 times smaller than distance to segment)
                minDist *= 0.1f;
            }

            var renderPoints = GetPoints();
            for (int i = 1; i < renderPoints.Count; i++)
            {
                var p1 = renderPoints[i - 1];
                var p2 = renderPoints[i];

                float d = DistanceRayToSegment(ray, p1, p2);
                if (d < minDist)
                {
                    minDist = d;
                    pointIndex = -1;
                }
            }

            return (minDist, pointIndex);
        }

        public static float DistanceRayToSegment(Ray ray, Vector3 segmentStart, Vector3 segmentEnd)
        {
            Vector3 u = ray.direction; // Direction of the ray
            Vector3 v = segmentEnd - segmentStart; // Direction of the segment
            Vector3 w = ray.origin - segmentStart;

            float a = Vector3.Dot(u, u);         // Squared length of the ray direction
            float b = Vector3.Dot(u, v);         // Dot product of ray and segment directions
            float c = Vector3.Dot(v, v);         // Squared length of the segment direction
            float d = Vector3.Dot(u, w);         // Dot product of ray direction and w
            float e = Vector3.Dot(v, w);         // Dot product of segment direction and w

            float denominator = a * c - b * b;   // Denominator for calculating sc and tc

            float sc, tc;

            // If denominator is close to zero, the lines are almost parallel
            if (denominator < Mathf.Epsilon)
            {
                sc = 0.0f;
                tc = (b > c ? d / b : e / c);  // Use the larger of the two factors to find tc
            }
            else
            {
                sc = (b * e - c * d) / denominator;
                tc = (a * e - b * d) / denominator;
            }

            // Clamp tc to the segment
            tc = Mathf.Clamp(tc, 0.0f, 1.0f);

            // Closest points on the ray and the segment
            Vector3 closestPointOnRay = ray.origin + sc * u;
            Vector3 closestPointOnSegment = segmentStart + tc * v;

            // Return the distance between the closest points
            return Vector3.Distance(closestPointOnRay, closestPointOnSegment);
        }

        public (float distance, Vector3 point, Vector3 direction) GetDistance(Vector3 point)
        {
            float   minDist = float.MaxValue;
            Vector3 closestPoint = point;
            Vector3 direction = Vector2.up;

            // Then check against segments in the rendered path
            var renderPoints = GetPoints();
            for (int i = 1; i < renderPoints.Count; i++)
            {
                Vector3 a = renderPoints[i - 1];
                Vector3 b = renderPoints[i];
                float d = Line.Distance(a, b, point, out var pt);
                if (d < minDist)
                {
                    minDist = d;
                    closestPoint = pt;
                    direction = (b - a).normalized;
                }
            }

            return (minDist, closestPoint, direction);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Handles.color = Color.yellow;

            int everyNth = 5;

            var renderPoints = GetPoints();
            for (int i = 1; i < renderPoints.Count; i++)
            {
                Handles.DrawLine(renderPoints[i - 1], renderPoints[i], 1.0f);

                if ((i % everyNth) == 0)
                {
                    var dir = (renderPoints[i] - renderPoints[i - 1]);
                    var length = dir.magnitude;
                    dir = dir.normalized.PerpendicularXY();

                    Handles.color = Color.yellow.ChangeAlpha(0.25f);
                    Handles.DrawLine(renderPoints[i], renderPoints[i - 1] + dir * length * 0.5f, 2.0f);
                    Handles.DrawLine(renderPoints[i], renderPoints[i - 1] - dir * length * 0.5f, 2.0f);
                    Handles.color = Color.yellow;
                }
            }

            if ((points != null) && (type != Type.Bezier))
            {
                float s = 1.0f;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 p = points[i];
                    if (isLocalSpace) p = transform.TransformPoint(p);

                    Handles.DrawLine(p + new Vector2(s, s), p + new Vector2(-s, -s), 1.0f);
                    Handles.DrawLine(p + new Vector2(s, -s), p + new Vector2(-s, s), 1.0f);
                }
            }
        }
#endif
    }
}