using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.VisualScripting;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{
    public class NavMesh2d : MonoBehaviour
    {
        private enum SimplificationAlgorithm { None, GreedyVertexDecimation, RamerDouglasPeucker };
        private enum PathMode { PolygonCenter, MidEdge };
        public enum PathState { Thinking, NoPath, Partial, Full };

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
        private PathMode                    pathMode = PathMode.MidEdge;
        [SerializeField]
        private bool                        funnelEnable = true;

        [SerializeField]
        private bool debugEnabled;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool debugGrid;
        [SerializeField, ShowIf(nameof(needColorByRegion))]
        private bool colorByRegion;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool debugContours;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool debugPolygons;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool debugTestNearPoint;
        [SerializeField, ShowIf(nameof(needTestPoint))]
        private Transform testPoint;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool debugTestPath;
        [SerializeField, ShowIf(nameof(isTestPath))]
        private Transform startPoint;
        [SerializeField, ShowIf(nameof(isTestPath))]
        private Transform endPoint;
        [SerializeField, ShowIf(nameof(needTestRegion))]
        private int       testRegionId;

        bool needSimplificationMaxDistance => simplificationAlgorithm == SimplificationAlgorithm.GreedyVertexDecimation;
        bool needTestPoint => debugEnabled && debugTestNearPoint;
        bool needTestRegion => debugEnabled && (debugTestNearPoint || debugTestPath);
        bool needColorByRegion => debugEnabled && debugGrid;
        bool isTestPath => debugEnabled && debugTestPath;

        public struct PathNode
        {
            public PathNode(Vector3 p) { pos = p; }

            public Vector3 pos;
        }


        [Serializable]
        class ConvexPolygon
        {
            public int              id;
            public List<int>        indices;
            public List<Vector3>    vertices;   // Can just refer the master list

            [SerializeField, HideInInspector]
            private Bounds2d        _bounds;
            [SerializeField, HideInInspector]
            private Vector3         _center;
            public List<int>        neighbors = new();

            public Bounds2d bounds => _bounds;
            public Vector3 center => _center;

            public int Count => (indices == null) ? (0) : (indices.Count);
            public int this[int index]
            {
                get
                {
                    // bounds-check, or throw your own exception
                    if ((indices == null) || (index < 0) || (index >= indices.Count))
                        throw new ArgumentOutOfRangeException(nameof(index));
                    return indices[index];
                }
                set
                {
                    // bounds-check, or throw your own exception
                    if ((indices == null) || (index < 0) || (index >= indices.Count))
                        throw new ArgumentOutOfRangeException(nameof(index));
                    indices[index] = value;
                }
            }

            public float    Distance(Vector2 point)
            {
                int n = indices.Count;
                float maxSd = float.MinValue;

                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    var vi = (Vector2)vertices[indices[i]];
                    var vj = (Vector2)vertices[indices[j]];
                    Vector2 edge = vi - vj;
                    // Outward normal for CCW: rotate edge right
                    Vector2 normal = new Vector2(edge.y, -edge.x).normalized;
                    // Signed distance from point to edge line
                    float sd = Vector2.Dot(normal, point - vi);
                    maxSd = Math.Max(maxSd, sd);
                }

                return maxSd;
            }

            public float Distance(Vector2 point, out Vector2 closestPoint)
            {
                int n = indices.Count;
                float maxSd = float.MinValue;
                float minDistSq = float.MaxValue;
                Vector2 bestPt = Vector2.zero;

                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    Vector2 vi = vertices[indices[i]];
                    Vector2 vj = vertices[indices[j]];

                    // 1) edge & outward normal (CCW winding)
                    Vector2 edge = vj - vi;
                    Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
                    float sd = Vector2.Dot(normal, point - vi);
                    maxSd = Mathf.Max(maxSd, sd);

                    // 2) projection onto [vi->vj]
                    float t = Vector2.Dot(point - vi, edge) / edge.sqrMagnitude;
                    t = Mathf.Clamp01(t);
                    Vector2 proj = vi + edge * t;

                    // 3) track closest
                    float dSq = (point - proj).sqrMagnitude;
                    if (dSq < minDistSq)
                    {
                        minDistSq = dSq;
                        bestPt = proj;
                    }
                }

                closestPoint = bestPt;
                float euclid = Mathf.Sqrt(minDistSq);
                return (maxSd > 0f) ? euclid : -euclid;
            }

            public bool     IsIntersecting(Bounds2d bounds)
            {
                // Get AABB corners
                Vector2[] boxCorners = new Vector2[4]
                {
                    bounds.min,
                    new Vector2(bounds.max.x, bounds.min.y),
                    bounds.max,
                    new Vector2(bounds.min.x, bounds.max.y)
                };
                int n = indices.Count;

                // Test the two AABB axes (X and Y)
                foreach (var axis in new[] { Vector2.right, Vector2.up })
                {
                    float minP = float.MaxValue, maxP = float.MinValue;
                    // Project polygon
                    for (int i = 0; i < n; i++)
                    {
                        var v = (Vector2)vertices[indices[i]];
                        float p = Vector2.Dot(axis, v);
                        minP = Math.Min(minP, p);
                        maxP = Math.Max(maxP, p);
                    }
                    // Project cell
                    float minB = float.MaxValue, maxB = float.MinValue;
                    foreach (var corner in boxCorners)
                    {
                        float p = Vector2.Dot(axis, corner);
                        minB = Math.Min(minB, p);
                        maxB = Math.Max(maxB, p);
                    }
                    if (maxP < minB || maxB < minP)
                        return false;
                }

                // Test polygon edge normals
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    var vi = (Vector2)vertices[indices[i]];
                    var vj = (Vector2)vertices[indices[j]];
                    Vector2 edge = vj - vi;
                    Vector2 axis = new Vector2(-edge.y, edge.x).normalized;

                    float minP = float.MaxValue, maxP = float.MinValue;
                    for (int k = 0; k < n; k++)
                    {
                        var vk = (Vector2)vertices[indices[k]];
                        float p = Vector2.Dot(axis, vk);
                        minP = Math.Min(minP, p);
                        maxP = Math.Max(maxP, p);
                    }

                    float minB = float.MaxValue, maxB = float.MinValue;
                    foreach (var corner in boxCorners)
                    {
                        float p = Vector2.Dot(axis, corner);
                        minB = Math.Min(minB, p);
                        maxB = Math.Max(maxB, p);
                    }

                    if (maxP < minB || maxB < minP)
                        return false;
                }

                // No separation found
                return true;
            }

            public void     UpdateGeometry()
            {
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = -min;

                _center = Vector3.zero;
                
                foreach (var index in indices)
                {
                    var v = vertices[index];
                    if (v.x < min.x) min.x = v.x;
                    if (v.y < min.y) min.y = v.y;
                    if (v.x > max.x) max.x = v.x;
                    if (v.y > max.y) max.y = v.y;

                    _center += v;
                }

                _bounds = new Bounds2d((min + max) * 0.5f, max - min);
                _center /= indices.Count;
            }

            public bool isCCW
            {
                get
                {
                    int n = indices.Count;
                    if (n < 3)
                        return false;  // degenerate

                    // Compute twice the signed area
                    float area2 = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 v1 = (Vector2)vertices[indices[i]];
                        Vector2 v2 = (Vector2)vertices[indices[(i + 1) % n]];
                        area2 += v1.x * v2.y - v2.x * v1.y;
                    }

                    // If signed area is positive, the winding is CCW
                    return area2 > 0f;
                }
            }

            public void InvertWinding()
            {
                indices.Reverse();
            }

            public void ForceCCW()
            {
                if (!isCCW) InvertWinding();
            }
            public void ForceCW()
            {
                if (isCCW) InvertWinding();
            }

            public IEnumerable<(int v1, int v2)> Edges()
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    yield return (indices[i], indices[(i + 1) % indices.Count]);
                }
            }

            public bool HasEdge((int v1, int v2) edge)
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % indices.Count];
                    if (((edge.v1 == i1) && (edge.v2 == i2)) ||
                        ((edge.v2 == i1) && (edge.v1 == i2)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        class PolyQuadtree : Quadtree<ConvexPolygon>
        {
            public PolyQuadtree(Vector2 min, Vector2 max, int nLevels) : base(min, max, nLevels) { }

            public void Add(ConvexPolygon poly)
            {
                Add(poly, IntersectionFunction);
            }

            private bool IntersectionFunction(ConvexPolygon polygon, Bounds2d bounds)
            {
                return polygon.IsIntersecting(bounds);
            }

            public ConvexPolygon FindClosest(Vector2 point, out float distance, out Vector2 closestPoint)
            {
                var ret = FindClosest(point, DistanceFunction, false, out distance, out closestPoint);

                return ret;
            }

            private float DistanceFunction(Vector2 point, ConvexPolygon polygon, out Vector2 closestPoint)
            {
                return polygon.Distance(point, out closestPoint);
            }
        }

        [Serializable]
        class RegionData
        {
            public byte                 regionId;
            public Polyline             boundary;
            public List<Polyline>       holes;
            public List<Vector3>        vertices;
            public List<ConvexPolygon>  polygons;
            public Bounds2d             bounds;
            
            private PolyQuadtree        _quadtree;

            void ComputeBounds()
            {
                if (polygons == null) return;

                bounds = null;
                foreach (var p in polygons)
                {
                    if (bounds == null) bounds = p.bounds;
                    else bounds.Encapsulate(p.bounds);
                }
            }

            public PolyQuadtree quadtree
            {
                get
                {
                    if (_quadtree == null)
                    {
                        ComputeBounds();

                        _quadtree = new PolyQuadtree(bounds.min, bounds.max, 3);
                        foreach (var polygon in polygons)
                        {
                            _quadtree.Add(polygon);
                        }
                    }
                    return _quadtree;
                }
            }
        };


        struct NavmeshPolyState : IComparable<NavmeshPolyState>
        {
            public int polyId;
            public Vector2 pos;
            public float cost;
            public float priority;
            public List<Vector2> path;

            public int CompareTo(NavmeshPolyState other) => priority.CompareTo(other.priority);
        }

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
            {
                GrowMap();
            }
            ComputeRegions();
            ExtractContours();
            Simplify();
            Polygonize();
            MergeToConvex();
            ComputeNeighbors();

#if UNITY_EDITOR
            // Record the change so Unity knows to save it
            Undo.RecordObject(this, "Rebuild Nav Regions");
            EditorUtility.SetDirty(this);
            // If you want the scene to show unsaved changes:
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
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

                List<int>       triangles = null;
                rd.boundary.Triangulate_EarCut(rd.holes, ref rd.vertices, ref triangles);

                ConstrainedDelaunayFlipper.EnforceDelaunay(rd.vertices, triangles, rd.boundary, rd.holes, 500, 1.0f);

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    var poly = new List<int>() { triangles[i], triangles[i + 1], triangles[i + 2] };
                    var convexPolygon = new ConvexPolygon { id = rd.polygons.Count, indices = poly, vertices = rd.vertices };

                    convexPolygon.UpdateGeometry();
                    rd.polygons.Add(convexPolygon);
                }
            }
        }

        #endregion

        #region Merge to convex

        void MergeToConvex()
        {
            // Need some marshaling, but that's life
            foreach (var rd in regionData)
            {    
                List<List<int>> polygons = new();
                foreach (var polygon in rd.polygons) polygons.Add(new(polygon.indices));

                HertelMehlhornPolygonMerger.Merge(rd.vertices, polygons, ref polygons);

                rd.polygons = new();
                foreach (var polygon in polygons)
                {
                    var convexPolygon = new ConvexPolygon { id = rd.polygons.Count, indices = polygon, vertices = rd.vertices };
                    convexPolygon.ForceCCW();
                    convexPolygon.UpdateGeometry();

                    rd.polygons.Add(convexPolygon);
                }
            }
        }

        #endregion

        #region Adjacency

        public void ComputeNeighbors()
        {
            foreach (var region in regionData)
            {
                var polys = region.polygons;
                for (int i = 0; i < polys.Count; i++)
                {
                    var pi = polys[i];
                    pi.neighbors.Clear();

                    foreach (var edge in pi.Edges())
                    {
                        int neighbour = -1;

                        for (int j = 0; j < polys.Count; j++)
                        {
                            if (i == j) continue;

                            var pj = polys[j];

                            if (pj.HasEdge(edge))
                            {
                                neighbour = j;
                                break;
                            }
                        }

                        pi.neighbors.Add(neighbour);
                    }

                }
            }
        }
        #endregion

        #region Queries

        public bool GetPointOnNavMesh(Vector3 point, int regionId, out Vector3 pt)
        {
            var rd = regionData[regionId];

            var retPolygon = rd.quadtree.FindClosest(point, out float distance, out Vector2 pt2d);

            if (distance < 0) pt = point;
            else pt = pt2d;

            return retPolygon != null;
        }

        public bool GetPointOnNavMesh(Vector3 point, int regionId, out int polygonId, out Vector3 pt)
        {
            var rd = regionData[regionId];

            var retPolygon = rd.quadtree.FindClosest(point, out float distance, out Vector2 pt2d);

            if (distance < 0) pt = point;
            else pt = pt2d;
            polygonId = retPolygon.id;

            return retPolygon != null;
        }

        public bool GetPointOnNavMesh(Vector3 point, out int regionId, out Vector3 pt)
        {
            ConvexPolygon retPolygon = null;
            Vector3 retPt = point;
            float retDist = float.MaxValue;
            int retRegionId = -1;

            if (regionData == null)
            {
                pt = point;
                regionId = -1;
                return false;
            }

            foreach (var rd in regionData)
            {
                var poly = rd.quadtree.FindClosest(point, out float distance, out Vector2 pt2d);
                if ((poly != null) && (retDist > distance))
                {
                    retDist = distance;
                    retPolygon = poly;
                    retRegionId = rd.regionId;
                    retPt = pt2d;
                }
            }

            if (retDist < 0) pt = point;
            else pt = retPt;
            regionId = retRegionId;

            return retPolygon != null;
        }

        public bool GetPointOnNavMesh(Vector3 point, out int regionId, out int polygonId, out Vector3 pt)
        {
            ConvexPolygon retPolygon = null;
            Vector3 retPt = point;
            float retDist = float.MaxValue;
            int retRegionId = -1;

            if (regionData == null)
            {
                pt = point;
                regionId = -1;
                polygonId = -1;
                return false;
            }

            foreach (var rd in regionData)
            {
                var poly = rd.quadtree.FindClosest(point, out float distance, out Vector2 pt2d);
                if ((poly != null) && (retDist > distance))
                {
                    retDist = distance;
                    retPolygon = poly;
                    retRegionId = rd.regionId;
                    retPt = pt2d;
                }
            }

            if (retDist < 0) pt = point;
            else pt = retPt;
            regionId = retRegionId;
            polygonId = retPolygon.id;

            return retPolygon != null;
        }


        public PathState PlanPath(Vector3 start, Vector3 end, ref int regionId, ref List<int> polygons, ref List<PathNode> path)
        {
            var startOnNavmesh = start;
            var endOnNavmesh = end;
            int startPolygonId = -1;
            int endPolygonId = -1;

            if ((regionId >= 0) && (regionId < regionData.Count))
            {
                if (!GetPointOnNavMesh(start, regionId, out startPolygonId, out startOnNavmesh))
                {
                    Debug.LogWarning("Can't find start point on navmesh!");
                    return PathState.NoPath;
                }
                if (!GetPointOnNavMesh(end, regionId, out endPolygonId, out endOnNavmesh))
                {
                    Debug.LogWarning("Can't find end point on navmesh!");
                    return PathState.NoPath;
                }
            }
            else
            {
                if (!GetPointOnNavMesh(start, out int startRegionId, out startPolygonId, out startOnNavmesh))
                {
                    Debug.LogWarning("Can't find start point on navmesh!");
                    return PathState.NoPath;
                }
                if (!GetPointOnNavMesh(end, out int endRegionId, out endPolygonId, out endOnNavmesh))
                {
                    Debug.LogWarning("Can't find end point on navmesh!");
                    return PathState.NoPath;
                }
                if (startRegionId != endRegionId)
                {
                    {
                        Debug.LogWarning("Can't find path between points in different regions!");
                        return PathState.NoPath;
                    }
                }
                regionId = startRegionId;
            }

            return PlanPathOnNavmesh(startOnNavmesh, startPolygonId, endOnNavmesh, endPolygonId, regionId, ref polygons, ref path);
        }

        public PathState PlanPathOnNavmesh(Vector3 start, int startPolygonId, Vector3 end, int endPolygonId, int regionId, ref List<int> polygons, ref List<PathNode> path)
        {
            PathState ret = PathState.NoPath;

            switch (pathMode)
            {
                case PathMode.PolygonCenter:
                    ret = PlanPathOnNavmeshPolygonCenter(start, startPolygonId, end, endPolygonId, regionId, ref polygons, ref path);
                    break;
                case PathMode.MidEdge:
                    ret = PlanPathOnNavmeshMidEdge(start, startPolygonId, end, endPolygonId, regionId, ref polygons, ref path);
                    break;
                default:
                    break;
            }

            if ((funnelEnable) && (path.Count > 2))
            {
                Funnel(polygons, path, regionId);
            }

            return ret;
        }

        public PathState PlanPathOnNavmeshPolygonCenter(Vector3 start, int startPolygonId, Vector3 end, int endPolygonId, int regionId, ref List<int> polygons, ref List<PathNode> path)
        {
            if (polygons == null) polygons = new();

            var region = regionData[regionId];
            var polys = region.polygons;

            var frontier = new PriorityQueue<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var costSoFar = new Dictionary<int, float>();
            var visited = new HashSet<int>();

            Vector2 endPos = polys[endPolygonId].center;

            frontier.Enqueue(startPolygonId, 0);
            costSoFar[startPolygonId] = 0;

            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();

                if (current == endPolygonId)
                    break;

                if (!visited.Add(current))
                    continue;

                var currentPoly = polys[current];
                Vector2 currentPos = currentPoly.center;

                foreach (var neighbor in currentPoly.neighbors)
                {
                    if (neighbor == -1) continue;

                    var neighborPoly = polys[neighbor];
                    Vector2 neighborPos = neighborPoly.center;

                    float newCost = costSoFar[current] + Vector2.Distance(currentPos, neighborPos);

                    if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                    {
                        costSoFar[neighbor] = newCost;
                        float priority = newCost + Vector2.Distance(neighborPos, endPos);
                        frontier.Enqueue(neighbor, priority);
                        cameFrom[neighbor] = current;
                    }
                }
            }

            // Reconstruct path
            List<int> pathPolyIds = new();
            int currentId = endPolygonId;

            if (!cameFrom.ContainsKey(endPolygonId) && startPolygonId != endPolygonId)
                return PathState.NoPath; // No path

            pathPolyIds.Add(currentId);
            while (currentId != startPolygonId)
            {
                currentId = cameFrom[currentId];
                pathPolyIds.Add(currentId);
            }
            pathPolyIds.Reverse();

            polygons.AddRange(pathPolyIds);

            // Convert to center path
            if (path == null) path = new List<PathNode>();
            if (pathPolyIds.Count > 0) path.Add(new (start));
            for (int i = 1; i < pathPolyIds.Count - 1; i++)
                path.Add(new (polys[pathPolyIds[i]].center));
            if (pathPolyIds.Count > 1) path.Add(new (end));

            return PathState.Full;
        }

        private PathState PlanPathOnNavmeshMidEdge(Vector3 start, int startPolygonId, Vector3 end, int endPolygonId, int regionId, ref List<int> polygons, ref List<PathNode> path)
        {
            if (polygons == null) polygons = new();

            var region = regionData[regionId];
            var polys = region.polygons;
            var frontier = new PriorityQueue<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var costSoFar = new Dictionary<int, float>();
            var visited = new HashSet<int>();

            Vector2 endPos = end;

            frontier.Enqueue(startPolygonId, 0);
            costSoFar[startPolygonId] = 0;

            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();
                if (current == endPolygonId)
                    break;

                if (!visited.Add(current))
                    continue;

                var currentPoly = polys[current];
                var vertices = region.vertices;

                for (int i = 0; i < currentPoly.neighbors.Count; i++)
                {
                    int neighbor = currentPoly.neighbors[i];
                    if (neighbor == -1) continue;

                    int vi = currentPoly[i];
                    int vj = currentPoly[(i + 1) % currentPoly.Count];
                    Vector2 edgeMid = 0.5f * ((Vector2)vertices[vi] + (Vector2)vertices[vj]);

                    float newCost = costSoFar[current] + Vector2.Distance((Vector2)start, edgeMid);
                    float priority = newCost + Vector2.Distance(edgeMid, endPos);

                    if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                    {
                        costSoFar[neighbor] = newCost;
                        frontier.Enqueue(neighbor, priority);
                        cameFrom[neighbor] = current;
                    }
                }
            }

            if (!cameFrom.ContainsKey(endPolygonId) && startPolygonId != endPolygonId)
                return PathState.NoPath;

            List<int> pathPolyIds = new();
            int currentId = endPolygonId;
            pathPolyIds.Add(currentId);
            while (currentId != startPolygonId)
            {
                currentId = cameFrom[currentId];
                pathPolyIds.Add(currentId);
            }
            pathPolyIds.Reverse();

            polygons.AddRange(pathPolyIds);

            if (path == null) path = new List<PathNode>();
            path.Add(new PathNode(start));
            for (int i = 1; i < pathPolyIds.Count; i++)
            {
                int prev = pathPolyIds[i - 1];
                int curr = pathPolyIds[i];

                var prevPoly = polys[prev];
                var verts = region.vertices;

                for (int j = 0; j < prevPoly.neighbors.Count; j++)
                {
                    if (prevPoly.neighbors[j] == curr)
                    {
                        int vi = prevPoly[j];
                        int vj = prevPoly[(j + 1) % prevPoly.Count];
                        Vector2 edgeMid = 0.5f * ((Vector2)verts[vi] + (Vector2)verts[vj]);
                        path.Add(new PathNode(edgeMid));
                        break;
                    }
                }
            }
            path.Add(new PathNode(end));

            return PathState.Full;
        }

        // Funnel implementation that modifies the path list in-place
        private void Funnel(List<int> polygons, List<PathNode> path, int regionId)
        {
            if (polygons == null || path == null || path.Count < 2 || polygons.Count < 1)
                return;

            var region = regionData[regionId];
            var polys = region.polygons;
            var verts = region.vertices;

            List<(Vector2 left, Vector2 right)> portals = new();

            for (int i = 0; i < polygons.Count - 1; i++)
            {
                var from = polys[polygons[i]];
                int to = polygons[i + 1];

                for (int j = 0; j < from.neighbors.Count; j++)
                {
                    if (from.neighbors[j] == to)
                    {
                        int vi = from[j];
                        int vj = from[(j + 1) % from.Count];
                        Vector2 a = verts[vi];
                        Vector2 b = verts[vj];

                        // Check if 'to' also has the same edge in reverse
                        var toPoly = polys[to];
                        bool reversed = false;
                        for (int k = 0; k < toPoly.Count; k++)
                        {
                            int ti = toPoly[k];
                            int tj = toPoly[(k + 1) % toPoly.Count];
                            if ((ti == vj && tj == vi))
                            {
                                reversed = true;
                                break;
                            }
                        }

                        if (reversed)
                            portals.Add((b, a));
                        else
                            portals.Add((a, b));
                        
                        break;
                    }
                }
            }

            Vector2 apex = path[0].pos;
            Vector2 left = portals[0].left;
            Vector2 right = portals[0].right;
            int apexIndex = 0, leftIndex = 0, rightIndex = 0;

            List<PathNode> newPath = new();
            newPath.Add(path[0]);

            for (int i = 1; i < portals.Count; i++)
            {
                Vector2 newLeft = portals[i].left;
                Vector2 newRight = portals[i].right;

                // Tighten the funnel from the right
                if (TriangleArea2(apex, right, newRight) <= 0)
                {
                    if (apex == right || TriangleArea2(apex, left, newRight) > 0)
                    {
                        right = newRight;
                        rightIndex = i;
                    }
                    else
                    {
                        newPath.Add(new PathNode(left));
                        apex = left;
                        apexIndex = leftIndex;
                        left = apex;
                        right = apex;
                        leftIndex = apexIndex;
                        rightIndex = apexIndex;
                        i = apexIndex;
                        continue;
                    }
                }

                // Tighten the funnel from the left
                if (TriangleArea2(apex, left, newLeft) >= 0)
                {
                    if (apex == left || TriangleArea2(apex, right, newLeft) < 0)
                    {
                        left = newLeft;
                        leftIndex = i;
                    }
                    else
                    {
                        newPath.Add(new PathNode(right));
                        apex = right;
                        apexIndex = rightIndex;
                        left = apex;
                        right = apex;
                        leftIndex = apexIndex;
                        rightIndex = apexIndex;
                        i = apexIndex;
                        continue;
                    }
                }
            }

            newPath.Add(path[^1]);
            path.Clear();
            path.AddRange(newPath);
        }

        private float TriangleArea2(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }


        private ConvexPolygon GetPoly(int regionId, int polygonId)
        {
            return regionData[regionId].polygons[polygonId];
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

        void OnDrawGizmos()
        {
            if (!debugEnabled) return;

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
        
            if ((debugTestNearPoint) && (testPoint))
            {
                if ((testRegionId >= 0) && (testRegionId < regionData.Count))
                {
                    if (GetPointOnNavMesh(testPoint.position, testRegionId, out Vector3 navMeshPoint))
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawSphere(testPoint.position, 2.0f);
                        Gizmos.DrawSphere(navMeshPoint, 2.0f);
                        Gizmos.DrawLine(testPoint.position, navMeshPoint);
                    }
                }
                else
                {
                    if (GetPointOnNavMesh(testPoint.position, out int regionId, out Vector3 navMeshPoint))
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawSphere(testPoint.position, 2.0f);
                        Gizmos.DrawSphere(navMeshPoint, 2.0f);
                        Gizmos.DrawLine(testPoint.position, navMeshPoint);
                    }
                }
            }

            if ((debugTestPath) && (startPoint) && (endPoint))
            {
                List<int>       polygons = null;
                int             regionId = testRegionId;
                List<PathNode>  path = null;
                if (PlanPath(startPoint.position, endPoint.position, ref regionId, ref polygons, ref path) != PathState.NoPath)
                if (path != null)
                {
                    Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.75f);
                    foreach (var polygon in polygons)
                    {
                        var poly = GetPoly(testRegionId, polygon);
                        if (poly != null)
                        {
                            DebugHelpers.DrawConvexPolygon(poly.vertices, poly.indices);
                        }
                    }
                    Gizmos.color = Color.blue;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Gizmos.DrawLine(path[i].pos, path[i + 1].pos);
                    }
                }
            }
        }
        #endregion
    }
}
