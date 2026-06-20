using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{
    /// <summary>
    /// Optional companion for <see cref="NavMesh2d"/>: holds all the debug visualization options and
    /// draws the gizmos, so the navmesh itself stays free of debug code. Attach it to the same
    /// GameObject as the NavMesh2d.
    /// </summary>
    [RequireComponent(typeof(NavMesh2d))]
    public class NavMeshDebug2d : MonoBehaviour
    {
        [SerializeField]
        private bool                debugEnabled;
        [SerializeField, ShowIf(nameof(debugEnabled)), Tooltip("If set, debug gizmos render even when this object is not selected (OnDrawGizmos). If clear, they only render while it is selected (OnDrawGizmosSelected).")]
        private bool                debugWhenNotSelected = true;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugGrid;
        [SerializeField, ShowIf(nameof(debugGridEnabled))]
        private bool                colorByRegion;
        [SerializeField, ShowIf(nameof(debugGridEnabled))]
        private bool                colorByCost;
        [SerializeField, ShowIf(nameof(debugGridEnabled))]
        private bool                costLabel;
        [SerializeField, ShowIf(nameof(needCostLabelScale))]
        private float               costLabelScale = 1.0f;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugContours;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugLinks;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugPolygons;
        [SerializeField, ShowIf(nameof(debugPolygonsEnabled))]
        private bool                colorPolygonsByCost;
        [SerializeField, ShowIf(nameof(debugPolygonsEnabled))]
        private bool                polygonCostLabel;
        [SerializeField, ShowIf(nameof(needPolygonCostLabelScale))]
        private float               polygonCostLabelScale = 1.0f;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugTestNearPoint;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugTestPath;
        [SerializeField, ShowIf(nameof(needDebugTestRegionToggle)), Tooltip("If set, the debug test path is restricted to 'Test Region Id'. If clear, it plans across regions (through links), using a null agent for link conditions.")]
        private bool                debugTestRegion = true;
        [SerializeField, ShowIf(nameof(debugEnabled)), Label("Debug Test LoS")]
        private bool                debugTestLoS;
        [SerializeField, ShowIf(nameof(needTestPoint))]
        private Transform           testPoint;
        [SerializeField, ShowIf(nameof(needPoints))]
        private Transform           startPoint;
        [SerializeField, ShowIf(nameof(needPoints))]
        private Transform           endPoint;
        [SerializeField, ShowIf(nameof(needTestRegion))]
        private int                 testRegionId;
        [SerializeField, ShowIf(nameof(needTestAgent))]
        private NavMeshAgentType2d  testAgent;

        bool needTestPoint => debugEnabled && debugTestNearPoint;
        bool needTestRegion => debugEnabled && (debugTestNearPoint || debugTestLoS || (debugTestPath && debugTestRegion));
        bool needDebugTestRegionToggle => debugEnabled && debugTestPath;
        bool debugGridEnabled => debugEnabled && debugGrid;
        bool debugPolygonsEnabled => debugEnabled && debugPolygons;
        bool needPoints => debugEnabled && (debugTestPath || debugTestLoS);
        bool needCostLabelScale => debugGridEnabled && costLabel;
        bool needPolygonCostLabelScale => debugPolygonsEnabled && polygonCostLabel;
        bool needTestAgent => debugEnabled && debugTestPath;

        // Whether the navmesh should keep its grid/region/terrain data after baking (for visualization).
        public bool DebugEnabled => debugEnabled;

        private NavMesh2d _navMesh;
        private NavMesh2d navMesh
        {
            get
            {
                if (_navMesh == null) _navMesh = GetComponent<NavMesh2d>();
                return _navMesh;
            }
        }

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
            Vector2 costRange = navMesh.DebugCostRange;
            var normalizedCost = Mathf.InverseLerp(costRange.x, costRange.y, cost);
            if (costRange.x == costRange.y) normalizedCost = 1.0f;
            var c = baseColor;
            c = Color.Lerp(c * 0.25f, c, normalizedCost);
            c.a = baseColor.a;

            return c;
        }

        void OnDrawGizmos()
        {
            if (debugWhenNotSelected) DrawDebugGizmos();
        }

        void OnDrawGizmosSelected()
        {
            if (!debugWhenNotSelected) DrawDebugGizmos();
        }

        void DrawDebugGizmos()
        {
            var nm = navMesh;
            if (nm == null || !debugEnabled) return;

            if ((debugGrid) && (nm.DebugHasValidGrid))
            {
                var     gridSize = nm.DebugGridSize;
                var     grid = nm.DebugGrid;
                var     region = nm.DebugRegionMap;
                var     terrainType = nm.DebugTerrain;
                int     cellSize = nm.DebugCellSize;
                bool    draw = true;
                int     index = 0;
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int x = 0; x < gridSize.x; x++)
                    {
                        bool    wall = false;
                        Vector2 boxCenterPos = nm.DebugGridToWorld(x, y);
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

                        var cost = nm.DebugGetCost(nm.DebugAgentType, terrainType[index]);

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

            if ((debugContours) && (nm.DebugHasValidRegions))
            {
                var regionData = nm.DebugRegions;
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

            if ((debugPolygons) && (nm.DebugHasValidRegions))
            {
                var regionData = nm.DebugRegions;
                int cellSize = nm.DebugCellSize;
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

                            var cost = nm.DebugGetCost(nm.DebugAgentType, poly.terrainType);

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

            if ((debugLinks) && (nm.DebugHasValidRegions))
            {
                var links = nm.DebugLinkSource();
                for (int li = 0; li < links.Count; li++)
                {
                    var link = links[li];
                    if (link == null) continue;
                    if (NavMesh2d.Get(link.AgentType) != nm) continue;
                    if (!nm.DebugResolveLink(link, out Vector2 exitA, out Vector2 enterB)) continue;

                    // Green when the link can currently be traversed, red otherwise. A condition that
                    // throws (e.g. due to the null debug agent) is treated as blocking by CanPass.
                    bool passable = link.CanPass(null);
                    Gizmos.color = passable ? new Color(0.1f, 0.9f, 0.4f, 1.0f) : new Color(0.9f, 0.15f, 0.15f, 1.0f);

                    // Just the bridge between the two polygon edge points, with a marker at each
                    // terminal (mirrors the selected-link look).
                    Gizmos.DrawLine(exitA, enterB);
                    Gizmos.DrawSphere(exitA, 1.5f);
                    Gizmos.DrawSphere(enterB, 1.5f);
                }
            }

            if ((debugTestNearPoint) && (testPoint))
            {
                if ((testRegionId >= 0) && (testRegionId < nm.DebugRegions.Count))
                {
                    if (nm.GetPointOnNavMesh(testPoint.position, testRegionId, out Vector3 navMeshPoint))
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawSphere(testPoint.position, 2.0f);
                        Gizmos.DrawSphere(navMeshPoint, 2.0f);
                        Gizmos.DrawLine(testPoint.position, navMeshPoint);
                    }
                }
                else
                {
                    if (nm.GetPointOnNavMesh(testPoint.position, out int regionId, out Vector3 navMeshPoint))
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
                List<int>               polygons = null;
                List<int>               polygonRegions = new();
                // When region testing is off, plan across regions (through links) with a null agent.
                int                     regionId = debugTestRegion ? testRegionId : -1;
                List<NavMesh2d.PathNode> path = null;
                var                     agent = (testAgent != null) ? (testAgent) : (nm.DebugAgentType);
                var                     pathState = nm.PlanPath(startPoint.position, endPoint.position, ref regionId, ref polygons, ref path, agentType : agent, polygonRegions : polygonRegions);
                if (pathState != NavMesh2d.PathState.NoPath)
                {
                    if (debugPolygons)
                    {
                        // Each polygon is drawn in its own region's colour, a bit transparent
                        // (the path may cross regions via links).
                        for (int i = 0; i < polygons.Count; i++)
                        {
                            int r = (i < polygonRegions.Count) ? polygonRegions[i] : regionId;
                            if (r < 0 || r >= nm.DebugRegions.Count) continue;
                            if (polygons[i] < 0 || polygons[i] >= nm.DebugRegions[r].polygons.Count) continue;
                            var poly = nm.DebugGetPoly(r, polygons[i]);
                            if (poly != null)
                            {
                                Gizmos.color = regionColors[r % regionColors.Length].ChangeAlpha(0.5f);
                                DebugHelpers.DrawConvexPolygon(poly.vertices, poly.indices);
                            }
                        }
                    }
                    // Partial paths (goal unreachable) are drawn orange, with the gap to the real target.
                    Gizmos.color = (pathState == NavMesh2d.PathState.Partial) ? new Color(1.0f, 0.5f, 0.0f) : Color.blue;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Gizmos.DrawLine(path[i].pos, path[i + 1].pos);
                    }
                    if ((pathState == NavMesh2d.PathState.Partial) && (path.Count > 0))
                    {
                        Vector3 stop = path[path.Count - 1].pos;
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(stop, 2.0f);
                        Handles.color = Color.red;
                        Handles.DrawDottedLine(stop, endPoint.position, 2.0f);
                    }
                }
            }

            if ((debugTestLoS) && (startPoint))
            {
                if (nm.RaycastSegment(startPoint.position, endPoint.position, testRegionId, out Vector3 actualEndPoint, out int polygonId))
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.green;

                Gizmos.DrawLine(startPoint.position, actualEndPoint);
                Handles.DrawDottedLine(actualEndPoint, endPoint.position, 2);
            }
        }
#endif
    }
}
