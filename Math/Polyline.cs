using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC
{

    [Serializable]
    public class Polyline : IEnumerable<(Vector3 position, Vector3 normal)>
    {
        [SerializeField]
        List<Vector3>   vertices;
        [SerializeField]
        List<Vector3>   normals;
        [SerializeField]
        bool            _closed = false;

        public List<Vector3> GetVertices() => vertices;
        public bool isClosed 
        {
            get => _closed;
            set { _closed = value; }
        }


        public Polyline() { }
        public Polyline(Vector3 p) { vertices = new() { p }; }
        public Polyline(Vector3 p1, Vector3 p2, Vector3 p3) { vertices = new() { p1, p2, p3 }; }

        public Polyline(Polyline polyline)
        {
            vertices = new(polyline.vertices);
            if (polyline.normals != null) normals = new(polyline.normals);
            _closed = polyline._closed;
        }

        public void Add(Vector3 vertex)
        {
            if (vertices == null) vertices = new List<Vector3>();

            vertices.Add(vertex);
        }
        public void Add(Vector3 vertex, Vector3 normal)
        {
            if (vertices == null) vertices = new List<Vector3>();
            if (normals == null) normals = new List<Vector3>();

            vertices.Add(vertex);
            normals.Add(normal);
        }


        public void SetNormal(int index, Vector3 normal)
        {
            if (normals == null)
            {
                normals = new List<Vector3>();
                normals.Resize(index + 1);
            }

            normals[index] = normal;
        }

        public void Insert(int index, Vector3 vertex)
        {
            vertices.Insert(index, vertex);
        }
        public void Insert(int index, Vector3 vertex, Vector3 normal)
        {
            vertices.Insert(index, vertex);
            normals.Insert(index, normal);
        }

        public int Count
        {
            get => (vertices != null) ? (vertices.Count) : (0);
        }

        public int normalCount
        {
            get => (normals != null) ? (normals.Count) : (0);
            set
            {
                if (value == 0) normals = null;
                else
                {
                    if (normals == null) normals = new List<Vector3>();
                    normals.Resize(value);
                }
            }
        }

        public Vector3 this[int idx]
        {
            get => vertices[idx];
            set => vertices[idx] = value;
        }

        public Vector3 this[int idx, bool fromTheEnd]
        {
            get => vertices[vertices.Count - idx - 1];
            set => vertices[vertices.Count - idx - 1] = value;
        }

        public Vector3 GetNormal(int idx)
        {
            return normals[idx];
        }

        public void Simplify(float maxDistance)
        {
            int currentPoint = 0;
            float currentError = 0;

            while ((currentPoint < vertices.Count) && (vertices.Count > 3))
            {
                int i1 = (currentPoint + 1) % (vertices.Count);
                int i2 = (currentPoint + 2) % (vertices.Count);
                Vector3 p0 = vertices[currentPoint];
                Vector3 p1 = vertices[i1];
                Vector3 p2 = vertices[i2];

                float dist = LineHelpers.Distance(p0, p2, p1);
                float error = (dist * (p2 - p0).magnitude) * 0.5f;
                if ((error + currentError) <= maxDistance)
                {
                    vertices.RemoveAt(i1);
                    normals?.RemoveAt(i1);
                    currentError += error;
                }
                else
                {
                    currentPoint++;
                    currentError = 0;
                }
            }
        }

        // Ramer-Douglas-Peucker algorithm gives predictable, distance-based simplification. You choose a single epsilon (max perpendicular deviation)-no accumulated area gymnastics-and it will:
        // Find the point with the maximum perpendicular distance to the line between start/end.
        // If that distance > epsilon, keep it and recurse on the two subsegments.
        // Otherwise, drop all intermediate points.
        //
        // Should be much better than my old greedy vertex decimater
        public Polyline SimplifyRDP(float epsilon, bool closed = true)
        {
            static Vector2 ProjectPointOnLine(Vector2 p, Vector2 a, Vector2 b)
            {
                var ap = p - a;
                var ab = (b - a).normalized;
                float d = Vector2.Dot(ap, ab);
                return a + ab * d;
            }

            // Returns a list of indices to keep
            List<int> RDP(List<Vector2> pts, int i1, int i2, float eps)
            {
                float maxDist = 0;
                int index = i1;
                for (int i = i1 + 1; i < i2; i++)
                {
                    float d = Vector2.Distance(pts[i], ProjectPointOnLine(pts[i], pts[i1], pts[i2]));
                    if (d > maxDist) { maxDist = d; index = i; }
                }

                if (maxDist > eps)
                {
                    var left = RDP(pts, i1, index, eps);
                    var right = RDP(pts, index, i2, eps);
                    // merge, avoid duplicate at the joint
                    return left.Take(left.Count - 1).Concat(right).ToList();
                }
                else
                {
                    return new List<int> { i1, i2 };
                }
            }

            var pts2D = vertices.Select(v => new Vector2(v.x, v.y)).ToList();
            var keep = RDP(pts2D, 0, pts2D.Count - 1, epsilon);

            // If closed, ensure the first/last are the same segment
            if (closed && keep[0] != keep[^1])
                keep.Add(keep[0]);

            var result = new Polyline();
            for (int i = 0; i < keep.Count; i++)
            {
                int idx = keep[i];
                result.Add(vertices[idx], normals?.Count == vertices.Count ? normals[idx] : Vector3.zero);
            }
            result.isClosed = isClosed;
            return result;
        }

        public bool isCW()
        {
            Vector3 v = Vector3.zero;
            var count = vertices.Count;

            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = vertices[i];
                Vector3 p1 = vertices[(i + 1) % count];
                Vector3 p2 = vertices[(i + 2) % count];

                v += Vector3.Cross(p1 - p0, p2 - p0);
            }
            v.Normalize();

            float ma = 0.0f;

            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            {
                if (Mathf.Abs(v.x) > Mathf.Abs(v.z)) ma = v.x;
                else ma = v.z;
            }
            else
            {
                if (Mathf.Abs(v.y) > Mathf.Abs(v.z)) ma = v.y;
                else ma = v.z;
            }

            return (ma < 0);
        }

        public void ReverseOrder()
        {
            vertices.Reverse();
            if (normals != null) normals.Reverse();
        }

        public float ComputeArea()
        {
            if (vertices == null || vertices.Count < 3)
            {
                return 0f; // No area for less than 3 vertices
            }

            // Step 1: Determine the best projection plane by calculating the normal vector
            Vector3 normal = CalculateNormal();

            // Step 2: Project the polygon onto the most appropriate 2D plane
            List<Vector2> projectedVertices = ProjectTo2D(normal);

            // Step 3: Use the Shoelace formula to calculate the area of the 2D polygon
            return Calculate2DPolygonArea(projectedVertices);
        }

        public void RemoveDuplicates(float epsilon = 1e-3f)
        {
            var toRemove = new List<int>();

            while (true)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (Vector3.Distance(vertices[i], vertices[(i + 1) % vertices.Count]) < epsilon)
                    {
                        toRemove.Add(i);
                    }
                }
                if (toRemove.Count == 0) break;

                for (int i = 0; i < toRemove.Count; i++)
                {
                    vertices.RemoveAt(toRemove[i] - i);
                    if (normals != null) normals.RemoveAt(toRemove[i] - i);
                }
                toRemove.Clear();
            }
        }

        Vector3 CalculateNormal()
        {
            Vector3 normal = Vector3.zero;
            if ((normals != null) && (normals.Count == vertices.Count))
            {
                foreach (var n in normals)
                {
                    normal += n;
                }
            }
            else
            {
                // Using Newell's method to calculate the normal
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 current = vertices[i];
                    Vector3 next = vertices[(i + 1) % vertices.Count];

                    normal.x += (current.y - next.y) * (current.z + next.z);
                    normal.y += (current.z - next.z) * (current.x + next.x);
                    normal.z += (current.x - next.x) * (current.y + next.y);
                }
            }

            normal.Normalize();

            return normal;
        }

        private List<Vector2> ProjectTo2D(Vector3 normal)
        {
            List<Vector2> projectedVertices = new List<Vector2>();

            // Determine the dominant axis (the largest component in the normal vector)
            if (Mathf.Abs(normal.x) > Mathf.Abs(normal.y) && Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
            {
                // Project onto YZ plane (ignore x component)
                foreach (var vertex in vertices)
                {
                    projectedVertices.Add(new Vector2(vertex.y, vertex.z));
                }
            }
            else if (Mathf.Abs(normal.y) > Mathf.Abs(normal.x) && Mathf.Abs(normal.y) > Mathf.Abs(normal.z))
            {
                // Project onto XZ plane (ignore y component)
                foreach (var vertex in vertices)
                {
                    projectedVertices.Add(new Vector2(vertex.x, vertex.z));
                }
            }
            else
            {
                // Project onto XY plane (ignore z component)
                foreach (var vertex in vertices)
                {
                    projectedVertices.Add(new Vector2(vertex.x, vertex.y));
                }
            }

            return projectedVertices;
        }

        // Calculate the area of a 2D polygon using the Shoelace formula
        private float Calculate2DPolygonArea(List<Vector2> vertices2D)
        {
            float area = 0f;
            int count = vertices2D.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 current = vertices2D[i];
                Vector2 next = vertices2D[(i + 1) % count]; // Ensure the loop wraps around

                area += current.x * next.y;
                area -= current.y * next.x;
            }

            return Mathf.Abs(area) * 0.5f;
        }

        public Vector3 GetCenter()
        {
            Vector3 center = Vector3.zero;
            if ((vertices == null) || (vertices.Count == 0)) return center;

            foreach (var v in vertices)
            {
                center += v;
            }

            return center / vertices.Count;
        }

        public void Subdivide(float maxEdgeLength)
        {
            if (vertices == null || vertices.Count < 2)
                return;

            bool hasNormals = (normals != null && normals.Count == vertices.Count);
            var newVerts = new List<Vector3>();
            var newNorms = hasNormals ? new List<Vector3>() : null;

            int N = vertices.Count;
            // if closed, we wrap around; otherwise we stop before the last vert
            int loopCount = isClosed ? N : N - 1;

            for (int i = 0; i < loopCount; i++)
            {
                var A = vertices[i];
                var B = vertices[(i + 1) % N];

                newVerts.Add(A);
                if (hasNormals) newNorms.Add(normals[i]);

                float d = Vector3.Distance(A, B);
                if (d > maxEdgeLength)
                {
                    // how many interior splits?
                    int steps = Mathf.CeilToInt(d / maxEdgeLength) - 1;
                    for (int s = 1; s <= steps; s++)
                    {
                        float t = (float)s / (steps + 1);
                        // split position
                        var P = Vector3.Lerp(A, B, t);
                        newVerts.Add(P);

                        if (hasNormals)
                        {
                            // interpolate & renormalize
                            var NA = normals[i];
                            var NB = normals[(i + 1) % N];
                            newNorms.Add(Vector3.Lerp(NA, NB, t).normalized);
                        }
                    }
                }
            }

            // if not closed, we still need to append the last original vertex
            if (!isClosed)
            {
                newVerts.Add(vertices[N - 1]);
                if (hasNormals) newNorms.Add(normals[N - 1]);
            }

            vertices = newVerts;
            if (hasNormals) normals = newNorms;
        }


        // Implement the IEnumerable interface to allow iteration
        public IEnumerator<(Vector3 position, Vector3 normal)> GetEnumerator()
        {
            if ((normals != null) && (vertices.Count == normals.Count))
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 position = vertices[i];
                    Vector3 normal = normals[i];
                    yield return (position, normal);
                }
            }
            else
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 position = vertices[i];
                    yield return (position, Vector3.zero);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Triangulate_EarCut(List<Polyline> holes, ref List<Vector3> outVertices, ref List<int> outTriangles)
        {
            #region Helpers
            static bool NearlyEqual(Vector2 a, Vector2 b, float eps = 1e-5f)
            {
                return (a - b).sqrMagnitude <= eps * eps;
            }

            static float SignedArea(List<Vector2> ring)
            {
                float a = 0f;
                for (int i = 0, n = ring.Count; i < n; i++)
                {
                    var p = ring[i];
                    var q = ring[(i + 1) % n];
                    a += p.x * q.y - q.x * p.y;
                }
                return 0.5f * a;
            }

            static List<Vector2> NormalizeRing(List<Vector3> src, bool wantCCW, float collinearEps = 1e-9f)
            {
                // 1) copy to 2D, drop duplicate last==first and consecutive dups
                var pts = new List<Vector2>(src.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    var v = new Vector2(src[i].x, src[i].y);
                    if (pts.Count == 0 || !NearlyEqual(pts[pts.Count - 1], v)) pts.Add(v);
                }
                if (pts.Count >= 2 && NearlyEqual(pts[0], pts[^1])) pts.RemoveAt(pts.Count - 1);

                for (int i = pts.Count - 2; i >= 0 && pts.Count >= 2; --i)
                    if (NearlyEqual(pts[i], pts[i + 1])) pts.RemoveAt(i + 1);

                // 2) remove near-collinears
                if (pts.Count >= 3)
                {
                    var clean = new List<Vector2>(pts.Count);
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var a = pts[(i + pts.Count - 1) % pts.Count];
                        var b = pts[i];
                        var c = pts[(i + 1) % pts.Count];
                        var ab = b - a;
                        var bc = c - b;
                        float cross = ab.x * bc.y - ab.y * bc.x;
                        if (Mathf.Abs(cross) > collinearEps || NearlyEqual(a, b) || NearlyEqual(b, c))
                            clean.Add(b);
                    }
                    pts = clean;
                }

                // 3) enforce winding
                bool isCCW = SignedArea(pts) > 0f;
                if (wantCCW != isCCW) pts.Reverse();
                return pts;
            }

            static bool PointInRing(List<Vector2> ring, Vector2 p)
            {
                bool inside = false;
                for (int i = 0, n = ring.Count, j = n - 1; i < n; j = i++)
                {
                    var a = ring[i];
                    var b = ring[j];
                    if (((a.y > p.y) != (b.y > p.y)) &&
                        (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + Mathf.Epsilon) + a.x))
                        inside = !inside;
                }
                return inside;
            }

            static bool RingFullyInside(List<Vector2> inner, List<Vector2> outer)
            {
                // quick AABB reject
                Vector2 minI = inner[0], maxI = inner[0];
                for (int i = 1; i < inner.Count; i++)
                {
                    var v = inner[i];
                    if (v.x < minI.x) minI.x = v.x; if (v.y < minI.y) minI.y = v.y;
                    if (v.x > maxI.x) maxI.x = v.x; if (v.y > maxI.y) maxI.y = v.y;
                }
                // cheap bbox check via point-in-ring on corners
                if (!PointInRing(outer, minI)) return false;
                if (!PointInRing(outer, new Vector2(maxI.x, minI.y))) return false;
                if (!PointInRing(outer, maxI)) return false;
                if (!PointInRing(outer, new Vector2(minI.x, maxI.y))) return false;

                // strict: every vertex inside
                for (int i = 0; i < inner.Count; i++)
                    if (!PointInRing(outer, inner[i])) return false;

                // NOTE: we are intentionally ignoring edge-touch and partial intersection cases for now.
                return true;
            }

            static void BuildEarcutInput(List<Vector2> outer, List<List<Vector2>> holesRings, out float[] data, out int[] holeIdx)
            {
                int dim = 2;
                int total = outer.Count;
                foreach (var h in holesRings) total += h.Count;
                data = new float[total * dim];
                var holeStarts = new List<int>(holesRings.Count);

                int w = 0;
                for (int i = 0; i < outer.Count; i++) { data[w++] = outer[i].x; data[w++] = outer[i].y; }
                int running = outer.Count;
                foreach (var h in holesRings)
                {
                    holeStarts.Add(running);
                    for (int i = 0; i < h.Count; i++) { data[w++] = h[i].x; data[w++] = h[i].y; }
                    running += h.Count;
                }
                holeIdx = holeStarts.Count > 0 ? holeStarts.ToArray() : null;
            }

            static float AreaAbs(List<Vector2> ring)
            {
                float a2 = 0f;
                for (int i = 0, n = ring.Count; i < n; i++)
                {
                    var p = ring[i];
                    var q = ring[(i + 1) % n];
                    a2 += p.x * q.y - q.x * p.y;
                }
                return Mathf.Abs(0.5f * a2);
            }

            #endregion

            if (outVertices == null) outVertices = new List<Vector3>();
            else outVertices.Clear();

            // Outer ring normalization (CCW)
            var outer = NormalizeRing(this.vertices, true);

            // Candidate hole normalization (CW) + filtering to "fully inside outer"
            var keptHoles = new List<List<Vector2>>();
            if (holes != null)
            {
                for (int h = 0; h < holes.Count; h++)
                {
                    var ring = NormalizeRing(holes[h].GetVertices(), false);
                    if ((ring.Count >= 3) && (RingFullyInside(ring, outer)))
                    {
                        keptHoles.Add(ring);
                    }
                }
            }
            if (keptHoles.Count > 1)
            {
                int n = keptHoles.Count;
                var remove = new bool[n];

                for (int i = 0; i < n; i++)
                {
                    if (remove[i]) continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j || remove[j]) continue;

                        // if hole i is fully inside hole j, drop i
                        if (RingFullyInside(keptHoles[i], keptHoles[j]))
                        {
                            // Prefer to drop the smaller one if both contain each other (degenerate duplicates)
                            float ai = AreaAbs(keptHoles[i]);
                            float aj = AreaAbs(keptHoles[j]);
                            if (ai <= aj) remove[i] = true;
                            else remove[j] = true;
                        }
                    }
                }

                var filtered = new List<List<Vector2>>(n);
                for (int i = 0; i < n; i++)
                    if (!remove[i]) filtered.Add(keptHoles[i]);
                keptHoles = filtered;
            }

            // Build Earcut input
            BuildEarcutInput(outer, keptHoles, out var coords, out var holeIndices);

            // Rebuild outVertices to match coords order exactly
            outVertices.Capacity = coords.Length / 2;
            for (int i = 0; i < coords.Length; i += 2)
                outVertices.Add(new Vector3(coords[i + 0], coords[i + 1], 0f));

            // Triangulate
            outTriangles = MadWorldNL.EarCut.Tessellate(coords, holeIndices, 2);
        }

#if UNITY_EDITOR
        public void DrawGizmos()
        {
            for (int i = 0; i < vertices.Count - ((isClosed) ? (0) : (1)); i++)
            {
                Gizmos.DrawLine(vertices[i], vertices[(i + 1) % vertices.Count]);
            }
        }

        public void DrawGizmosWithNormals(float normalLength, Color normalColor)
        {
            if ((normals == null) || (normals.Count != vertices.Count)) DrawGizmos();
            else
            {
                var segmentColor = Gizmos.color;
                for (int i = 0; i < vertices.Count - 1; i++)
                {
                    Gizmos.DrawLine(vertices[i], vertices[i + 1]);
                }
                Gizmos.color = normalColor;
                for (int i = 0; i < vertices.Count; i++)
                {
                    Gizmos.DrawLine(vertices[i], vertices[i] + normals[i] * normalLength);
                }

                Gizmos.color = segmentColor;
            }
        }
#endif
    }
}