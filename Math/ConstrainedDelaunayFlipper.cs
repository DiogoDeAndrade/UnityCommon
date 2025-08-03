using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    public static class ConstrainedDelaunayFlipper
    {
        struct Edge : IEquatable<Edge>
        {
            public readonly int A, B;
            public Edge(int a, int b)
            {
                if (a < b) { A = a; B = b; }
                else { A = b; B = a; }
            }
            public bool Equals(Edge o) => A == o.A && B == o.B;
            public override int GetHashCode() => (A * 397) ^ B;
        }

        /// <summary>
        /// In-place edge flips on 'tris' to maximize minimum angles,
        /// respecting the fixed boundary+holes, never inverting or leaving
        /// the mesh outside its original region.
        /// </summary>
        /// <param name="vertices">All mesh vertices (Vector3, z ignored).</param>
        /// <param name="tris">Flat triangle list: 3 ints per tri.</param>
        /// <param name="boundary">CCW outer ring.</param>
        /// <param name="holes">Zero or more CCW hole rings.</param>
        /// <param name="maxPasses">How many full clean-up sweeps to do.</param>
        /// <param name="epsilonDeg">Minimum-angle gain threshold (degrees).</param>
        public static void EnforceDelaunay(List<Vector3> vertices, List<int> tris, Polyline boundary, List<Polyline> holes, int maxPasses = 5, float epsilonDeg = 0.1f)
        {
            // 1) Gather fixed edges from boundary and holes
            var fixedEdges = new HashSet<Edge>();
            void CollectRing(Polyline ring)
            {
                int n = ring.Count;
                for (int i = 0; i < n; i++)
                {
                    int ni = (i + 1) % n;
                    var a = ring[i];
                    var b = ring[ni];
                    int ai = FindNearestIndex(vertices, a);
                    int bi = FindNearestIndex(vertices, b);
                    fixedEdges.Add(new Edge(ai, bi));
                }
            }
            CollectRing(boundary);
            foreach (var hole in holes) CollectRing(hole);

            // 2) Rebuild adjacency helper
            Dictionary<Edge, List<int>> BuildAdjacency()
            {
                var adj = new Dictionary<Edge, List<int>>();
                for (int t = 0; t < tris.Count; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        int a = tris[t + e];
                        int b = tris[t + (e + 1) % 3];
                        var edge = new Edge(a, b);
                        if (!adj.TryGetValue(edge, out var list))
                        {
                            list = new List<int>();
                            adj[edge] = list;
                        }
                        list.Add(t / 3);
                    }
                }
                return adj;
            }

            // 3) Point-in-polygon (even-odd) for boundary+holes
            bool PointInRing(Vector2 p, Polyline ring)
            {
                bool inside = false;
                int n = ring.Count;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    var vi = ring[i];
                    var vj = ring[j];
                    if (((vi.y > p.y) != (vj.y > p.y)) &&
                        (p.x < (vj.x - vi.x) * (p.y - vi.y) / (vj.y - vi.y) + vi.x))
                    {
                        inside = !inside;
                    }
                }
                return inside;
            }
            bool IsInsideRegion(Vector2 p)
            {
                if (!PointInRing(p, boundary)) return false;
                foreach (var hole in holes)
                    if (PointInRing(p, hole))
                        return false;
                return true;
            }

            // 4) Main flip loop
            for (int pass = 0; pass < maxPasses; pass++)
            {
                var adj = BuildAdjacency();
                var flips = new List<(int t0, int t1, int i, int j, int k, int l)>();

                // collect all safe, improving flips
                foreach (var kv in adj)
                {
                    var edge = kv.Key;
                    var owners = kv.Value;
                    if (owners.Count != 2) continue; // boundary or hole
                    if (fixedEdges.Contains(edge)) continue; // do not flip fixed edges

                    int t0 = owners[0], t1 = owners[1];
                    int o0 = t0 * 3, o1 = t1 * 3;

                    int i = edge.A, j = edge.B;
                    int k = OtherVertex(tris, o0, i, j);
                    int l = OtherVertex(tris, o1, i, j);

                    // compute min-angle before/after
                    float oldMin = Mathf.Min(
                        MinAngle(vertices[k], vertices[i], vertices[j]),
                        MinAngle(vertices[l], vertices[j], vertices[i])
                    );
                    float newMin = Mathf.Min(
                        MinAngle(vertices[k], vertices[l], vertices[i]),
                        MinAngle(vertices[l], vertices[k], vertices[j])
                    );
                    if (newMin <= oldMin + epsilonDeg)
                        continue;

                    // ensure new tris stay CCW (no inversion)
                    if (SignedArea(vertices[k], vertices[i], vertices[l]) <= 0) continue;
                    if (SignedArea(vertices[l], vertices[j], vertices[k]) <= 0) continue;

                    // ensure midpoint of new edge is inside region
                    var mid = 0.5f * ((Vector2)vertices[k] + (Vector2)vertices[l]);
                    if (!IsInsideRegion(mid)) continue;

                    flips.Add((t0, t1, i, j, k, l));
                }

                if (flips.Count == 0)
                    break;

                // apply all collected flips
                foreach (var f in flips)
                    PerformFlip(tris, f.t0, f.t1, f.i, f.j, f.k, f.l);
            }
        }

        // ———————— Helpers ————————

        // Find the index of the closest vertex to 'p'
        static int FindNearestIndex(List<Vector3> verts, Vector2 p)
        {
            int best = 0;
            float bestDist2 = float.MaxValue;
            for (int i = 0; i < verts.Count; i++)
            {
                var v2 = new Vector2(verts[i].x, verts[i].y);
                float d2 = (v2 - p).sqrMagnitude;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    best = i;
                }
            }
            return best;
        }

        // Given tri at offset 'o' (o = triIndex*3), return the vert != a,b
        static int OtherVertex(List<int> tris, int o, int a, int b)
        {
            for (int e = 0; e < 3; e++)
            {
                int v = tris[o + e];
                if (v != a && v != b) return v;
            }
            throw new InvalidOperationException("Degenerate triangle in OtherVertex");
        }

        // Signed area *2 of (A->B->C). >0 if CCW
        static float SignedArea(Vector3 A, Vector3 B, Vector3 C)
        {
            return (B.x - A.x) * (C.y - A.y)
                 - (B.y - A.y) * (C.x - A.x);
        }

        // Minimum interior angle at B in triangle (A,B,C) in degrees
        static float MinAngle(Vector3 A, Vector3 B, Vector3 C)
        {
            var AB = (A - B);
            var CB = (C - B);
            float cos = Vector3.Dot(AB.normalized, CB.normalized);
            cos = Mathf.Clamp(cos, -1f, 1f);
            return Mathf.Acos(cos) * Mathf.Rad2Deg;
        }

        // Replace diagonal (i,j) with (k,l) in triangles t0 and t1
        static void PerformFlip(List<int> tris, int t0, int t1, int i, int j, int k, int l)
        {
            int o0 = t0 * 3;
            int o1 = t1 * 3;
            // new triangles: (k,i,l) and (l,j,k)
            tris[o0 + 0] = k; tris[o0 + 1] = i; tris[o0 + 2] = l;
            tris[o1 + 0] = l; tris[o1 + 1] = j; tris[o1 + 2] = k;
        }
    }
}