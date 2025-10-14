using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{
    public class NavMesh2d : MonoBehaviour
    {
        private enum SimplificationAlgorithm { None, GreedyVertexDecimation, RamerDouglasPeucker };
        private enum PathMode { MidEdge };
        public enum PathState { Thinking, NoPath, Partial, Full };

        [SerializeField] 
        private int                        setCellSize;
        [SerializeField] 
        private NavMeshAgentType2d                  agentType;
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
        [SerializeField, Range(0.0f, 1.0f)]
        private float                       funnelBias = 0.0f;
        [SerializeField]
        private NavMeshTerrainType2d        defaultTerrainType;
        [SerializeField, Tooltip("Absolute area tolerance squared-units for deciding if a cost loop equals the region outer boundary.")]
        private float                       boundaryAreaTolerance = 1.0f;
        [SerializeField]
        private bool        debugEnabled;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool        debugGrid;
        [SerializeField, ShowIf(nameof(debugGridEnabled))]
        private bool        colorByRegion;
        [SerializeField, ShowIf(nameof(debugGridEnabled))]
        private bool        colorByCost;
        [SerializeField, ShowIf(nameof(debugGridEnabled))]
        private bool        costLabel;
        [SerializeField, ShowIf(nameof(needCostLabelScale))]
        private float       costLabelScale = 1.0f;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool        debugContours;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool        debugPolygons;
        [SerializeField, ShowIf(nameof(debugPolygonsEnabled))]
        private bool        colorPolygonsByCost;
        [SerializeField, ShowIf(nameof(debugPolygonsEnabled))]
        private bool        polygonCostLabel;
        [SerializeField, ShowIf(nameof(needPolygonCostLabelScale))]
        private float       polygonCostLabelScale = 1.0f;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool        debugTestNearPoint;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool        debugTestPath;
        [SerializeField, ShowIf(nameof(debugEnabled)), Label("Debug Test LoS")]
        private bool        debugTestLoS;
        [SerializeField, ShowIf(nameof(needTestPoint))]
        private Transform   testPoint;
        [SerializeField, ShowIf(nameof(needPoints))]
        private Transform   startPoint;
        [SerializeField, ShowIf(nameof(needPoints))]
        private Transform   endPoint;
        [SerializeField, ShowIf(nameof(needTestRegion))]
        private int         testRegionId;
        [SerializeField, ShowIf(nameof(needTestAgent))]
        private NavMeshAgentType2d  testAgent;

        bool needSimplificationMaxDistance => simplificationAlgorithm == SimplificationAlgorithm.GreedyVertexDecimation;
        bool needTestPoint => debugEnabled && debugTestNearPoint;
        bool needTestRegion => debugEnabled && (debugTestNearPoint || debugTestPath || debugTestLoS);
        bool debugGridEnabled => debugEnabled && debugGrid;
        bool debugPolygonsEnabled => debugEnabled && debugPolygons;
        bool needPoints => debugEnabled && (debugTestPath || debugTestLoS);
        bool needCostLabelScale => debugGridEnabled && costLabel;
        bool needPolygonCostLabelScale => debugPolygonsEnabled && polygonCostLabel;
        bool needTestAgent => debugEnabled && debugTestPath;

        public struct PathNode
        {
            public PathNode(Vector3 p) { pos = p; }

            public Vector3 pos;
        }


        [Serializable]
        class ConvexPolygon
        {
            public int                  id;
            public List<int>            indices;
            public List<Vector3>        vertices;   // Can just refer the master list
            public NavMeshTerrainType2d terrainType;

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
        class Subregion
        {
            public NavMeshTerrainType2d terrainType;
            public Polyline             regionBoundary;
        }

        [Serializable]
        class RegionData
        {
            public byte                 regionId;
            public NavMeshTerrainType2d defaultTerrainType;
            public float                boundaryAreaAbs = 0.0f; 
            public Polyline             boundary;
            public List<Polyline>       holes;
            public List<Subregion>      subregions;
            public List<Vector3>        vertices;
            public List<ConvexPolygon>  polygons;
            public Bounds2d             bounds;
            
            private PolyQuadtree        _quadtree;

            public RegionData()
            {
            }

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


        [SerializeField, HideInInspector] private int           cellSize;
        [SerializeField, HideInInspector] private Vector2Int    gridSize;
        [SerializeField, HideInInspector] private Vector2       gridOffset;

        [SerializeField, HideInInspector]
        byte[]                  grid;
        [SerializeField, HideInInspector]
        byte[]                  region;
        [SerializeField, HideInInspector]
        NavMeshTerrainType2d[]  terrainType;
        [SerializeField, HideInInspector]
        Vector2                 costRange;
        [SerializeField, HideInInspector]
        List<RegionData>        regionData;      

        bool hasValidGrid => (grid != null) && (grid.Length == gridSize.x * gridSize.y);
        bool hasValidRegions => (regionData != null) && (regionData.Count > 0);

        private void Awake()
        {
            if (NavigationMeshes == null) NavigationMeshes = new();
            if (NavigationMeshes.ContainsKey(agentType))
            {
                Debug.LogError($"More than one navigation mesh for agent type {agentType.name}!");
            }
            else
            {
                NavigationMeshes.Add(agentType, this);
            }
        }

        private void OnDestroy()
        {
            NavigationMeshes.Remove(agentType);
        }

        [Button("Clear")]
        public void Clear()
        {
            grid = null;
            region = null;
            terrainType = null;
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
            ComputeCost();
            ExtractContours();
            Simplify();
            Polygonize();
            MergeToConvex();
            ComputeNeighbors();

            if (!debugEnabled)
            {
                grid = null;
                region = null;
                terrainType = null;
                foreach (var rd in regionData)
                {
                    rd.boundary = null;
                    rd.holes = null;
                    rd.subregions = null;
                    var quadtree = rd.quadtree;
                }
            }

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

            var contactFilter = new ContactFilter2D();
            contactFilter.layerMask = obstacleMask;
            var results = new Collider2D[32];

            int index = 0;
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
                        regionId = currentRegion,
                        defaultTerrainType = defaultTerrainType,
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

        #region Cost
        void ComputeCost()
        {
            terrainType = new NavMeshTerrainType2d[gridSize.x * gridSize.y];
            costRange = Vector2.one;

            var modifiers = new List<NavMeshModifier2d>(FindObjectsByType<NavMeshModifier2d>(FindObjectsSortMode.None));
            modifiers.Sort((m1, m2) => m2.priority.CompareTo(m1.priority));

            int index = 0;            
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    NavMeshTerrainType2d    tt = defaultTerrainType;
                    Vector2                 boxCenterPos = GridToWorldCenter(x, y);

                    foreach (var modifier in modifiers)
                    {
                        if (!modifier.enabled) continue;
                        if (modifier.InfluenceTerrainType(boxCenterPos, ref tt))
                        {
                            if (tt.defaultCost < costRange.x) costRange.x = tt.defaultCost;
                            if (tt.defaultCost > costRange.y) costRange.y = tt.defaultCost;
                            break;
                        }
                    }

                    this.terrainType[index] = tt;
                    index++;
                }
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

                    // ensure CCW
                    if (areas[boundaryLoop] < 0) outer.ReverseOrder();
                    
                    regionData[r].boundary = outer;

                    // NEW: cache absolute area of outer boundary for this region
                    regionData[r].boundaryAreaAbs = Mathf.Abs(areas[boundaryLoop]);

                    for (int i = 0; i < plines.Count; i++)
                    {
                        if (i == boundaryLoop) continue;
                        var hole = plines[i];

                        // ensure CW
                        if (areas[i] > 0) hole.ReverseOrder();

                        regionData[r].holes.Add(hole);
                    }
                }
            }

            // Find all cost-based subregions per region
            // This this code and the previous could be folded into a similar codepath, I believe there is
            // a lot of redundancy here
            for (int r = 0; r < regionData.Count; r++)
            {
                if (regionData[r].boundary == null) continue;
                if (regionData[r].subregions == null) regionData[r].subregions = new List<Subregion>();

                BuildCostSubregionsForRegion(regionData[r]);
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

        void BuildCostSubregionsForRegion(RegionData rd)
        {
            int w = gridSize.x, h = gridSize.y;
            byte rid = rd.regionId;

            // Component labels for this region (-1 = not visited / not in region)
            int[] comp = new int[w * h];
            for (int i = 0; i < comp.Length; i++) comp[i] = -1;

            int currentComp = 0;
            var subregions = rd.subregions;
            subregions.Clear();

            float outerAreaAbs = rd.boundaryAreaAbs;
            float bestOuterAreaDiff = float.MaxValue;
            NavMeshTerrainType2d chosenOuterTerrain = rd.defaultTerrainType;

            // BFS over 4-neighborhood
            var queue = new Queue<Cell>();
            var neighbors = new (int dx, int dy)[] { (-1, 0), (1, 0), (0, -1), (0, 1) };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int startIdx = y * w + x;
                    if (region[startIdx] != rid) continue;
                    if (comp[startIdx] != -1) continue;

                    // Terrain type to match for this component
                    NavMeshTerrainType2d tt = terrainType[startIdx];
                    if (tt == null) continue;

                    // BFS this terrain-typed component
                    var cells = new HashSet<int>();
                    comp[startIdx] = currentComp;
                    queue.Clear();
                    queue.Enqueue(new Cell(x, y, startIdx));
                    cells.Add(startIdx);

                    while (queue.Count > 0)
                    {
                        var c = queue.Dequeue();
                        for (int k = 0; k < neighbors.Length; k++)
                        {
                            int nx = c.x + neighbors[k].dx;
                            int ny = c.y + neighbors[k].dy;
                            if (!InBounds(nx, ny)) continue;

                            int nIdx = ny * w + nx;
                            if (region[nIdx] != rid) continue;
                            if (comp[nIdx] != -1) continue;
                            if (!ReferenceEquals(terrainType[nIdx], tt)) continue;

                            comp[nIdx] = currentComp;
                            queue.Enqueue(new Cell(nx, ny, nIdx));
                            cells.Add(nIdx);
                        }
                    }

                    // Build the contour for this component
                    var edgeMap = new Dictionary<Vector2, List<Vector2>>();
                    BuildComponentEdges(cells, edgeMap, rid);

                    float areaAbs;
                    Polyline pl = PolylineFromComponentLoops(edgeMap, out areaAbs);
                    if (pl == null)
                    {
                        currentComp++;
                        continue;
                    }

                    // Decide whether this component is the region's "outer" area
                    float areaDiff = Mathf.Abs(areaAbs - outerAreaAbs);
                    if (areaDiff <= boundaryAreaTolerance)
                    {
                        if (areaDiff < bestOuterAreaDiff)
                        {
                            bestOuterAreaDiff = areaDiff;
                            chosenOuterTerrain = tt;
                        }
                    }
                    else
                    {
                        // Store as subregion for this terrain type
                        subregions.Add(new Subregion
                        {
                            terrainType = tt,
                            regionBoundary = pl
                        });
                    }

                    currentComp++;
                }
            }

            // Finalize region outer terrain
            if (bestOuterAreaDiff < float.MaxValue)
                rd.defaultTerrainType = chosenOuterTerrain;
        }

        struct Cell 
        {
            public Cell(int X, int Y, int I) { x = X; y = Y; idx = I; }

            public int x, y, idx;  
        }

        bool InBounds(int x, int y) => ((x >= 0) && (x < gridSize.x) && (y >= 0) && (y < gridSize.y));

        // Build edge contour for a set of cells that belong to a component
        void BuildComponentEdges(HashSet<int> component, Dictionary<Vector2, List<Vector2>> edgeMap, byte rid)
        {
            int w = gridSize.x, h = gridSize.y;
            Vector2 worldMin = gridOffset - Vector2.one * (cellSize * 0.5f);

            foreach (var idx in component)
            {
                int y = idx / w;
                int x = idx - y * w;

                Vector2 bl = worldMin + new Vector2(x * cellSize, y * cellSize);
                Vector2 br = bl + new Vector2(cellSize, 0);
                Vector2 tl = bl + new Vector2(0, cellSize);
                Vector2 tr = bl + new Vector2(cellSize, cellSize);

                // Edge belongs to contour if neighbour is not inside the same component
                // left
                if (!InBounds(x - 1, y) || !component.Contains(idx - 1))
                    AddEdge(edgeMap, bl, tl);
                // right
                if (!InBounds(x + 1, y) || !component.Contains(idx + 1))
                    AddEdge(edgeMap, tr, br);
                // bottom
                if (!InBounds(x, y - 1) || !component.Contains(idx - w))
                    AddEdge(edgeMap, br, bl);
                // top
                if (!InBounds(x, y + 1) || !component.Contains(idx + w))
                    AddEdge(edgeMap, tl, tr);
            }
        }

        // Build a Polyline from the largest loop in edgeMap; returns area (abs)
        Polyline PolylineFromComponentLoops(Dictionary<Vector2, List<Vector2>> edgeMap, out float areaAbs)
        {
            var loops = BuildLoops(edgeMap);
            areaAbs = 0f;
            int best = -1;

            // pick largest by |area|
            for (int i = 0; i < loops.Count; i++)
            {
                float a = Mathf.Abs(SignedArea(loops[i]));
                if (a > areaAbs) { areaAbs = a; best = i; }
            }

            if (best < 0) return null;

            var pts = loops[best];
            var pl = new Polyline();
            foreach (var v in pts) pl.Add(new Vector3(v.x, v.y, 0f));
            pl.Add(pts[0]);
            pl.isClosed = true;

            // Ensure CCW for "outer" style; holes will not matter here
            if (SignedArea(pts) < 0f) pl.ReverseOrder();
            return pl;
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
                        if (region.subregions != null)
                        {
                            foreach (var subregion in region.subregions)
                            {
                                subregion.regionBoundary.Simplify(simplificationMaxDistance);
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
                        if (region.subregions != null)
                        {
                            foreach (var subregion in region.subregions)
                            {
                                subregion.regionBoundary = subregion.regionBoundary.SimplifyRDP(epsilon, true);
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
                if (region.subregions != null)
                {
                    foreach (var subregion in region.subregions)
                    {
                        subregion.regionBoundary.RemoveDuplicates();
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

                #region Helpers
                // Helper function to find a vertex or add one if needed
                int FindOrAdd(Vector3 p, float epsilon = 1e-3f)
                {
                    for (int i = 0; i < rd.vertices.Count; i++)
                    {
                        if (Vector3.SqrMagnitude(rd.vertices[i] - p) < epsilon) return i;
                    }

                    rd.vertices.Add(p);
                    return rd.vertices.Count - 1;
                }

                List<Polyline> GetHoles(Subregion excludeSubregion)
                {
                    List<Polyline> holes = new(rd.holes);
                    if (rd.subregions != null)
                    {
                        foreach (var subregion in rd.subregions)
                        {
                            if (subregion == excludeSubregion) continue;
                            var tmpHole = new Polyline(subregion.regionBoundary);
                            tmpHole.ReverseOrder();
                            holes.Add(tmpHole);
                        }
                    }
                    return holes;
                }
                #endregion

                // Skip empty regions
                if (rd.boundary == null) continue;

                List<Polyline>  holes = GetHoles(null);
                List<int>       triangles = null;
                rd.boundary.Triangulate_EarCut(holes, ref rd.vertices, ref triangles);

                ConstrainedDelaunayFlipper.EnforceDelaunay(rd.vertices, triangles, rd.boundary, holes, 500, 1.0f);

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    var poly = new List<int>() { triangles[i], triangles[i + 1], triangles[i + 2] };
                    var convexPolygon = new ConvexPolygon { id = rd.polygons.Count, indices = poly, vertices = rd.vertices, terrainType = rd.defaultTerrainType };

                    convexPolygon.UpdateGeometry();
                    rd.polygons.Add(convexPolygon);
                }

                if (rd.subregions != null)
                {
                    // Create the polygonal regions corresponding to the cost regions
                    foreach (var subregion in rd.subregions)
                    {
                        List<Vector3>   polyVertexList = null;
                        List<int>       polyTriangleList = null;
                        List<Polyline>  subHoles = GetHoles(subregion);

                        subregion.regionBoundary.Triangulate_EarCut(subHoles, ref polyVertexList, ref polyTriangleList);

                        ConstrainedDelaunayFlipper.EnforceDelaunay(polyVertexList, polyTriangleList, subregion.regionBoundary, subHoles, 500, 1.0f);

                        for (int i = 0; i < polyTriangleList.Count; i += 3)
                        {
                            var poly = new List<int>()
                            {
                                FindOrAdd(polyVertexList[polyTriangleList[i]]),
                                FindOrAdd(polyVertexList[polyTriangleList[i + 1]]),
                                FindOrAdd(polyVertexList[polyTriangleList[i + 2]]),
                            };

                            var convexPolygon = new ConvexPolygon { id = rd.polygons.Count, indices = poly, vertices = rd.vertices, terrainType = subregion.terrainType };
                            convexPolygon.UpdateGeometry();
                            rd.polygons.Add(convexPolygon);
                        }
                    }
                }
            }
        }

        #endregion

        #region Merge to convex

        void MergeToConvex()
        {
            // Have to have the same cost to merge
            bool SameCostOnly(List<ConvexPolygon> parentsA, List<ConvexPolygon> parentsB)
            {
                return parentsA[0].terrainType == parentsB[0].terrainType;
            }

            // Need some marshaling, but that's life
            foreach (var rd in regionData)
            {    
                List<List<int>>             polygons = new();
                List<List<ConvexPolygon>>   parents = new();
                foreach (var polygon in rd.polygons) polygons.Add(new(polygon.indices));

                HertelMehlhornPolygonMerger.MergeExt(rd.vertices, polygons, rd.polygons, ref polygons, ref parents, SameCostOnly);

                rd.polygons.Clear();

                for (int i = 0; i < polygons.Count; i++)                    
                {
                    var convexPolygon = new ConvexPolygon { id = rd.polygons.Count, indices = polygons[i], vertices = rd.vertices, terrainType = parents[i][0].terrainType };
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
            return GetPointOnNavMesh(point, regionId, out int polygonId, out pt);
        }

        public bool GetPointOnNavMesh(Vector3 point, out int regionId, out Vector3 pt)
        {
            return GetPointOnNavMesh(point, out regionId, out int polygonId, out pt);
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


        public PathState PlanPath(Vector3 start, Vector3 end, ref int regionId, ref List<int> polygons, ref List<PathNode> path, float pathMidBias = -float.MaxValue, NavMeshAgentType2d agentType = null)
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

            return PlanPathOnNavmesh(startOnNavmesh, startPolygonId, endOnNavmesh, endPolygonId, regionId, ref polygons, ref path, pathMidBias, agentType);
        }

        public PathState PlanPathOnNavmesh(Vector3 start, int startPolygonId, Vector3 end, int endPolygonId, int regionId, ref List<int> polygons, ref List<PathNode> path, float pathMidBias = -float.MaxValue, NavMeshAgentType2d agentType = null)
        {
            PathState ret = PathState.NoPath;

            switch (pathMode)
            {
                case PathMode.MidEdge:
                    ret = PlanPathOnNavmeshMidEdge(start, startPolygonId, end, endPolygonId, regionId, ref polygons, ref path, agentType);
                    break;
                default:
                    break;
            }

            if ((funnelEnable) && (path.Count > 2))
            {
                Funnel(polygons, path, regionId, (pathMidBias < 0) ? (funnelBias) : (pathMidBias));
            }

            return ret;
        }

        private float GetCost(NavMeshAgentType2d agent, NavMeshTerrainType2d terrainType)
        {
            if (agent == null)
            {
                return agentType.GetCost(terrainType);
            }

            return agent.GetCost(terrainType);
        }

        private PathState PlanPathOnNavmeshMidEdge(Vector3 start, int startPolygonId, Vector3 end, int endPolygonId, int regionId, ref List<int> polygons, ref List<PathNode> path, NavMeshAgentType2d agentType = null)
        {
            if (polygons == null) polygons = new();

            var region = regionData[regionId];
            var polys = region.polygons;
            var frontier = new PriorityQueue<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var costSoFar = new Dictionary<int, float>();
            var entryPoint = new Dictionary<int, Vector2>();

            Vector2 endPos = end;

            frontier.Enqueue(startPolygonId, 0);
            costSoFar[startPolygonId] = 0;
            entryPoint[startPolygonId] = (Vector2)start;

            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();
                if (current == endPolygonId)
                    break;

                Vector2 fromPos = entryPoint[current];

                var currentPoly = polys[current];
                var vertices = region.vertices;

                for (int i = 0; i < currentPoly.neighbors.Count; i++)
                {
                    int neighbor = currentPoly.neighbors[i];
                    if (neighbor == -1) continue;

                    int vi = currentPoly[i];
                    int vj = currentPoly[(i + 1) % currentPoly.Count];
                    Vector2 edgeMid = 0.5f * ((Vector2)vertices[vi] + (Vector2)vertices[vj]);

                    // incremental: from where we entered ‘current’ to this portal
                    float stepCost = Vector2.Distance(fromPos, edgeMid) * Mathf.Max(1e-5f, GetCost(agentType, currentPoly.terrainType));
                    float newCost = costSoFar[current] + stepCost;

                    float minUnitCost = Mathf.Max(1e-5f, costRange.x);
                    float priority = newCost + Vector2.Distance(edgeMid, endPos) * minUnitCost;

                    if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                    {
                        costSoFar[neighbor] = newCost;
                        frontier.Enqueue(neighbor, priority);
                        cameFrom[neighbor] = current;
                        entryPoint[neighbor] = edgeMid;
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

        private void Funnel(List<int> polygons, List<PathNode> path, int regionId, float bias)
        {
            // trivial cases
            if (polygons == null || path == null || polygons.Count < 2 || path.Count < 2)
                return;

            var region = regionData[regionId];
            var verts = region.vertices;     // Vector3[] or Vector2[]
            var polyList = region.polygons;     // List<Polygon> (each has .indices and .neighbors)

            // 1) Build the portal list: one portal per shared edge
            var lefts = new List<Vector2>();
            var rights = new List<Vector2>();

            for (int i = 1; i < polygons.Count; i++)
            {
                var prev = polyList[polygons[i - 1]];
                int currId = polygons[i];

                // find which edge of prev links to curr
                for (int e = 0; e < prev.neighbors.Count; e++)
                {
                    if (prev.neighbors[e] != currId)
                        continue;

                    int vi = prev.indices[e];
                    int vj = prev.indices[(e + 1) % prev.indices.Count];
                    Vector2 p0 = (Vector2)verts[vi];
                    Vector2 p1 = (Vector2)verts[vj];

                    // orient portal endpoints by local travel direction
                    Vector2 travelDir = (path[i].pos - path[i - 1].pos).normalized;
                    Vector2 edgeVec = p1 - p0;
                    float side = Vector3.Cross(travelDir, edgeVec).z;

                    Vector2 rawLeft, rawRight;
                    if (side >= 0f)
                    {
                        // p1 is to the left of travelDir
                        rawLeft = p0;
                        rawRight = p1;
                    }
                    else
                    {
                        rawLeft = p1;
                        rawRight = p0;
                    }

                    // now pull both ends toward the true midpoint by t
                    Vector2 mid = 0.5f * (p0 + p1);
                    Vector2 adjL = Vector2.Lerp(rawLeft, mid, bias);
                    Vector2 adjR = Vector2.Lerp(rawRight, mid, bias);

                    lefts.Add(adjL);
                    rights.Add(adjR);

                    break;
                }
            }

            // 1b) append a zero-width portal at the goal
            Vector3 goal = path[path.Count - 1].pos;
            lefts.Add(goal);
            rights.Add(goal);

            // 2) Funnel-tightening
            var newPath = new List<PathNode> { path[0] };

            Vector2 apex = path[0].pos;
            Vector2 left = apex;
            Vector2 right = apex;
            int apexIndex = 0, leftIndex = 0, rightIndex = 0;

            for (int i = 0; i < lefts.Count; i++)
            {
                Vector2 newLeft = lefts[i];
                Vector2 newRight = rights[i];

                // --- update right boundary ---
                if (Triangle.SignedArea2(apex, right, newRight) <= 0f)
                {
                    if ((apex == right) || (Triangle.SignedArea2(apex, left, newRight) > 0f))
                    {
                        right = newRight;
                        rightIndex = i;
                    }
                    else
                    {
                        // emit left apex, restart
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

                // --- update left boundary ---
                if (Triangle.SignedArea2(apex, left, newLeft) >= 0f)
                {
                    if ((apex == left) || (Triangle.SignedArea2(apex, right, newLeft) < 0f))
                    {
                        left = newLeft;
                        leftIndex = i;
                    }
                    else
                    {
                        // emit right apex, restart
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

            // 3) ensure the real goal is the last waypoint
            if (newPath[newPath.Count - 1].pos != goal)
                newPath.Add(new PathNode(goal));

            // overwrite original path
            path.Clear();
            path.AddRange(newPath);
        }


        private ConvexPolygon GetPoly(int regionId, int polygonId)
        {
            return regionData[regionId].polygons[polygonId];
        }

        public int GetRegion(Vector3 position)
        {
            if (GetPointOnNavMesh(position, out var regionId, out var pt))
            {
                return regionId;
            }

            return -1;
        }

        public bool RaycastVector(Vector3 start, Vector3 dir, float maxDist, int regionId, out Vector3 endPoint, out int polygonId)
        {
            // 1) Normalize and find the starting polygon
            Vector3 direction = dir.normalized;
            if (!GetPointOnNavMesh(start, regionId, out polygonId, out Vector3 currentPoint))
            {
                endPoint = start;
                return false;
            }

            // 2) Prepare traversal state
            var rd = regionData[regionId];
            float traveled = 0f;
            float remaining = maxDist;
            int currentPoly = polygonId;
            const float epsilon = 1e-4f;
            int maxIters = rd.polygons.Count * 2;
            int iter = 0;

            // 3) Loop: in each polygon, find the nearest edge-intersection
            while (iter++ < maxIters)
            {
                var poly = rd.polygons[currentPoly];
                float closestT = float.MaxValue;
                int bestNeighbor = -1;
                Vector3 hitPos = Vector3.zero;

                // 3a) test each edge
                for (int i = 0; i < poly.neighbors.Count; i++)
                {
                    int neighbor = poly.neighbors[i];
                    Vector2 v1 = (Vector2)rd.vertices[poly.indices[i]];
                    Vector2 v2 = (Vector2)rd.vertices[poly.indices[(i + 1) % poly.indices.Count]];

                    // solve ray–segment intersection in 2D:
                    Vector2 p = new Vector2(currentPoint.x, currentPoint.y);
                    Vector2 r = new Vector2(direction.x, direction.y);
                    Vector2 q = v1;
                    Vector2 s = v2 - v1;

                    float rxs = r.x * s.y - r.y * s.x;
                    if (Mathf.Abs(rxs) < Mathf.Epsilon)
                        continue; // parallel

                    Vector2 qp = q - p;
                    float t = (qp.x * s.y - qp.y * s.x) / rxs;
                    float u = (qp.x * r.y - qp.y * r.x) / rxs;

                    if (t > epsilon && t <= remaining + epsilon && u >= -epsilon && u <= 1f + epsilon)
                    {
                        if (t < closestT)
                        {
                            closestT = t;
                            bestNeighbor = neighbor;
                            hitPos = currentPoint + direction * t;
                        }
                    }
                }

                // 3b) no edge hit -> stays in this poly for full distance
                if (closestT == float.MaxValue)
                {
                    endPoint = start + direction * maxDist;
                    polygonId = currentPoly;
                    return false;
                }

                // 3c) the intersection lies beyond maxDist
                if (closestT > remaining)
                {
                    endPoint = start + direction * maxDist;
                    polygonId = currentPoly;
                    return false;
                }

                // 3d) hit a boundary edge?
                if (bestNeighbor == -1)
                {
                    endPoint = hitPos;
                    polygonId = currentPoly;
                    return true;
                }

                // 3e) cross into neighbor polygon
                traveled += closestT + epsilon;
                remaining = maxDist - traveled;
                currentPoint = hitPos + direction * epsilon;
                currentPoly = bestNeighbor;
            }

            // 4) fallback if something went wrong
            endPoint = start + direction * maxDist;
            polygonId = currentPoly;
            return false;
        }

        public bool RaycastSegment(Vector3 start, Vector3 end, int regionId, out Vector3 endPoint, out int polygonId)
        {
            return RaycastVector(start, (end - start).normalized, (end - start).magnitude, regionId, out endPoint, out polygonId);
        }



        #endregion

        #region Debug and Gizmos

#if UNITY_EDITOR
        static Color[] regionColors =
        {
            new Color(0.0f, 1.0f, 1.0f, 0.25f),
            new Color(0.0f, 1.0f, 0.0f, 0.25f),
            new Color(1.0f, 1.0f, 0.0f, 0.25f),
            new Color(1.0f, 0.0f, 1.0f, 0.25f),
            new Color(0.0f, 0.0f, 1.0f, 0.25f),
            new Color(1.0f, 1.0f, 1.0f, 0.25f)
        };

        Color GetColorByCost(float cost, Color baseColor)
        {
            var normalizedCost = Mathf.InverseLerp(costRange.x, costRange.y, cost);
            if (costRange.x == costRange.y) normalizedCost = 1.0f;
            var c = baseColor;
            c = Color.Lerp(c * 0.25f, c, normalizedCost);
            c.a = baseColor.a;
            
            return c;
        }

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
                        bool    wall = false;
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
                                case 1: Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.25f); wall = true; break;
                                case 2: Gizmos.color = new Color(1.0f, 1.0f, 0.0f, 0.25f); wall = true; break;
                            }
                        }

                        var cost = GetCost(agentType, terrainType[index]);

                        if ((colorByCost) && (terrainType != null) && (terrainType.Length == grid.Length) && (!wall))
                        {
                            Gizmos.color = GetColorByCost(cost, Gizmos.color);
                        }
                        if (draw) Gizmos.DrawCube(boxCenterPos, Vector2.one * cellSize);
                        if (costLabel)
                        {
                            if (cost != 1.0f)
                            {
                                DebugHelpers.DrawTextAt(boxCenterPos, Vector3.zero, (int)(cellSize * costLabelScale * 2.0f), Color.grey, $"{cost}", true, true);
                            }
                        }

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
                    if (region.subregions != null)
                    {
                        foreach (var subregion in region.subregions)
                        {
                            Gizmos.color = regionColors[i % regionColors.Length].ChangeAlpha(1.0f);
                            for (int j = 0; j < subregion.regionBoundary.Count; j++)
                            {
                                Handles.color = regionColors[i % regionColors.Length].ChangeAlpha(1.0f);
                                Handles.DrawDottedLine(subregion.regionBoundary[j], subregion.regionBoundary[(j + 1) % subregion.regionBoundary.Count], 1.0f);
                            }
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

                            var cost = GetCost(agentType, poly.terrainType);

                            if (colorPolygonsByCost)
                            {
                                Gizmos.color = GetColorByCost(cost, regionColors[i % regionColors.Length]);
                            }

                            DebugHelpers.DrawWireConvexPolygon(vertices);
                            DebugHelpers.DrawConvexPolygon(vertices);

                            if (polygonCostLabel)
                            {
                                if (cost != 1.0f)
                                {
                                    DebugHelpers.DrawTextAt(poly.center, Vector3.zero, (int)(cellSize * polygonCostLabelScale * 4.0f), Color.grey, $"{cost}", true, true);
                                }
                            }
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
                var             agent = (testAgent != null) ? (testAgent) : (agentType);
                if (PlanPath(startPoint.position, endPoint.position, ref regionId, ref polygons, ref path, agentType : agent) != PathState.NoPath)
                {
                    if (debugPolygons)
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
                    }
                    Gizmos.color = Color.blue;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Gizmos.DrawLine(path[i].pos, path[i + 1].pos);
                    }
                }
            }

            if ((debugTestLoS) && (startPoint))
            {
                if (RaycastSegment(startPoint.position, endPoint.position, testRegionId, out Vector3 actualEndPoint, out int polygonId))
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.green;

                Gizmos.DrawLine(startPoint.position, actualEndPoint);
                Handles.DrawDottedLine(actualEndPoint, endPoint.position, 2);

            }
        }
        #endif

        #endregion

        #region NavMesh2d management
        static Dictionary<NavMeshAgentType2d, NavMesh2d> NavigationMeshes;

        public static NavMesh2d Get(NavMeshAgentType2d agentType)
        {
            if ((NavigationMeshes != null) && (NavigationMeshes.TryGetValue(agentType, out var navMesh)))
            {
                return navMesh;
            }

            if (agentType.parentType != null)
            {
                return Get(agentType.parentType);
            }

#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                var navMeshes = FindObjectsByType<NavMesh2d>(FindObjectsSortMode.None);
                foreach (var nm in navMeshes)
                {
                    if (nm.agentType == agentType) return nm;
                }
                if (agentType.parentType != null)
                {
                    return Get(agentType.parentType);
                }
            }
#endif

            return null;
        }
        #endregion
    }
}
