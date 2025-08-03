using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC
{
    public class NavMesh2d : MonoBehaviour
    {
        private enum SimplificationAlgorithm { GreedyVertexDecimation, RamerDouglasPeucker };

        [SerializeField] 
        private int                        setCellSize;
        [SerializeField] 
        private AgentType                  agentType;
        [SerializeField] 
        private LayerMask                  obstacleMask;
        [SerializeField] 
        private SimplificationAlgorithm    simplificationAlgorithm = SimplificationAlgorithm.RamerDouglasPeucker;
        [SerializeField, ShowIf(nameof(needSimplificationMaxDistance))] 
        private float                      simplificationMaxDistance = 10.0f;

        [SerializeField]
        private bool debugGrid;
        [SerializeField, ShowIf(nameof(debugGrid))]
        private bool colorByRegion;
        [SerializeField]
        private bool debugContours;
        [SerializeField]
        private bool debugPolygons;

        bool needSimplificationMaxDistance => simplificationAlgorithm == SimplificationAlgorithm.GreedyVertexDecimation;

        [Serializable]
        class RegionData
        {
            public byte                 regionId;
            public Polyline             boundary;
            public List<Polyline>       holes;
            public List<Vector3>        vertices;
            public List<List<int>>      polygons;
            public List<List<int>>      adjacency;
        };

        [SerializeField, HideInInspector] private int           cellSize;
        [SerializeField, HideInInspector] private Vector2Int    gridSize;
        [SerializeField, HideInInspector] private Vector2       gridOffset;

        [SerializeField, HideInInspector]
        byte[] grid;
        [SerializeField, HideInInspector]
        byte[] region;
        [SerializeField, HideInInspector]
        List<RegionData> regionData;

        bool hasValidGrid => (grid != null) && (grid.Length == gridSize.x * gridSize.y);
        bool hasValidRegions => (regionData != null) && (regionData.Count > 0);

        [Button("Clear")]
        public void Clear()
        {
            grid = null;
            region = null;
            regionData = null;
        }

        [Button("Bake NavMesh")]
        public void Bake()
        {
            Clear();
            cellSize = setCellSize;
            CreateGridMap();
            if (agentType != null)
                GrowMap();
            ComputeRegions();
            ExtractContours();
            Simplify();
            Polygonize();
            MergeToConvex();
        }

        #region CreateGridMap
        void CreateGridMap()
        {
            ComputeGridSize();

            grid = new byte[gridSize.x * gridSize.y];
            int index = 0;

            var contactFilter = new ContactFilter2D();
            contactFilter.layerMask = obstacleMask;
            var results = new Collider2D[32];

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    Vector2 boxCenterPos = GridToWorldCenter(x, y);

                    Array.Clear(results, 0, results.Length);
                    if (Physics2D.OverlapBox(boxCenterPos, Vector2.one * cellSize, 0, contactFilter, results) > 0)
                    {
                        for (int i = 0; i < results.Length; i++)
                        {
                            if (results[i] != null)
                            {
                                if (results[i].gameObject.isStatic)
                                {
                                    grid[index] = 1;
                                    break;
                                }
                            }
                        }
                    }
                    index++;
                }
            }
        }


        void ComputeGridSize()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (!collider.gameObject.isStatic) continue;
                if (((1 << collider.gameObject.layer) & obstacleMask.value) == 0) continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning("No static colliders found in obstacle mask.");
                gridSize = Vector2Int.zero;
                gridOffset = Vector2.zero;
                return;
            }
            else
            {
                // Expand slightly to be safe
                bounds.Expand(0.01f);

                Vector2 min = bounds.min;
                Vector2 size = bounds.size;

                gridSize = new Vector2Int(
                    Mathf.CeilToInt(size.x / cellSize),
                    Mathf.CeilToInt(size.y / cellSize)
                );

                // Grid offset is the world position of the first (bottom-left) cell center
                gridOffset = min + Vector2.one * cellSize * 0.5f;
            }
        }

        Vector2 GridToWorldCenter(int x, int y)
        {
            return new Vector2(x, y) * cellSize + gridOffset;
        }
        #endregion

        #region GrowMap
        void GrowMap()
        {
            int radiusInCells = Mathf.CeilToInt(agentType.agentRadius / cellSize);
            if (radiusInCells <= 0) return;

            byte[] grownGrid = new byte[grid.Length];
            Array.Copy(grid, grownGrid, grid.Length); // Copy original obstacles

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    int index = y * gridSize.x + x;
                    if (grid[index] != 1) continue; // Only grow from original obstacles

                    for (int dy = -radiusInCells; dy <= radiusInCells; dy++)
                    {
                        for (int dx = -radiusInCells; dx <= radiusInCells; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx < 0 || nx >= gridSize.x || ny < 0 || ny >= gridSize.y) continue;

                            int nIndex = ny * gridSize.x + nx;
                            if ((grid[nIndex] == 0) && (grownGrid[nIndex] == 0))
                                grownGrid[nIndex] = 2; // mark as newly grown
                        }
                    }
                }
            }

            grid = grownGrid;
        }
        #endregion

        #region Regions
        void ComputeRegions()
        {
            regionData = new();
            region = new byte[grid.Length];
            for (int i = 0; i < region.Length; i++)
                region[i] = byte.MaxValue; // unassigned

            byte currentRegion = 0;

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    int index = y * gridSize.x + x;

                    // Only flood from unassigned walkable cells
                    if (grid[index] != 0 || region[index] != byte.MaxValue)
                        continue;

                    // Start a new region
                    FloodFillRegion(x, y, currentRegion);

                    regionData.Add(new RegionData()
                    {
                        regionId = currentRegion
                    });

                    currentRegion++;

                    if (currentRegion == byte.MaxValue)
                    {
                        Debug.LogWarning("Exceeded max number of regions (255).");
                        return;
                    }
                }
            }

            Debug.Log($"Computed {currentRegion} walkable regions.");
        }

        void FloodFillRegion(int startX, int startY, byte regionId)
        {
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                int x = pos.x;
                int y = pos.y;
                int index = y * gridSize.x + x;

                if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y)
                    continue;

                if (grid[index] != 0 || region[index] != byte.MaxValue)
                    continue;

                region[index] = regionId;

                // 4-directional flood fill (you can add diagonals if needed)
                queue.Enqueue(new Vector2Int(x - 1, y));
                queue.Enqueue(new Vector2Int(x + 1, y));
                queue.Enqueue(new Vector2Int(x, y - 1));
                queue.Enqueue(new Vector2Int(x, y + 1));
            }
        }

        #endregion

        #region Contours
        void ExtractContours()
        {
            int w = gridSize.x, h = gridSize.y;
            Vector2 worldMin = gridOffset - Vector2.one * (cellSize * 0.5f);

            // Prepare RegionData entries
            foreach (var rd in regionData)
            {
                rd.boundary = null;
                rd.holes = new List<Polyline>();
            }

            // For each region, collect and build its loops
            for (int r = 0; r < regionData.Count; r++)
            {
                byte rid = regionData[r].regionId;
                // Map from corner -> list of next corners
                var edgeMap = new Dictionary<Vector2, List<Vector2>>();

                // 1) Collect every exposed edge of each cell in this region
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        if (region[idx] != rid) continue;

                        // compute the four world-space corners of cell (x,y)
                        Vector2 bl = worldMin + new Vector2(x * cellSize, y * cellSize);
                        Vector2 br = bl + new Vector2(cellSize, 0);
                        Vector2 tl = bl + new Vector2(0, cellSize);
                        Vector2 tr = bl + new Vector2(cellSize, cellSize);

                        // if neighbor is out-of-bounds or not same region -> this edge is on the contour
                        // left
                        if (x == 0 || region[idx - 1] != rid) AddEdge(edgeMap, bl, tl);
                        // right
                        if (x == w - 1 || region[idx + 1] != rid) AddEdge(edgeMap, tr, br);
                        // bottom
                        if (y == 0 || region[idx - w] != rid) AddEdge(edgeMap, br, bl);
                        // top
                        if (y == h - 1 || region[idx + w] != rid) AddEdge(edgeMap, tl, tr);
                    }

                // 2) Stitch edges into closed loops
                var loops = BuildLoops(edgeMap);

                // 3) Turn loops into Polylines, classify by area
                float maxArea = float.MinValue;
                int boundaryLoop = -1;
                var plines = new List<Polyline>(loops.Count);
                var areas = new List<float>(loops.Count);

                for (int i = 0; i < loops.Count; i++)
                {
                    var pts = loops[i];
                    float a = SignedArea(pts);
                    areas.Add(a);

                    var poly = new Polyline();
                    foreach (var v in pts)
                        poly.Add(new Vector3(v.x, v.y, 0f));
                    poly.Add(pts[0]);
                    poly.isClosed = true;

                    plines.Add(poly);
                    if (Mathf.Abs(a) > maxArea)
                    {
                        maxArea = Mathf.Abs(a);
                        boundaryLoop = i;
                    }
                }

                // assign boundary & holes
                if (boundaryLoop >= 0)
                {
                    var outer = plines[boundaryLoop];
                    if (areas[boundaryLoop] < 0) // ensure CCW
                        outer.ReverseOrder();
                    regionData[r].boundary = outer;

                    for (int i = 0; i < plines.Count; i++)
                    {
                        if (i == boundaryLoop) continue;
                        var hole = plines[i];
                        if (areas[i] > 0) // ensure CW
                            hole.ReverseOrder();
                        regionData[r].holes.Add(hole);
                    }
                }
            }
        }

        // Helper to record a directed edge from -> to
        void AddEdge(Dictionary<Vector2, List<Vector2>> map, Vector2 from, Vector2 to)
        {
            if (!map.TryGetValue(from, out var list))
            {
                list = new List<Vector2>();
                map[from] = list;
            }
            list.Add(to);
        }

        List<List<Vector2>> BuildLoops(Dictionary<Vector2, List<Vector2>> map)
        {
            var loops = new List<List<Vector2>>();

            while (map.Count > 0)
            {
                // Grab any entry directly
                var e = map.First();
                Vector2 start = e.Key;
                Vector2 curr = start;
                Vector2 next = e.Value[0];
                RemoveEdge(map, curr, next);

                var loop = new List<Vector2> { curr };
                while (next != start)
                {
                    curr = next;
                    loop.Add(curr);
                    var nbrs = map[curr];
                    next = nbrs[0];
                    RemoveEdge(map, curr, next);
                }
                loops.Add(loop);
            }
            return loops;
        }

        void RemoveEdge(Dictionary<Vector2, List<Vector2>> map, Vector2 from, Vector2 to)
        {
            var lst = map[from];
            lst.RemoveAt(0);
            if (lst.Count == 0) map.Remove(from);
        }

        // Signed 2D polygon area (positive = CCW)
        float SignedArea(List<Vector2> pts)
        {
            float a = 0;
            for (int i = 0, n = pts.Count; i < n; i++)
            {
                Vector2 p0 = pts[i];
                Vector2 p1 = pts[(i + 1) % n];
                a += p0.x * p1.y - p1.x * p0.y;
            }
            return a * 0.5f;
        }


        #endregion

        #region Simplification
        public void Simplify()
        {
            switch (simplificationAlgorithm)
            {
                case SimplificationAlgorithm.GreedyVertexDecimation:
                    foreach (var region in regionData)
                    {
                        if (region.boundary != null) region.boundary.Simplify(simplificationMaxDistance);
                        if (region.holes != null)
                        {
                            foreach (var hole in region.holes)
                            {
                                hole.Simplify(simplificationMaxDistance);
                            }
                        }
                    }
                    break;
                case SimplificationAlgorithm.RamerDouglasPeucker:
                    float epsilon = cellSize;
                    foreach (var region in regionData)
                    {
                        if (region.boundary != null) region.boundary = region.boundary.SimplifyRDP(epsilon, true);
                        if (region.holes != null)
                        {
                            for (int i = 0; i < region.holes.Count; i++)
                            {
                                region.holes[i] = region.holes[i].SimplifyRDP(epsilon, true);
                            }
                        }
                    }
                    break;
                default:
                    break;
            }

            foreach (var region in regionData)
            {
                if (region.boundary != null) region.boundary.RemoveDuplicates();
                if (region.holes != null)
                {
                    foreach (var hole in region.holes)
                    {
                        hole.RemoveDuplicates();
                    }
                }
            }
        }
        #endregion

        #region Polygonization
        void Polygonize()
        {
            // Make sure regionData is populated
            if (regionData == null) return;

            foreach (var rd in regionData)
            {
                // Prepare containers
                rd.vertices = new List<Vector3>();
                rd.polygons = new();

                // Skip empty regions
                if (rd.boundary == null) continue;

                // 1) Flatten coords into a double[] for Earcut, and build rd.vertices
                var coords = new List<float>();
                var holeIndices = new List<int>();

                // Outer boundary
                foreach (var v in rd.boundary.GetVertices())
                {
                    coords.Add(v.x);
                    coords.Add(v.y);
                    rd.vertices.Add(v);
                }

                // Holes
                int holeCount = 0;
                foreach (var hole in rd.holes)
                {
                    // mark the start index of this hole (in *vertices*, not coords)
                    holeIndices.Add(coords.Count / 2);
                    foreach (var v in hole.GetVertices())
                    {
                        coords.Add(v.x);
                        coords.Add(v.y);
                        rd.vertices.Add(v);
                    }
                    holeCount++;
                }

                // 2) Triangulate
                var indices = MadWorldNL.EarCut.Tessellate(coords.ToArray(), holeIndices.Count > 0 ? holeIndices.ToArray() : null, 2);
                for (int i = 0; i < indices.Count; i+=3)
                {
                    var poly = new List<int>() { indices[i], indices[i + 1], indices[i + 2] };
                    rd.polygons.Add(poly);
                }              
            }

            Debug.Log($"Polygonize: created triangulation + adjacency for {regionData.Count} regions.");
        }
        #endregion

        #region Merge to convex

        void MergeToConvex()
        {
            foreach (var rd in regionData)
            {
                // 1) Turn each triangle into a NavCell
                var cells = new List<NavCell>();
                foreach (var tri in rd.polygons)
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
                            if (TryMerge(A, B, rd.vertices, out var M))
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
                rd.polygons = cells.Select(c => c.vertexIndices).ToList();

                // 5) Write back adjacency (indices into rd.polygons list)
                //    Build a map from cell -> its index in 'cells'
                var idxMap = new Dictionary<NavCell, int>();
                for (int i = 0; i < cells.Count; i++)
                    idxMap[cells[i]] = i;

                rd.adjacency = new List<List<int>>(cells.Count);
                foreach (var c in cells)
                {
                    var neighIdxs = c.neighbors
                                      .Select(n => idxMap[n])
                                      .ToList();
                    rd.adjacency.Add(neighIdxs);
                }
            }
        }

        // --------------------------------------------------
        // Helper types & methods (as before)
        // --------------------------------------------------

        class NavCell
        {
            public List<int> vertexIndices;
            public HashSet<NavCell> neighbors = new HashSet<NavCell>();

            public NavCell(IEnumerable<int> idxs)
            {
                vertexIndices = idxs.ToList();
            }
        }

        void BuildAdjacency(List<NavCell> cells)
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

        bool SharesEdge(NavCell A, NavCell B)
            => A.vertexIndices.Intersect(B.vertexIndices).Count() == 2;

        // Try to merge A & B. Returns true + M when union(A,B) is convex.
        bool TryMerge(NavCell A, NavCell B, List<Vector3> verts3D, out NavCell M)
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

        void AppendPath(
            List<int> ring,
            int start,
            int end,
            List<int> outList,
            bool includeStart = true,
            bool includeEnd = true
        )
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

        bool IsConvex(List<int> poly, List<Vector3> verts3D)
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


        #endregion

        #region Debug and Gizmos
        static Color[] regionColors =
        {
            new Color(0.0f, 1.0f, 1.0f, 0.25f),
            new Color(0.0f, 1.0f, 0.0f, 0.25f),
            new Color(1.0f, 1.0f, 0.0f, 0.25f),
            new Color(1.0f, 0.0f, 1.0f, 0.25f),
            new Color(0.0f, 0.0f, 1.0f, 0.25f),
            new Color(1.0f, 1.0f, 1.0f, 0.25f)
        };

        void OnDrawGizmosSelected()
        {
            if ((debugGrid) && (hasValidGrid))
            {
                bool draw = true;
                int index = 0;
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int x = 0; x < gridSize.x; x++)
                    {
                        Vector2 boxCenterPos = GridToWorldCenter(x, y);
                        if ((colorByRegion) && (region != null) && (region.Length == grid.Length))
                        {

                            switch (grid[index])
                            {
                                case 0:
                                    Gizmos.color = regionColors[region[index] % regionColors.Length];
                                    draw = true;
                                    break;
                                default:
                                    draw = false;
                                    break;
                            }
                        }
                        else
                        {
                            switch (grid[index])
                            {
                                case 0: Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.25f); break;
                                case 1: Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.25f); break;
                                case 2: Gizmos.color = new Color(1.0f, 1.0f, 0.0f, 0.25f); break;
                            }
                        }
                        if (draw) Gizmos.DrawCube(boxCenterPos, Vector2.one * cellSize);

                        index++;
                    }
                }
            }
            if ((debugContours) && (hasValidRegions))
            {
                int index = 0;
                for (int i = 0; i < regionData.Count; i++) 
                {
                    var region = regionData[i];

                    if (region.boundary != null)
                    {
                        Gizmos.color = regionColors[i % regionColors.Length].ChangeAlpha(1.0f);
                        region.boundary.DrawGizmos();
                    }
                    if (region.holes != null)
                    {
                        foreach (var boundary in region.holes)
                        {
                            Gizmos.color = Color.red;
                            boundary.DrawGizmos();
                            index++;
                        }
                    }
                }
            }

            if ((debugPolygons) && (hasValidRegions))
            {
                for (int i = 0; i < regionData.Count; i++)
                {
                    var region = regionData[i];

                    Gizmos.color = regionColors[i % regionColors.Length];
                    if ((region.polygons != null) && (region.vertices != null))
                    {
                        for (int j = 0; j < region.polygons.Count; j++)
                        {
                            var poly = region.polygons[j];
                            
                            Vector3[] vertices = new Vector3[poly.Count];
                            for (int k = 0; k < poly.Count; k++)
                            {
                                vertices[k] = region.vertices[poly[k]];
                            }

                            DebugHelpers.DrawWireConvexPolygon(vertices);
                            DebugHelpers.DrawConvexPolygon(vertices);
                        }
                    }
                }
            }
        }
        #endregion
    }
}
