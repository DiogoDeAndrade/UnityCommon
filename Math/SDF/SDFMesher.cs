using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class SDFMesher : MonoBehaviour
    {
        public enum NormalMode { Simple, Estimated };

        [SerializeField]
        private SDFComponent        sdf;
        [SerializeField] 
        private NormalMode          normalMode;
        [SerializeField]
        private float               voxelsPerUnit = 1.0f;
        [SerializeField]
        private Material            defaultMaterial;
        [SerializeField]
        private bool                simplify;
        [SerializeField, Range(0.0f, 1.0f), ShowIf(nameof(simplify))]
        private float               quality = 0.5f;        
        [SerializeField, ShowIf(nameof(simplify))]
        private bool                preserveBorderEdges = false;
        [SerializeField, ShowIf(nameof(simplify))]
        private bool                preserveSurfaceCurvature = false;
        [SerializeField]
        private bool                debugEnabled;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugShowGrid;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private bool                debugShowGridLines;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private Gradient            debugColorRange;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private Vector2             debugFilterRange;

        [SerializeField, HideInInspector]
        private VoxelData<float> voxelData;
        [SerializeField, ReadOnly]
        private Vector2          distanceRange;

        bool isShowGrid => debugEnabled && debugShowGrid;

        public float voxelSizeF => 1.0f / voxelsPerUnit;
        public Vector3 voxelSize => Vector3.one * voxelSizeF;


        [Button("Build")]
        public void Build()
        {
            // Create the voxel field
            Bounds bounds = sdf.GetBounds();

            Vector3Int gs = new Vector3Int(Mathf.CeilToInt(bounds.size.x * voxelsPerUnit) + 1,
                                           Mathf.CeilToInt(bounds.size.y * voxelsPerUnit) + 1,
                                           Mathf.CeilToInt(bounds.size.z * voxelsPerUnit) + 1);

            voxelData = new VoxelData<float>();
            voxelData.Init(gs, Vector3.one / voxelsPerUnit);
            voxelData.minBound = bounds.min;

            distanceRange = new Vector2(-1.0f, 1.0f);

            SampleSDF();
            BuildMesh();
        }

        [Button("Reset Debug Filter Range")]
        public void ResetRange()
        {
            debugFilterRange = distanceRange;
        }

        void SampleSDF()
        {
            Vector3Int gs = voxelData.gridSize;
            for (int x = 0; x < gs.x; x++)
            {
                for (int y = 0; y < gs.y; y++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        var worldPoint = GetPos(x, y, z);
                        var value = sdf.Sample(worldPoint);
                        if (value < distanceRange.x) distanceRange.x = value;
                        if (value > distanceRange.y) distanceRange.y = value;

                        voxelData[x, y, z] = value;
                    }
                }
            }
        }

        void BuildMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = defaultMaterial;

            meshFilter.mesh = CreateSDFMesh();
        }

        Mesh CreateSDFMesh()
        {
            if (voxelData == null) return null;

            var verts = new List<Vector3>(8192);
            var norms = new List<Vector3>(8192);
            var uvs = new List<Vector2>(8192);
            var tris = new List<int>(16384);

            var gs = voxelData.gridSize;
            if (gs.x < 2 || gs.y < 2 || gs.z < 2) return null;

            int nx = gs.x, ny = gs.y, nz = gs.z;
            int[,,] vx = new int[nx - 1, ny, nz];
            int[,,] vy = new int[nx, ny - 1, nz];
            int[,,] vz = new int[nx, ny, nz - 1];
            FillArray(vx, -1);
            FillArray(vy, -1);
            FillArray(vz, -1);

            #region Helper Functions (FillArray, GetOrCreateEdgeVertex, ResolveAmbiguityHint, InterpolateVertex, EstimateNormal, ProjectUV)
            void FillArray(int[,,] a, int v)
            {
                for (int i = a.GetLowerBound(0); i <= a.GetUpperBound(0); i++)
                {
                    for (int j = a.GetLowerBound(1); j <= a.GetUpperBound(1); j++)
                    {
                        for (int k = a.GetLowerBound(2); k <= a.GetUpperBound(2); k++)
                        {
                            a[i, j, k] = v;
                        }
                    }
                }
            }

            int GetOrCreateEdgeVertex(int edgeId, int x, int y, int z,
                                      float iso, float epsBias,
                                      Vector3[] p, float[] val,
                                      List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
                                      float h)
            {
                int FetchX(int ix, int iy, int iz, Vector3 a, Vector3 b, float va, float vb)
                {
                    int idx = vx[ix, iy, iz];
                    if (idx >= 0) return idx;
                    Vector3 vpos = InterpolateVertex(iso, a, b, va + epsBias, vb + epsBias);
                    idx = verts.Count;
                    verts.Add(vpos);

                    if (normalMode == NormalMode.Estimated) norms.Add(EstimateNormal(vpos, h));
                    uvs.Add(ProjectUV(vpos));

                    vx[ix, iy, iz] = idx;
                    return idx;
                }

                int FetchY(int ix, int iy, int iz, Vector3 a, Vector3 b, float va, float vb)
                {
                    int idx = vy[ix, iy, iz];
                    if (idx >= 0) return idx;
                    Vector3 vpos = InterpolateVertex(iso, a, b, va + epsBias, vb + epsBias);
                    idx = verts.Count;
                    verts.Add(vpos);

                    if (normalMode == NormalMode.Estimated) norms.Add(EstimateNormal(vpos, h));
                    uvs.Add(ProjectUV(vpos));

                    vy[ix, iy, iz] = idx;
                    return idx;
                }

                int FetchZ(int ix, int iy, int iz, Vector3 a, Vector3 b, float va, float vb)
                {
                    int idx = vz[ix, iy, iz];
                    if (idx >= 0) return idx;
                    Vector3 vpos = InterpolateVertex(iso, a, b, va + epsBias, vb + epsBias);
                    idx = verts.Count;
                    verts.Add(vpos);

                    if (normalMode == NormalMode.Estimated) norms.Add(EstimateNormal(vpos, h));
                    uvs.Add(ProjectUV(vpos));

                    vz[ix, iy, iz] = idx;
                    return idx;
                }

                // Map MC edge to grid + which array
                switch (edgeId)
                {
                    case 0: return FetchX(x, y, z, p[0], p[1], val[0], val[1]);
                    case 1: return FetchY(x + 1, y, z, p[1], p[2], val[1], val[2]);
                    case 2: return FetchX(x, y + 1, z, p[2], p[3], val[2], val[3]);
                    case 3: return FetchY(x, y, z, p[3], p[0], val[3], val[0]);
                    case 4: return FetchX(x, y, z + 1, p[4], p[5], val[4], val[5]);
                    case 5: return FetchY(x + 1, y, z + 1, p[5], p[6], val[5], val[6]);
                    case 6: return FetchX(x, y + 1, z + 1, p[6], p[7], val[6], val[7]);
                    case 7: return FetchY(x, y, z + 1, p[7], p[4], val[7], val[4]);
                    case 8: return FetchZ(x, y, z, p[0], p[4], val[0], val[4]);
                    case 9: return FetchZ(x + 1, y, z, p[1], p[5], val[1], val[5]);
                    case 10: return FetchZ(x + 1, y + 1, z, p[2], p[6], val[2], val[6]);
                    case 11: return FetchZ(x, y + 1, z, p[3], p[7], val[3], val[7]);
                    default: return -1;
                }
            }

            // Small, deterministic bias steered by center sample to help break edge ties/epsilon.
            // You can extend this into a full asymptotic decider: pick alternates for ambiguous faces
            // based on 'centerVal' (and/or face centers) for MC33-like behavior.
            float ResolveAmbiguityHint(float centerVal)
            {
                // Signed epsilon scaled so it’s big vs float-ulp, small vs typical SDF magnitudes.
                // Change magnitude if your SDF ranges are tiny/huge.
                const float baseEps = 1e-6f;
                return (centerVal >= 0f) ? +baseEps : -baseEps;
            }

            Vector3 InterpolateVertex(float iso, Vector3 p1, Vector3 p2, float v1, float v2)
            {
                // Handle edge cases explicitly to avoid NaNs
                float d1 = v1 - iso;
                float d2 = v2 - iso;

                if (Mathf.Abs(d1) < 1e-12f) return p1;
                if (Mathf.Abs(d2) < 1e-12f) return p2;
                float denom = (d1 - d2);
                if (Mathf.Abs(denom) < 1e-12f) return (p1 + p2) * 0.5f;

                float t = d1 / (d1 - d2); // linear interpolation factor
                return p1 + t * (p2 - p1);
            }

            Vector3 EstimateNormal(Vector3 pos, float h)
            {
                // Central differences on SDF
                float dx = sdf.Sample(new Vector3(pos.x + h, pos.y, pos.z)) - sdf.Sample(new Vector3(pos.x - h, pos.y, pos.z));
                float dy = sdf.Sample(new Vector3(pos.x, pos.y + h, pos.z)) - sdf.Sample(new Vector3(pos.x, pos.y - h, pos.z));
                float dz = sdf.Sample(new Vector3(pos.x, pos.y, pos.z + h)) - sdf.Sample(new Vector3(pos.x, pos.y, pos.z - h));
                var n = new Vector3(dx, dy, dz);
                return n.sqrMagnitude > 0f ? n.normalized : Vector3.up;
            }

            Vector2 ProjectUV(Vector3 wpos)
            {
                // Basic planar projection (XZ) normalized to the local bounds size if available
                Vector3 local = wpos - voxelData.minBound;
                float sx = Mathf.Max(voxelData.size.x, 1e-6f);
                float sz = Mathf.Max(voxelData.size.z, 1e-6f);
                return new Vector2(local.x / sx, local.z / sz);
            }
            #endregion

            // Precompute a small differential step for SDF gradient estimation
            float h = Mathf.Min(voxelData.voxelSize.x, Mathf.Min(voxelData.voxelSize.y, voxelData.voxelSize.z)) * 0.5f;

            // Iterate all cells (cubes)
            for (int z = 0; z < gs.z - 1; z++)
            {
                for (int y = 0; y < gs.y - 1; y++)
                {
                    for (int x = 0; x < gs.x - 1; x++)
                    {
                        // Corner positions
                        Vector3[] p = new Vector3[8];
                        float[] val = new float[8];
                        int cubeIndex = 0;
                        const float iso = 0f;

                        for (int i = 0; i < 8; i++)
                        {
                            p[i] = GetPos(x + MCTables.MC_INCS[i, 0], y + MCTables.MC_INCS[i, 1], z + MCTables.MC_INCS[i, 2]);
                            val[i] = voxelData[x + MCTables.MC_INCS[i, 0], y + MCTables.MC_INCS[i, 1], z + MCTables.MC_INCS[i, 2]];
                            if (val[i] < iso) cubeIndex |= (1 << i);
                        }

                        if (cubeIndex == 0 || cubeIndex == 255) continue;

                        // Optional: center-sample hint for ambiguity resolution / tiebreaks
                        float centerVal = sdf.Sample(GetPos(x, y, z) + voxelSize * 0.5f);
                        float epsBias = ResolveAmbiguityHint(centerVal); // small bias for interpolation/ties

                        int edgeFlags = MCTables.MC_EDGE_TABLE[cubeIndex];
                        if (edgeFlags == 0) continue;

                        // Compute the 12 edge intersection points
                        Vector3[] edgeVert = new Vector3[12];

                        // For each edge, compute intersection by linear interpolation
                        for (int i = 0; i < 12; i++)
                        {
                            if ((edgeFlags & (1 << i)) != 0) edgeVert[i] = InterpolateVertex(iso, p[MCTables.MC_EDGE_INTERPOLATION[i, 0]], p[MCTables.MC_EDGE_INTERPOLATION[i, 1]], val[MCTables.MC_EDGE_INTERPOLATION[i, 0]] + epsBias, val[MCTables.MC_EDGE_INTERPOLATION[i, 1]] + epsBias);

                        }

                        // Emit triangles
                        for (int i = 0; MCTables.MC_TRI_TABLE[cubeIndex, i] != -1; i += 3)
                        {
                            int e0 = MCTables.MC_TRI_TABLE[cubeIndex, i];
                            int e1 = MCTables.MC_TRI_TABLE[cubeIndex, i + 1];
                            int e2 = MCTables.MC_TRI_TABLE[cubeIndex, i + 2];

                            int ia = GetOrCreateEdgeVertex(e0, x, y, z, iso, epsBias, p, val, verts, norms, uvs, h);
                            int ib = GetOrCreateEdgeVertex(e1, x, y, z, iso, epsBias, p, val, verts, norms, uvs, h);
                            int ic = GetOrCreateEdgeVertex(e2, x, y, z, iso, epsBias, p, val, verts, norms, uvs, h);

                            // keep your winding flip
                            tris.Add(ia); tris.Add(ic); tris.Add(ib);
                        }
                    }
                }
            }

            // Build mesh
            var mesh = new Mesh();
            mesh.name = "SDFMesh";
            mesh.indexFormat = (verts.Count > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32
                                                     : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            if (norms.Count > 0) mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateBounds();
            if (normalMode == NormalMode.Simple) mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            if (simplify)
            {
                var simplificationOptions = UnityMeshSimplifier.SimplificationOptions.Default;
                simplificationOptions.PreserveSurfaceCurvature = preserveSurfaceCurvature;
                simplificationOptions.PreserveBorderEdges = preserveBorderEdges;

                var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
                meshSimplifier.Initialize(mesh);
                meshSimplifier.SimplificationOptions = simplificationOptions;
                meshSimplifier.SimplifyMesh(quality);

                mesh = meshSimplifier.ToMesh();
                if (normalMode == NormalMode.Simple)
                {
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                }
            }

            return mesh;
        }


        private Vector3 GetPos(int x, int y, int z) => new Vector3(voxelData.minBound.x + x * voxelData.voxelSize.x,
                                                                       voxelData.minBound.y + y * voxelData.voxelSize.y,
                                                                       voxelData.minBound.z + z * voxelData.voxelSize.z);

        private void OnDrawGizmos()
        {
            if (!debugEnabled || !debugShowGrid || voxelData == null)
                return;

            Vector3Int gs = voxelData.gridSize;

            // 1) Draw grid lines by marching between corner points:

            if (debugShowGridLines)
            {
                Gizmos.color = Color.gray;

                // — lines along Z (vary x,y, connect z=0 -> z=gs.z-1)
                for (int x = 0; x < gs.x; x++)
                {
                    for (int y = 0; y < gs.y; y++)
                    {
                        Vector3 p1 = GetPos(x, y, 0);
                        Vector3 p2 = GetPos(x, y, gs.z - 1);
                        Gizmos.DrawLine(p1, p2);
                    }
                }

                // — lines along Y (vary x,z, connect y=0 -> y=gs.y-1)
                for (int x = 0; x < gs.x; x++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        Vector3 p1 = GetPos(x, 0, z);
                        Vector3 p2 = GetPos(x, gs.y - 1, z);
                        Gizmos.DrawLine(p1, p2);
                    }
                }

                // — lines along X (vary y,z, connect x=0 -> x=gs.x-1)
                for (int y = 0; y < gs.y; y++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        Vector3 p1 = GetPos(0, y, z);
                        Vector3 p2 = GetPos(gs.x - 1, y, z);
                        Gizmos.DrawLine(p1, p2);
                    }
                }
            }
            // 2) Draw a small colored sphere at each corner sample:
            float sphereRadius = voxelData.voxelSize.magnitude * 0.20f;
            for (int x = 0; x < gs.x; x++)
            {
                for (int y = 0; y < gs.y; y++)
                {
                    for (int z = 0; z < gs.z; z++)
                    {
                        float val = voxelData[x, y, z];
                        if ((val >= debugFilterRange.x) && (val <= debugFilterRange.y))
                        {
                            float t = Mathf.InverseLerp(distanceRange.x, distanceRange.y, val);
                            Color c = debugColorRange.Evaluate(t);

                            Gizmos.color = c;
                            Gizmos.DrawSphere(GetPos(x, y, z), sphereRadius);
                        }
                    }
                }
            }
        }
    }
}