using NaughtyAttributes;
using System;
using System.Collections;
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
        [SerializeField, Tooltip("Per-frame time budget (ms) for runtime rebuilds; the bake is time-sliced across frames to stay under it. 0 or less bakes synchronously in a single frame. The editor 'Bake NavMesh' button is always synchronous.")]
        private float                       maxBakeMillisecondsPerFrame = 4.0f;
        bool needSimplificationMaxDistance => simplificationAlgorithm == SimplificationAlgorithm.GreedyVertexDecimation;

        public struct PathNode
        {
            public PathNode(Vector3 p) { pos = p; link = null; manualTraversal = false; }
            public PathNode(Vector3 p, NavMeshLink2d link, bool manualTraversal) { pos = p; this.link = link; this.manualTraversal = manualTraversal; }

            public Vector3          pos;
            // When this waypoint sits at the near side of a NavMeshLink2d crossing, 'link' is that
            // link. 'manualTraversal' is true for non-auto-traverse links, signalling the agent to
            // hand the crossing to an INavMeshLinkTraversal instead of walking across.
            public NavMeshLink2d    link;
            public bool             manualTraversal;
        }


        [Serializable]
        internal class ConvexPolygon
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

        internal class PolyQuadtree : Quadtree<ConvexPolygon>
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
        internal class Subregion
        {
            public NavMeshTerrainType2d terrainType;
            public Polyline             regionBoundary;
        }

        [Serializable]
        internal class RegionData
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

        // Obstacles incorporated by the last bake (serialized so scene-placed obstacles aren't seen as
        // "new" on load and don't each trigger a redundant rebuild).
        [SerializeField, HideInInspector]
        List<NavMeshObstacle2d> bakedObstacles = new();

        // Transient set of obstacle colliders used while baking; cleared afterwards.
        [NonSerialized] HashSet<Collider2D>     _obstacleColliders;
        [NonSerialized] bool                    _rebuildRequested;
        // True while a (possibly time-sliced) bake is in progress. The pipeline builds fresh objects
        // into the working fields above; queries instead read the snapshot below so they keep serving
        // the last completed bake until the new one is published (on completion _isBaking clears).
        [NonSerialized] bool                    _isBaking;
        [NonSerialized] List<RegionData>        _snapRegionData;
        [NonSerialized] byte[]                  _snapGrid;
        [NonSerialized] byte[]                  _snapRegion;
        [NonSerialized] NavMeshTerrainType2d[]  _snapTerrain;
        [NonSerialized] Vector2                 _snapCostRange;
        [NonSerialized] int                     _snapCellSize;
        [NonSerialized] Vector2Int              _snapGridSize;
        [NonSerialized] Vector2                 _snapGridOffset;

        // Query/debug views: the snapshot while baking, the working fields otherwise.
        List<RegionData>        QRegionData => _isBaking ? _snapRegionData : regionData;
        byte[]                  QGrid       => _isBaking ? _snapGrid : grid;
        byte[]                  QRegion     => _isBaking ? _snapRegion : region;
        NavMeshTerrainType2d[]  QTerrain    => _isBaking ? _snapTerrain : terrainType;
        Vector2                 QCostRange  => _isBaking ? _snapCostRange : costRange;
        int                     QCellSize   => _isBaking ? _snapCellSize : cellSize;
        Vector2Int              QGridSize   => _isBaking ? _snapGridSize : gridSize;
        Vector2                 QGridOffset => _isBaking ? _snapGridOffset : gridOffset;

        /// <summary>True while a (possibly time-sliced) rebuild is in progress. Queries keep working
        /// against the previous mesh meanwhile; poll this to wait for the freshest result.</summary>
        public bool IsRebuilding => _isBaking;

        // Active navmesh instances (runtime), so obstacles can notify the ones they affect.
        static readonly HashSet<NavMesh2d>      s_NavMeshes = new();

        bool hasValidGrid => (QGrid != null) && (QGrid.Length == QGridSize.x * QGridSize.y);
        bool hasValidRegions => (QRegionData != null) && (QRegionData.Count > 0);

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
            s_NavMeshes.Add(this);
        }

        private void OnDestroy()
        {
            NavigationMeshes.Remove(agentType);
            s_NavMeshes.Remove(this);
        }

        private void Start()
        {
            // Scene-placed obstacles may send their OnEnable notification before this navmesh registers
            // in s_NavMeshes (component init order is undefined), so that push can be lost. Reconcile
            // here (Start runs after every Awake/OnEnable): if the live obstacle set differs from what we
            // baked with, request a rebuild.
            if (!ObstaclesMatchBake())
                RequestRebuild();
        }

        private void LateUpdate()
        {
            // A runtime obstacle change requested a rebuild; do it once here, coalescing same-frame
            // requests. Never start a new bake while one is already running (the request stays pending).
            if (_rebuildRequested && !_isBaking)
            {
                _rebuildRequested = false;
                if (maxBakeMillisecondsPerFrame > 0.0f)
                    StartCoroutine(BakePipeline(new BakeBudget(maxBakeMillisecondsPerFrame)));
                else
                    Bake();
            }
        }

        [Button("Clear")]
        public void Clear()
        {
            grid = null;
            region = null;
            terrainType = null;
            regionData = null;
            _linkAdjacency = null;
            bakedObstacles?.Clear();
        }

        [Button("Bake NavMesh")]
        public void Bake()
        {
            // Synchronous bake: drain the pipeline in one call (editor button / direct calls). The
            // budget is effectively infinite, so the pipeline never yields.
            RunToEnd(BakePipeline(new BakeBudget(float.MaxValue)));
        }

        // The full bake pipeline. Run as a coroutine with a finite per-frame budget it time-slices
        // across frames (the heavy per-cell stages yield by the budget; geometry stages yield between
        // each). Run via Bake()/RunToEnd it completes synchronously.
        IEnumerator BakePipeline(BakeBudget budget)
        {
            // Capture the current (last completed) state so queries keep working against it while the
            // pipeline rebuilds the working fields. The pipeline only allocates fresh objects, so these
            // references stay valid throughout the bake.
            SnapshotForQueries();
            _isBaking = true;
            try
            {
                Clear();
                cellSize = setCellSize;

                // Gather the NavMeshObstacle2d colliders to bake in as impassable geometry.
                bakedObstacles = GatherObstacles();
                BuildObstacleColliderSet();

                yield return CreateGridMapRoutine(budget);
                if (agentType != null) yield return GrowMapRoutine(budget);
                ComputeRegions();   if (budget.Step()) yield return null;
                yield return ComputeCostRoutine(budget);
                ExtractContours();  if (budget.Step()) yield return null;
                Simplify();         if (budget.Step()) yield return null;
                Polygonize();       if (budget.Step()) yield return null;
                MergeToConvex();    if (budget.Step()) yield return null;
                ComputeNeighbors();

                FinalizeBake();
            }
            finally
            {
                _isBaking = false;   // publish: queries now read the freshly built working fields
                _obstacleColliders = null;
                _snapRegionData = null;
                _snapGrid = null;
                _snapRegion = null;
                _snapTerrain = null;
            }
        }

        void SnapshotForQueries()
        {
            _snapRegionData = regionData;
            _snapGrid = grid;
            _snapRegion = region;
            _snapTerrain = terrainType;
            _snapCostRange = costRange;
            _snapCellSize = cellSize;
            _snapGridSize = gridSize;
            _snapGridOffset = gridOffset;
        }

        void FinalizeBake()
        {
            // Link adjacency may have been (re)built against the snapshot during this bake; drop it so
            // the next query rebuilds it against the freshly baked regions.
            _linkAdjacency = null;

            // Keep the grid/region/contour data only if a NavMeshDebug2d companion wants to visualize it.
            var debug = GetComponent<NavMeshDebug2d>();
            if (debug == null || !debug.DebugEnabled)
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

            if (!Application.isPlaying)
            {
                // If you want the scene to show unsaved changes:
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        // Drains an iterator (and any nested iterators it 'yield return's) to completion, ignoring frame
        // yields. Lets the same pipeline run either synchronously or time-sliced as a coroutine.
        static void RunToEnd(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (top.MoveNext())
                {
                    if (top.Current is IEnumerator inner) stack.Push(inner);
                }
                else
                {
                    stack.Pop();
                }
            }
        }

        // Per-frame time budget for a time-sliced bake. Step() returns true (and restarts the clock)
        // once the budget for the current frame has been spent.
        class BakeBudget
        {
            readonly float                          budgetMs;
            readonly System.Diagnostics.Stopwatch   sw;

            public BakeBudget(float ms) { budgetMs = ms; sw = System.Diagnostics.Stopwatch.StartNew(); }

            public bool Step()
            {
                if (sw.Elapsed.TotalMilliseconds < budgetMs) return false;
                sw.Restart();
                return true;
            }
        }

        #region CreateGridMap
        IEnumerator CreateGridMapRoutine(BakeBudget budget)
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
                    // NavMeshObstacle2d colliders block the cell regardless of layer or static flag.
                    if (grid[index] == 0 && _obstacleColliders != null)
                    {
                        foreach (var c in _obstacleColliders)
                        {
                            if (c != null && c.OverlapPoint(boxCenterPos))
                            {
                                grid[index] = 1;
                                break;
                            }
                        }
                    }
                    index++;
                }
                if (budget.Step()) yield return null;   // time-slice between rows
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

            // Obstacle colliders also define the baked area, regardless of layer / static flag.
            if (_obstacleColliders != null)
            {
                foreach (var c in _obstacleColliders)
                {
                    if (c == null) continue;
                    if (!hasBounds)
                    {
                        bounds = c.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(c.bounds);
                    }
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
        IEnumerator GrowMapRoutine(BakeBudget budget)
        {
            int radiusInCells = Mathf.CeilToInt(agentType.agentRadius / cellSize);
            if (radiusInCells <= 0) yield break;

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
                if (budget.Step()) yield return null;   // time-slice between rows
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
        IEnumerator ComputeCostRoutine(BakeBudget budget)
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
                if (budget.Step()) yield return null;   // time-slice between rows
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
            var regions = QRegionData;
            if (regions == null || regionId < 0 || regionId >= regions.Count)
            {
                polygonId = -1; pt = point; return false;
            }

            var rd = regions[regionId];

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

            var regions = QRegionData;
            if (regions == null)
            {
                pt = point;
                regionId = -1;
                polygonId = -1;
                return false;
            }

            foreach (var rd in regions)
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


        public PathState PlanPath(Vector3 start, Vector3 end, ref int regionId, ref List<int> polygons, ref List<PathNode> path, float pathMidBias = -float.MaxValue, NavMeshAgentType2d agentType = null, NavMeshAgent2d agent = null, bool allowPartial = true, List<int> polygonRegions = null)
        {
            var startOnNavmesh = start;
            var endOnNavmesh = end;
            int startPolygonId = -1;
            int endPolygonId = -1;
            int startRegionId;
            int endRegionId;

            if ((regionId >= 0) && (regionId < QRegionData.Count))
            {
                if (!GetPointOnNavMesh(start, regionId, out startPolygonId, out startOnNavmesh))
                {
                    Debug.LogWarning("Can't find start point on navmesh!");
                    return PathState.NoPath;
                }
                startRegionId = regionId;
            }
            else
            {
                if (!GetPointOnNavMesh(start, out startRegionId, out startPolygonId, out startOnNavmesh))
                {
                    Debug.LogWarning("Can't find start point on navmesh!");
                    return PathState.NoPath;
                }
            }

            // The end may now live in a different region, reachable through a NavMeshLink2d.
            if (!GetPointOnNavMesh(end, out endRegionId, out endPolygonId, out endOnNavmesh))
            {
                Debug.LogWarning("Can't find end point on navmesh!");
                return PathState.NoPath;
            }

            regionId = startRegionId;

            return PlanPathOnNavmesh(startOnNavmesh, startRegionId, startPolygonId, endOnNavmesh, endRegionId, endPolygonId, ref polygons, ref path, pathMidBias, agentType, agent, allowPartial, polygonRegions);
        }

        // Back-compatible single-region entry point (start and end resolved in the same region).
        public PathState PlanPathOnNavmesh(Vector3 start, int startPolygonId, Vector3 end, int endPolygonId, int regionId, ref List<int> polygons, ref List<PathNode> path, float pathMidBias = -float.MaxValue, NavMeshAgentType2d agentType = null, NavMeshAgent2d agent = null, bool allowPartial = true, List<int> polygonRegions = null)
        {
            return PlanPathOnNavmesh(start, regionId, startPolygonId, end, regionId, endPolygonId, ref polygons, ref path, pathMidBias, agentType, agent, allowPartial, polygonRegions);
        }

        // Cross-region capable entry point. Start and end polygons may live in different regions;
        // they are connected only through NavMeshLink2d bridges whose conditions allow passage.
        // 'polygonRegions', if supplied, is filled in parallel with 'polygons' so each polygon id can be
        // mapped back to the region it belongs to (the path may span several regions through links).
        public PathState PlanPathOnNavmesh(Vector3 start, int startRegionId, int startPolygonId, Vector3 end, int endRegionId, int endPolygonId, ref List<int> polygons, ref List<PathNode> path, float pathMidBias = -float.MaxValue, NavMeshAgentType2d agentType = null, NavMeshAgent2d agent = null, bool allowPartial = true, List<int> polygonRegions = null)
        {
            if (polygons == null) polygons = new();
            if (path == null) path = new();

            var startNode = new NavNode(startRegionId, startPolygonId);
            var endNode = new NavNode(endRegionId, endPolygonId);

            // pathMode currently selects the (only implemented) mid-edge planner; kept for future modes.
            PathState state;
            switch (pathMode)
            {
                case PathMode.MidEdge:
                default:
                    state = PlanPathGlobal(start, startNode, end, endNode, allowPartial, agent, agentType, out var nodePath, out var transitions, out var effectiveEnd);
                    if (state == PathState.NoPath) return PathState.NoPath;

                    foreach (var n in nodePath) { polygons.Add(n.poly); polygonRegions?.Add(n.region); }

                    float bias = (pathMidBias < 0) ? funnelBias : pathMidBias;
                    path.AddRange(BuildPath(start, effectiveEnd, transitions, funnelEnable, bias));
                    break;
            }

            return state;
        }

        private float GetCost(NavMeshAgentType2d agent, NavMeshTerrainType2d terrainType)
        {
            if (agent == null)
            {
                return agentType.GetCost(terrainType);
            }

            return agent.GetCost(terrainType);
        }

        // ---- Link registry -------------------------------------------------------------------
        // Links register themselves here; each navmesh selects the links whose agent type resolves
        // (via Get) to itself. A version counter lets every navmesh rebuild its adjacency lazily
        // when links are added, removed or moved.
        static readonly List<NavMeshLink2d>     s_Links = new();
        static int                              s_LinksVersion = 0;

        public static void RegisterLink(NavMeshLink2d link)
        {
            if (link == null) return;
            if (!s_Links.Contains(link)) { s_Links.Add(link); s_LinksVersion++; }
        }

        public static void UnregisterLink(NavMeshLink2d link)
        {
            if (s_Links.Remove(link)) s_LinksVersion++;
        }

        public static void InvalidateLinks()
        {
            s_LinksVersion++;
        }

        // ---- Internal pathfinding types ------------------------------------------------------
        readonly struct NavNode : IEquatable<NavNode>
        {
            public readonly int region;
            public readonly int poly;
            public NavNode(int region, int poly) { this.region = region; this.poly = poly; }
            public bool Equals(NavNode other) => region == other.region && poly == other.poly;
            public override bool Equals(object obj) => obj is NavNode o && Equals(o);
            public override int GetHashCode() => (region * 73856093) ^ poly;
            public static bool operator ==(NavNode a, NavNode b) => a.Equals(b);
            public static bool operator !=(NavNode a, NavNode b) => !a.Equals(b);
        }

        class LinkConnection
        {
            public NavMeshLink2d    link;
            public NavNode          to;
            public Vector2          exit;    // point on the 'from' polygon where the agent leaves
            public Vector2          enter;   // point on the 'to' polygon where the agent arrives
            public float            baseCost;
            public bool             manual;
        }

        struct Transition
        {
            public bool             isLink;
            public NavMeshLink2d    link;
            public bool             manual;
            public Vector2          edgeA, edgeB;   // edge transition: shared edge endpoints (unoriented)
            public Vector2          exit, enter;    // link transition: bridge endpoints
        }

        [NonSerialized] Dictionary<NavNode, List<LinkConnection>>    _linkAdjacency;
        [NonSerialized] int                                         _builtLinksVersion = -1;

        // ---- Link resolution -----------------------------------------------------------------
        // Returns the links this navmesh should consider. At runtime that is the registry populated by
        // OnEnable; in the editor (where links are not registered, e.g. in edit mode) they are
        // discovered from the open scene so debug drawing and path testing work without Play Mode.
        IReadOnlyList<NavMeshLink2d> GetLinkSource()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return FindObjectsByType<NavMeshLink2d>(FindObjectsSortMode.None);
#endif
            return s_Links;
        }

        void EnsureLinkAdjacency()
        {
            bool editorMode = false;
#if UNITY_EDITOR
            editorMode = !Application.isPlaying;
#endif
            // In the editor, links are not kept in the registry, so rebuild each time to reflect edits.
            if (!editorMode && _linkAdjacency != null && _builtLinksVersion == s_LinksVersion) return;

            _linkAdjacency = new Dictionary<NavNode, List<LinkConnection>>();
            _builtLinksVersion = s_LinksVersion;

            var regions = QRegionData;
            if (regions == null || regions.Count == 0) return;

            var links = GetLinkSource();
            for (int li = 0; li < links.Count; li++)
            {
                var link = links[li];
                if (link == null) continue;
                if (Get(link.AgentType) != this) continue;
                if (!ResolveLink(link, out var aNode, out var bNode, out Vector2 exitA, out Vector2 enterB)) continue;

                float baseCost = Vector2.Distance(exitA, enterB) * link.CostMultiplier;
                bool manual = !link.IsAutoTraverse;

                AddLinkConnection(aNode, new LinkConnection { link = link, to = bNode, exit = exitA, enter = enterB, baseCost = baseCost, manual = manual });
                if (link.IsBidirectional)
                    AddLinkConnection(bNode, new LinkConnection { link = link, to = aNode, exit = enterB, enter = exitA, baseCost = baseCost, manual = manual });
            }
        }

        void AddLinkConnection(NavNode from, LinkConnection c)
        {
            if (from == c.to) return;   // ignore self loops
            if (!_linkAdjacency.TryGetValue(from, out var list))
            {
                list = new List<LinkConnection>();
                _linkAdjacency[from] = list;
            }
            list.Add(c);
        }

        // Resolves a link's endpoints to navmesh nodes plus the points the agent uses to cross.
        // Auto-traverse: the crossing points are the closest points between the two polygons (closest
        // point to B inside PA, closest point to A inside PB). Manual: the literal endpoint positions
        // (the agent walks to A and an INavMeshLinkTraversal carries it across to B).
        bool ResolveLink(NavMeshLink2d link, out NavNode aNode, out NavNode bNode, out Vector2 exitA, out Vector2 enterB)
        {
            aNode = default; bNode = default; exitA = default; enterB = default;

            Vector3 aWorld = link.worldStart;
            Vector3 bWorld = link.worldEnd;

            if (!GetPointOnNavMesh(aWorld, out int regionA, out int polyA, out Vector3 _)) return false;
            if (!GetPointOnNavMesh(bWorld, out int regionB, out int polyB, out Vector3 _)) return false;

            aNode = new NavNode(regionA, polyA);
            bNode = new NavNode(regionB, polyB);
            if (aNode == bNode) return false;

            if (link.IsAutoTraverse)
            {
                GetPoly(regionA, polyA).Distance((Vector2)bWorld, out exitA);
                GetPoly(regionB, polyB).Distance((Vector2)aWorld, out enterB);
            }
            else
            {
                exitA = aWorld;
                enterB = bWorld;
            }
            return true;
        }

        // ---- Region-agnostic A* --------------------------------------------------------------
        // Runs the mid-edge A*. When 'ignoreConditions' is set, links are treated as always passable
        // (used to discover the ideal route for partial paths). Returns whether the goal was reached,
        // the search trees, and the reachable node whose portal is closest to the goal.
        bool RunAStar(Vector3 start, NavNode startNode, NavNode endNode, Vector2 endPos, bool ignoreConditions,
                      NavMeshAgent2d agent, NavMeshAgentType2d agentType,
                      out Dictionary<NavNode, NavNode> cameFrom, out Dictionary<NavNode, Transition> cameVia,
                      out NavNode bestNode)
        {
            var frontier = new PriorityQueue<NavNode, float>();
            cameFrom = new Dictionary<NavNode, NavNode>();
            cameVia = new Dictionary<NavNode, Transition>();
            var costSoFar = new Dictionary<NavNode, float>();
            var entryPoint = new Dictionary<NavNode, Vector2>();

            float minUnitCost = Mathf.Max(1e-5f, QCostRange.x);

            frontier.Enqueue(startNode, 0);
            costSoFar[startNode] = 0;
            entryPoint[startNode] = (Vector2)start;

            bool reached = (startNode == endNode);
            bestNode = startNode;
            float bestHeuristic = Vector2.Distance((Vector2)start, endPos);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (current == endNode) { reached = true; break; }

                Vector2 fromPos = entryPoint[current];
                var rd = QRegionData[current.region];
                var currentPoly = rd.polygons[current.poly];
                var vertices = rd.vertices;
                float unitCost = Mathf.Max(1e-5f, GetCost(agentType, currentPoly.terrainType));
                float baseSoFar = costSoFar[current];

                // Edge neighbours (always inside the same region).
                for (int i = 0; i < currentPoly.neighbors.Count; i++)
                {
                    int neighbor = currentPoly.neighbors[i];
                    if (neighbor == -1) continue;

                    int vi = currentPoly[i];
                    int vj = currentPoly[(i + 1) % currentPoly.Count];
                    Vector2 va = vertices[vi];
                    Vector2 vb = vertices[vj];
                    Vector2 edgeMid = 0.5f * (va + vb);

                    float newCost = baseSoFar + Vector2.Distance(fromPos, edgeMid) * unitCost;
                    var neighborNode = new NavNode(current.region, neighbor);

                    if (!costSoFar.TryGetValue(neighborNode, out float prev) || newCost < prev)
                    {
                        float h = Vector2.Distance(edgeMid, endPos);
                        costSoFar[neighborNode] = newCost;
                        frontier.Enqueue(neighborNode, newCost + h * minUnitCost);
                        cameFrom[neighborNode] = current;
                        cameVia[neighborNode] = new Transition { isLink = false, edgeA = va, edgeB = vb };
                        entryPoint[neighborNode] = edgeMid;
                        if (h < bestHeuristic) { bestHeuristic = h; bestNode = neighborNode; }
                    }
                }

                // Link neighbours (possibly cross-region). Conditions are skipped for speculative runs.
                if (_linkAdjacency.TryGetValue(current, out var links))
                {
                    for (int i = 0; i < links.Count; i++)
                    {
                        var c = links[i];
                        if (!ignoreConditions && !c.link.CanPass(agent)) continue;

                        float newCost = baseSoFar + Vector2.Distance(fromPos, c.exit) * unitCost + c.baseCost;
                        var neighborNode = c.to;

                        if (!costSoFar.TryGetValue(neighborNode, out float prev) || newCost < prev)
                        {
                            float h = Vector2.Distance(c.enter, endPos);
                            costSoFar[neighborNode] = newCost;
                            frontier.Enqueue(neighborNode, newCost + h * minUnitCost);
                            cameFrom[neighborNode] = current;
                            cameVia[neighborNode] = new Transition { isLink = true, link = c.link, manual = c.manual, exit = c.exit, enter = c.enter };
                            entryPoint[neighborNode] = c.enter;
                            if (h < bestHeuristic) { bestHeuristic = h; bestNode = neighborNode; }
                        }
                    }
                }
            }

            return reached;
        }

        // Reconstructs the node + transition sequence from startNode to 'target'.
        static void BuildNodePath(NavNode target, NavNode startNode,
                                  Dictionary<NavNode, NavNode> cameFrom, Dictionary<NavNode, Transition> cameVia,
                                  out List<NavNode> nodePath, out List<Transition> transitions)
        {
            var revNodes = new List<NavNode> { target };
            var revTrans = new List<Transition>();
            var cur = target;
            while (cur != startNode)
            {
                revTrans.Add(cameVia[cur]);
                cur = cameFrom[cur];
                revNodes.Add(cur);
            }
            revNodes.Reverse();
            revTrans.Reverse();
            nodePath = revNodes;
            transitions = revTrans;
        }

        PathState PlanPathGlobal(Vector3 start, NavNode startNode, Vector3 end, NavNode endNode, bool allowPartial,
                                 NavMeshAgent2d agent, NavMeshAgentType2d agentType,
                                 out List<NavNode> nodePath, out List<Transition> transitions, out Vector3 effectiveEnd)
        {
            nodePath = null;
            transitions = null;
            effectiveEnd = end;

            var regions = QRegionData;
            if (regions == null) return PathState.NoPath;
            if (startNode.region < 0 || startNode.region >= regions.Count) return PathState.NoPath;
            if (endNode.region < 0 || endNode.region >= regions.Count) return PathState.NoPath;

            EnsureLinkAdjacency();

            Vector2 endPos = end;

            // Pass 1: the real search, with link conditions enforced.
            bool reached = RunAStar(start, startNode, endNode, endPos, false, agent, agentType,
                                    out var cameFrom, out var cameVia, out var bestNode);
            if (reached)
            {
                BuildNodePath(endNode, startNode, cameFrom, cameVia, out nodePath, out transitions);
                return PathState.Full;
            }

            if (!allowPartial) return PathState.NoPath;

            // Pass 2: speculate that every link is open to find the route the agent would ideally take,
            // then stop at the first link along it that is actually closed (i.e. head for the door).
            bool specReached = RunAStar(start, startNode, endNode, endPos, true, agent, agentType,
                                        out var specFrom, out var specVia, out _);
            if (specReached)
            {
                BuildNodePath(endNode, startNode, specFrom, specVia, out var specNodes, out var specTrans);
                for (int k = 0; k < specTrans.Count; k++)
                {
                    var t = specTrans[k];
                    if (t.isLink && !t.link.CanPass(agent))
                    {
                        // specNodes[k] is the reachable node on the near side of this closed link.
                        nodePath = specNodes.GetRange(0, k + 1);
                        transitions = specTrans.GetRange(0, k);
                        effectiveEnd = new Vector3(t.exit.x, t.exit.y, end.z);
                        return PathState.Partial;
                    }
                }
                // (A closed link must exist here, since the real search failed; fall through if not.)
            }

            // Goal unreachable even with every link open (genuinely disconnected): fall back to the
            // reachable node whose polygon comes closest to the goal.
            GetPoly(bestNode.region, bestNode.poly).Distance(endPos, out Vector2 closest);
            effectiveEnd = new Vector3(closest.x, closest.y, end.z);
            BuildNodePath(bestNode, startNode, cameFrom, cameVia, out nodePath, out transitions);
            return PathState.Partial;
        }

        // ---- Corridor / funnel ---------------------------------------------------------------
        // Builds geometric waypoints from the transition list. Edge transitions inside one region are
        // string-pulled (funnel); each link transition is a forced crossing: the run terminates exactly
        // at the link 'exit' (tagged with the link) and the next run starts at 'enter'.
        List<PathNode> BuildPath(Vector3 start, Vector3 end, List<Transition> transitions, bool funnel, float bias)
        {
            var result = new List<PathNode> { new PathNode(start) };

            Vector2 runStart = start;
            var runEdges = new List<Transition>();

            void FlushRun(Vector2 runGoal)
            {
                if (funnel)
                {
                    BuildFunnelPortals(runStart, runEdges, bias, out var lefts, out var rights);
                    var pts = StringPull(runStart, runGoal, lefts, rights);
                    for (int i = 1; i < pts.Count; i++) result.Add(new PathNode(pts[i]));
                }
                else
                {
                    foreach (var e in runEdges) result.Add(new PathNode(0.5f * (e.edgeA + e.edgeB)));
                }
                // Guarantee the run terminates exactly at runGoal (link tagging relies on this).
                if (((Vector2)result[result.Count - 1].pos - runGoal).sqrMagnitude > 1e-8f)
                    result.Add(new PathNode(runGoal));
                runEdges.Clear();
            }

            foreach (var t in transitions)
            {
                if (!t.isLink) { runEdges.Add(t); continue; }

                FlushRun(t.exit);
                result[result.Count - 1] = new PathNode(t.exit, t.link, t.manual);   // tag the near side
                result.Add(new PathNode(t.enter));
                runStart = t.enter;
            }

            FlushRun(end);

            return Dedupe(result);
        }

        // Orients each shared edge into (left,right) using local travel direction, applying funnel bias.
        void BuildFunnelPortals(Vector2 runStart, List<Transition> edges, float bias, out List<Vector2> lefts, out List<Vector2> rights)
        {
            lefts = new List<Vector2>(edges.Count);
            rights = new List<Vector2>(edges.Count);

            Vector2 prev = runStart;
            foreach (var e in edges)
            {
                Vector2 mid = 0.5f * (e.edgeA + e.edgeB);
                Vector2 dir = mid - prev;

                Vector2 rawLeft, rawRight;
                if (Vector3.Cross((Vector3)dir, (Vector3)(e.edgeB - e.edgeA)).z >= 0f) { rawLeft = e.edgeA; rawRight = e.edgeB; }
                else { rawLeft = e.edgeB; rawRight = e.edgeA; }

                if (bias > 0f)
                {
                    rawLeft = Vector2.Lerp(rawLeft, mid, bias);
                    rawRight = Vector2.Lerp(rawRight, mid, bias);
                }

                lefts.Add(rawLeft);
                rights.Add(rawRight);
                prev = mid;
            }
        }

        // Standard funnel (Demyen) string-pulling over an oriented portal list. Returns positions
        // including 'startApex' at [0] and ending at 'goal'.
        static List<Vector2> StringPull(Vector2 startApex, Vector2 goal, List<Vector2> lefts, List<Vector2> rights)
        {
            var L = new List<Vector2>(lefts) { goal };
            var R = new List<Vector2>(rights) { goal };

            var outPts = new List<Vector2> { startApex };
            Vector2 apex = startApex, left = startApex, right = startApex;
            int apexIndex = 0, leftIndex = 0, rightIndex = 0;

            for (int i = 0; i < L.Count; i++)
            {
                Vector2 newLeft = L[i];
                Vector2 newRight = R[i];

                // right boundary
                if (Triangle.SignedArea2(apex, right, newRight) <= 0f)
                {
                    if ((apex == right) || (Triangle.SignedArea2(apex, left, newRight) > 0f))
                    {
                        right = newRight; rightIndex = i;
                    }
                    else
                    {
                        outPts.Add(left);
                        apex = left; apexIndex = leftIndex;
                        left = apex; right = apex; leftIndex = apexIndex; rightIndex = apexIndex;
                        i = apexIndex;
                        continue;
                    }
                }

                // left boundary
                if (Triangle.SignedArea2(apex, left, newLeft) >= 0f)
                {
                    if ((apex == left) || (Triangle.SignedArea2(apex, right, newLeft) < 0f))
                    {
                        left = newLeft; leftIndex = i;
                    }
                    else
                    {
                        outPts.Add(right);
                        apex = right; apexIndex = rightIndex;
                        left = apex; right = apex; leftIndex = apexIndex; rightIndex = apexIndex;
                        i = apexIndex;
                        continue;
                    }
                }
            }

            if (outPts[outPts.Count - 1] != goal) outPts.Add(goal);
            return outPts;
        }

        static List<PathNode> Dedupe(List<PathNode> pts)
        {
            var outp = new List<PathNode>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                if (outp.Count > 0 && pts[i].link == null &&
                    ((Vector2)pts[i].pos - (Vector2)outp[outp.Count - 1].pos).sqrMagnitude < 1e-6f)
                    continue;
                outp.Add(pts[i]);
            }
            return outp;
        }

        private ConvexPolygon GetPoly(int regionId, int polygonId)
        {
            return QRegionData[regionId].polygons[polygonId];
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
            var rd = QRegionData[regionId];
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

                    // solve ray�segment intersection in 2D:
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

        #region Obstacles

        // Called by NavMeshObstacle2d when it is enabled: rebuild only if this navmesh wasn't already
        // baked with it (so scene-placed obstacles don't each force a rebuild on load).
        public static void NotifyObstacleAdded(NavMeshObstacle2d obstacle)
        {
            if (obstacle == null) return;
            foreach (var nm in s_NavMeshes)
                if (nm.AppliesObstacle(obstacle) && !nm.WasBakedWith(obstacle))
                    nm.RequestRebuild();
        }

        // Called when an obstacle is disabled/destroyed: rebuild only if it was part of the last bake.
        public static void NotifyObstacleRemoved(NavMeshObstacle2d obstacle)
        {
            if (obstacle == null) return;
            foreach (var nm in s_NavMeshes)
                if (nm.AppliesObstacle(obstacle) && nm.WasBakedWith(obstacle))
                    nm.RequestRebuild();
        }

        bool AppliesObstacle(NavMeshObstacle2d obstacle)
        {
            var at = obstacle.AgentType;
            return at == null || Get(at) == this;
        }

        public bool WasBakedWith(NavMeshObstacle2d obstacle)
        {
            return bakedObstacles != null && bakedObstacles.Contains(obstacle);
        }

        // True when the live, applicable obstacles are exactly the set the last bake used.
        bool ObstaclesMatchBake()
        {
            var current = GatherObstacles();
            foreach (var o in current)
                if (!WasBakedWith(o)) return false;

            int liveBaked = 0;
            if (bakedObstacles != null)
                foreach (var o in bakedObstacles) if (o != null) liveBaked++;

            return current.Count == liveBaked;
        }

        public void RequestRebuild()
        {
            _rebuildRequested = true;
        }

        List<NavMeshObstacle2d> GatherObstacles()
        {
            var result = new List<NavMeshObstacle2d>();
            foreach (var o in FindObjectsByType<NavMeshObstacle2d>(FindObjectsSortMode.None))
            {
                if (o == null || !o.isActiveAndEnabled) continue;
                if (!AppliesObstacle(o)) continue;
                result.Add(o);
            }
            return result;
        }

        void BuildObstacleColliderSet()
        {
            _obstacleColliders = new HashSet<Collider2D>();
            var tmp = new List<Collider2D>();
            foreach (var o in bakedObstacles)
            {
                if (o == null) continue;
                o.GetColliders(tmp);
                foreach (var c in tmp) if (c != null) _obstacleColliders.Add(c);
            }
        }

        #endregion

        #region Debug data access (consumed by NavMeshDebug2d)

        internal NavMeshAgentType2d         DebugAgentType => agentType;
        internal int                        DebugCellSize => QCellSize;
        internal Vector2Int                 DebugGridSize => QGridSize;
        internal byte[]                     DebugGrid => QGrid;
        internal byte[]                     DebugRegionMap => QRegion;
        internal NavMeshTerrainType2d[]     DebugTerrain => QTerrain;
        internal Vector2                    DebugCostRange => QCostRange;
        internal bool                       DebugHasValidGrid => hasValidGrid;
        internal bool                       DebugHasValidRegions => hasValidRegions;
        internal IReadOnlyList<RegionData>  DebugRegions => QRegionData;
        internal Vector2                    DebugGridToWorld(int x, int y) => new Vector2(x, y) * QCellSize + QGridOffset;
        internal float                      DebugGetCost(NavMeshAgentType2d agentType, NavMeshTerrainType2d terrainType) => GetCost(agentType, terrainType);
        internal ConvexPolygon              DebugGetPoly(int regionId, int polygonId) => GetPoly(regionId, polygonId);
        internal IReadOnlyList<NavMeshLink2d> DebugLinkSource() => GetLinkSource();
        internal bool                       DebugResolveLink(NavMeshLink2d link, out Vector2 exitA, out Vector2 enterB)
            => ResolveLink(link, out _, out _, out exitA, out enterB);

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
