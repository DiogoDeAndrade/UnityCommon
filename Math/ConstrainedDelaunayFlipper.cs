using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    // Enforces Delauney triangulation by flipping adjacent triangles on the common edge,
    // to ensure fatter triangles

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

        public static void EnforceDelaunay(List<Vector3> vertices, List<int> tris,
                                           Polyline boundary, List<Polyline> holes,
                                           int maxPasses = 5, float epsilonDeg = 0.0f)
        {
            // ---- helpers ----
            static float Orient2D(Vector2 a, Vector2 b, Vector2 c)
            {
                return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            }
            static float InCircle(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                float adx = a.x - d.x, ady = a.y - d.y;
                float bdx = b.x - d.x, bdy = b.y - d.y;
                float cdx = c.x - d.x, cdy = c.y - d.y;

                float ad2 = adx * adx + ady * ady;
                float bd2 = bdx * bdx + bdy * bdy;
                float cd2 = cdx * cdx + cdy * cdy;

                return ad2 * (bdx * cdy - bdy * cdx)
                     - bd2 * (adx * cdy - ady * cdx)
                     + cd2 * (adx * bdy - ady * bdx);
            }
            static float SignedArea(Vector3 A, Vector3 B, Vector3 C)
            {
                return (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
            }
            static int OtherVertex(List<int> T, int o, int a, int b)
            {
                for (int e = 0; e < 3; e++)
                {
                    int v = T[o + e];
                    if (v != a && v != b) return v;
                }
                throw new InvalidOperationException("Degenerate triangle in OtherVertex");
            }

            // 1) Build a fast position->index map for fixed edges
            var indexMap = new Dictionary<(int, int), int>(vertices.Count);
            const float snap = 1e-6f;
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                int sx = Mathf.RoundToInt(v.x / snap);
                int sy = Mathf.RoundToInt(v.y / snap);
                indexMap[(sx, sy)] = i;
            }
            int FindNearestIndex(Vector2 p)
            {
                int sx = Mathf.RoundToInt(p.x / snap);
                int sy = Mathf.RoundToInt(p.y / snap);
                if (indexMap.TryGetValue((sx, sy), out int idx)) return idx;

                // fallback (rare): linear search
                int best = 0; float best2 = float.MaxValue;
                for (int i = 0; i < vertices.Count; i++)
                {
                    var d = (Vector2)vertices[i] - p;
                    float d2 = d.sqrMagnitude;
                    if (d2 < best2) { best2 = d2; best = i; }
                }
                return best;
            }

            // 2) Collect fixed edges (outer and holes)
            var fixedEdges = new HashSet<Edge>();
            void CollectRing(Polyline ring)
            {
                int n = ring.Count;
                for (int i = 0; i < n; i++)
                {
                    int ni = (i + 1) % n;
                    int ai = FindNearestIndex(ring[i]);
                    int bi = FindNearestIndex(ring[ni]);
                    fixedEdges.Add(new Edge(ai, bi));
                }
            }
            CollectRing(boundary);
            foreach (var h in holes) CollectRing(h);

            // 3) Region test
            bool PointInRing(Vector2 p, Polyline ring)
            {
                bool inside = false;
                int n = ring.Count;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    var vi = (Vector2)ring[i];
                    var vj = (Vector2)ring[j];
                    if (((vi.y > p.y) != (vj.y > p.y)) &&
                        (p.x < (vj.x - vi.x) * (p.y - vi.y) / (vj.y - vi.y) + vi.x))
                        inside = !inside;
                }
                return inside;
            }
            bool IsInsideRegion(Vector2 p)
            {
                if (!PointInRing(p, boundary)) return false;
                foreach (var h in holes) if (PointInRing(p, h)) return false;
                return true;
            }

            // 4) adjacency builder
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
                            list = new List<int>(2);
                            adj[edge] = list;
                        }
                        list.Add(t / 3);
                    }
                }
                return adj;
            }

            // 5) main loop — flip edges one-by-one; no batch conflicts
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool changed = false;
                var adj = BuildAdjacency();

                foreach (var kv in adj)
                {
                    var edge = kv.Key;
                    var owners = kv.Value;
                    if (owners.Count != 2) continue;
                    if (fixedEdges.Contains(edge)) continue;

                    int t0 = owners[0], t1 = owners[1];
                    int o0 = t0 * 3, o1 = t1 * 3;

                    int i = edge.A, j = edge.B;
                    int k = OtherVertex(tris, o0, i, j);
                    int l = OtherVertex(tris, o1, i, j);

                    var vi = (Vector2)vertices[i];
                    var vj = (Vector2)vertices[j];
                    var vk = (Vector2)vertices[k];
                    var vl = (Vector2)vertices[l];

                    // convexity across (i,j)
                    float s1 = Orient2D(vi, vj, vk);
                    float s2 = Orient2D(vi, vj, vl);
                    if (s1 == 0f || s2 == 0f) continue;
                    if (s1 * s2 >= 0f) continue; // not convex

                    // Delaunay: ensure (i,k,j) is CCW for test
                    Vector2 a = vi, b = vk, c = vj, d = vl;
                    if (Orient2D(a, b, c) <= 0f) { b = vj; c = vk; }
                    if (InCircle(a, b, c, d) <= 0f) continue; // no improvement

                    // new tris CCW
                    if (SignedArea(vertices[k], vertices[i], vertices[l]) <= 0f) continue;
                    if (SignedArea(vertices[l], vertices[j], vertices[k]) <= 0f) continue;

                    // keep inside region
                    var mid = 0.5f * (vk + vl);
                    if (!IsInsideRegion(mid)) continue;

                    // apply flip immediately and restart this pass (local adj changed)
                    int o0i0 = tris[o0 + 0], o0i1 = tris[o0 + 1], o0i2 = tris[o0 + 2];
                    int o1i0 = tris[o1 + 0], o1i1 = tris[o1 + 1], o1i2 = tris[o1 + 2];

                    tris[o0 + 0] = k; tris[o0 + 1] = i; tris[o0 + 2] = l;
                    tris[o1 + 0] = l; tris[o1 + 1] = j; tris[o1 + 2] = k;

                    changed = true;
                    // rebuild adjacency after each successful flip to avoid conflicts
                    adj = BuildAdjacency();
                }

                if (!changed) break;
            }
        }
    }
}