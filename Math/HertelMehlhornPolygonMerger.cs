using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public static class HertelMehlhornPolygonMerger
    {
        // Undirected edge key for adjacency
        struct Edge : IEquatable<Edge>
        {
            public readonly int A, B;
            public Edge(int v1, int v2)
            {
                if (v1 < v2) { A = v1; B = v2; }
                else { A = v2; B = v1; }
            }
            public bool Equals(Edge other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is Edge o && Equals(o);
            public override int GetHashCode() => (A * 397) ^ B;
        }

        public static void Merge(List<Vector3> vertices, List<List<int>> inPolygons, ref List<List<int>> outPolygons)
        {
            // start from the input list
            var polys = new List<List<int>>(inPolygons);

            bool anyMerge = true;
            while (anyMerge)
            {
                anyMerge = false;
                // build edge->polygons adjacency
                var adjacency = BuildAdjacency(polys);

                // try every shared edge once
                foreach (var kv in adjacency)
                {
                    var edge = kv.Key;
                    var shared = kv.Value;
                    // only interior diagonals (exactly two owners)
                    if (shared.Count != 2)
                        continue;

                    int idxA = shared[0];
                    int idxB = shared[1];
                    var polyA = polys[idxA];
                    var polyB = polys[idxB];

                    // merge the two along this edge
                    var merged = MergeOnEdge(polyA, polyB, edge.A, edge.B);

                    // test convexity
                    if (IsConvex(vertices, merged))
                    {
                        // remove the two old polys and add the merged one
                        int hi = Mathf.Max(idxA, idxB);
                        int lo = Mathf.Min(idxA, idxB);
                        polys.RemoveAt(hi);
                        polys.RemoveAt(lo);
                        polys.Add(merged);

                        anyMerge = true;
                        break;  // restart adjacency
                    }
                }
            }

            // output result
            outPolygons = polys;
        }

        static Dictionary<Edge, List<int>> BuildAdjacency(List<List<int>> polys)
        {
            var adj = new Dictionary<Edge, List<int>>();
            for (int i = 0; i < polys.Count; i++)
            {
                var poly = polys[i];
                int m = poly.Count;
                for (int j = 0; j < m; j++)
                {
                    int v1 = poly[j];
                    int v2 = poly[(j + 1) % m];
                    var e = new Edge(v1, v2);
                    if (!adj.TryGetValue(e, out var list))
                    {
                        list = new List<int>();
                        adj[e] = list;
                    }
                    list.Add(i);
                }
            }
            return adj;
        }

        static List<int> MergeOnEdge(List<int> polyA, List<int> polyB, int a, int b)
        {
            var merged = new List<int>();

            // 1) walk polyA from a -> b via the long way around
            int nA = polyA.Count;
            int posA = polyA.IndexOf(a);
            int nextA = polyA[(posA + 1) % nA];
            bool forwardA = nextA != b; // if next is b, the long way is backward
            if (forwardA)
            {
                int idx = posA;
                do
                {
                    merged.Add(polyA[idx]);
                    idx = (idx + 1) % nA;
                } while (idx != polyA.IndexOf(b));
                merged.Add(b);
            }
            else
            {
                int idx = posA;
                do
                {
                    merged.Add(polyA[idx]);
                    idx = (idx - 1 + nA) % nA;
                } while (idx != polyA.IndexOf(b));
                merged.Add(b);
            }

            // 2) walk polyB from b -> a via the long way around
            int nB = polyB.Count;
            int posB = polyB.IndexOf(b);
            int nextB = polyB[(posB + 1) % nB];
            bool forwardB = nextB != a;
            if (forwardB)
            {
                int idx = posB;
                while (true)
                {
                    idx = (idx + 1) % nB;
                    if (polyB[idx] == a) break;
                    merged.Add(polyB[idx]);
                }
            }
            else
            {
                int idx = posB;
                while (true)
                {
                    idx = (idx - 1 + nB) % nB;
                    if (polyB[idx] == a) break;
                    merged.Add(polyB[idx]);
                }
            }

            return merged;
        }

        static bool IsConvex(List<Vector3> verts, List<int> poly)
        {
            int m = poly.Count;
            if (m < 3) return false;

            bool hasNeg = false, hasPos = false;
            for (int i = 0; i < m; i++)
            {
                Vector3 prev = verts[poly[(i - 1 + m) % m]];
                Vector3 curr = verts[poly[i]];
                Vector3 next = verts[poly[(i + 1) % m]];

                Vector3 v1 = curr - prev;
                Vector3 v2 = next - curr;
                float cross = v1.x * v2.y - v1.y * v2.x;
                if (cross < 0f) hasNeg = true;
                else if (cross > 0f) hasPos = true;
                if (hasNeg && hasPos)
                    return false;
            }
            return true;
        }
    }
}