using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC
{
    public static class GreedyPolygonMerger
    {
        class NavCell
        {
            public List<int> vertexIndices;
            public HashSet<NavCell> neighbors = new HashSet<NavCell>();

            public NavCell(IEnumerable<int> idxs)
            {
                vertexIndices = idxs.ToList();
            }
        }

        public static void Merge(List<Vector3> vertices, List<List<int>> inPolygons, ref List<List<int>> outPolygons)
        {
            // 1) Turn each triangle into a NavCell
            var cells = new List<NavCell>();
            foreach (var tri in inPolygons)
                cells.Add(new NavCell(tri));

            // 2) Build initial adjacency
            BuildAdjacency(cells);

            // 3) Greedily merge until no more convex merges are possible
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < cells.Count && !merged; i++)
                {
                    var A = cells[i];
                    foreach (var B in A.neighbors)
                    {
                        if (TryMerge(A, B, vertices, out var M))
                        {
                            // remove A & B
                            cells.Remove(A);
                            cells.Remove(B);

                            // disconnect them
                            foreach (var n in A.neighbors) n.neighbors.Remove(A);
                            foreach (var n in B.neighbors) n.neighbors.Remove(B);

                            // add M and rebuild its adjacency
                            cells.Add(M);
                            foreach (var other in cells)
                            {
                                if (other == M) continue;
                                if (SharesEdge(M, other))
                                {
                                    M.neighbors.Add(other);
                                    other.neighbors.Add(M);
                                }
                            }

                            merged = true;
                            break;
                        }
                    }
                }
            } while (merged);

            // 4) Write back polygons
            outPolygons = cells.Select(c => c.vertexIndices).ToList();
        }

        static void BuildAdjacency(List<NavCell> cells)
        {
            var edgeMap = new Dictionary<(int, int), NavCell>();
            foreach (var cell in cells)
            {
                int n = cell.vertexIndices.Count;
                for (int i = 0; i < n; i++)
                {
                    int a = cell.vertexIndices[i];
                    int b = cell.vertexIndices[(i + 1) % n];
                    var key = a < b ? (a, b) : (b, a);
                    if (edgeMap.TryGetValue(key, out var other))
                    {
                        cell.neighbors.Add(other);
                        other.neighbors.Add(cell);
                    }
                    else edgeMap[key] = cell;
                }
            }
        }

        static bool TryMerge(NavCell A, NavCell B, List<Vector3> verts3D, out NavCell M)
        {
            // 1) Find their two shared vertices
            var shared = A.vertexIndices.Intersect(B.vertexIndices).ToList();
            if (shared.Count != 2)
            {
                M = null;
                return false;
            }
            int v0 = shared[0], v1 = shared[1];

            // 2) Build the union loop WITHOUT duplicating v0 or v1
            var loop = new List<int>();
            AppendPath(A.vertexIndices, v0, v1, loop, includeEnd: true);
            AppendPath(B.vertexIndices, v1, v0, loop, includeStart: false, includeEnd: false);

            // 3) Quick degenerate-check
            if (loop.Count < 3) { M = null; return false; }

            // 4) Test convexity
            if (!IsConvex(loop, verts3D)) { M = null; return false; }

            // 5) Success
            M = new NavCell(loop);
            return true;
        }

        static bool SharesEdge(NavCell A, NavCell B) => A.vertexIndices.Intersect(B.vertexIndices).Count() == 2;

        static void AppendPath(List<int> ring, int start, int end, List<int> outList, bool includeStart = true, bool includeEnd = true)
        {
            int n = ring.Count;
            // find the index of 'start' in ring
            int idx = ring.IndexOf(start);
            if (idx < 0) return;

            // optionally skip the very first vertex
            if (!includeStart) idx = (idx + 1) % n;

            // walk forward until we've just added 'end'
            while (true)
            {
                int v = ring[idx];
                // if we're at the end and shouldn't include it, break
                if (v == end && !includeEnd) break;

                outList.Add(v);
                if (v == end) break;

                idx = (idx + 1) % n;
            }
        }

        static bool IsConvex(List<int> poly, List<Vector3> verts3D)
        {
            bool gotPos = false, gotNeg = false;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = (Vector2)verts3D[poly[i]];
                var b = (Vector2)verts3D[poly[(i + 1) % n]];
                var c = (Vector2)verts3D[poly[(i + 2) % n]];
                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                if (cross > 0) gotPos = true;
                if (cross < 0) gotNeg = true;
                if (gotPos && gotNeg) return false;
            }
            return true;    
        }
    }
}