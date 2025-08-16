using NaughtyAttributes;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{

    public class SDFMesher : MonoBehaviour
    {
        public enum NormalMode { Simple, Estimated, HardEdges };
        public enum FilterMode { Box2, Box3, Tent3, Gaussian5 };
        public enum NoiseMode { None, Perlin };

        [SerializeField]
        private SDFComponent        sdf;
        [SerializeField]
        private float               isoValue = 0.0f;
        [SerializeField] 
        private NormalMode          normalMode;
        [SerializeField]
        private float               voxelsPerUnit = 1.0f;
        [SerializeField]
        private bool                filter;
        [SerializeField, ShowIf(nameof(filter)), Range(0, 1)]
        private float               boundExtension = 0.25f;
        [SerializeField, ShowIf(nameof(filter)), Range(1, 4)]
        private int                 filterIterations;
        [SerializeField, ShowIf(nameof(filter))]
        private FilterMode          filterMode;
        [SerializeField] 
        private NoiseMode           noiseMode;
        [SerializeField, ShowIf(nameof(isPerlinNoise))] 
        private float               noiseStrength;
        [SerializeField, ShowIf(nameof(isPerlinNoise))] 
        private Vector3             perAxisStrength = Vector3.one;
        [SerializeField, ShowIf(nameof(isPerlinNoise))] 
        private float               noiseFrequency;
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
        [SerializeField, Range(0.0f, 180.0f), ShowIf(nameof(hasHardEdges))]
        private float               hardEdgeTolerance = 45.0f;
        [SerializeField]
        private bool                debugEnabled;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                debugShowGrid;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private bool                debugShowVoxelData;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private bool                debugShowGridLines;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private Gradient            debugColorRange;
        [SerializeField, ShowIf(nameof(isShowGrid))]
        private Vector2             debugFilterRange;
        [SerializeField, ShowIf(nameof(debugEnabled))]
        private bool                showNormals;

        [SerializeField, HideInInspector]
        private VoxelDataFloat  voxelData;
        [SerializeField, ReadOnly]
        private Vector2         distanceRange;

        bool isShowGrid => debugEnabled && debugShowGrid;
        bool hasHardEdges => normalMode == NormalMode.HardEdges;
        bool isPerlinNoise => noiseMode == NoiseMode.Perlin;

        public float voxelSizeF => (1.0f / voxelsPerUnit) * ((filter) ? (1 << filterIterations ) : (1));
        public Vector3 voxelSize => Vector3.one * voxelSizeF;


        int NextDivisible(int startNumber, int divisionCount)
        {
            int m = 1 << divisionCount; 
            return ((startNumber + m - 1) / m) * m;
        }

        [Button("Build")]
        public void Build()
        {
            // Create the voxel field
            Bounds bounds = sdf.GetBounds();
            if (isoValue > 0.0f) bounds.Expand(isoValue * 2.2f);
            if (filter)
            {
                // Give some margin to the bounds - 10% margin
                bounds.Expand(bounds.size * boundExtension);
            }

            Vector3Int gs = new Vector3Int(Mathf.CeilToInt(bounds.size.x * voxelsPerUnit) + 1,
                                           Mathf.CeilToInt(bounds.size.y * voxelsPerUnit) + 1,
                                           Mathf.CeilToInt(bounds.size.z * voxelsPerUnit) + 1);
            if (filter)
            {
                gs.x = NextDivisible(gs.x, filterIterations);
                gs.y = NextDivisible(gs.y, filterIterations);
                gs.z = NextDivisible(gs.z, filterIterations);
            }

            voxelData = new VoxelDataFloat();
            voxelData.Init(gs, Vector3.one / voxelsPerUnit);
            voxelData.minBound = bounds.min;

            distanceRange = new Vector2(-1.0f, 1.0f);

            SampleSDF();
            if (filter)
            {
                VoxelDataFloat.VoxelFilterKernel<float> filterKernel = null;
                switch (filterMode)
                {
                    case FilterMode.Box2:
                        filterKernel = VoxelDataFloat.FilterKernel_Box2;
                        break;
                    case FilterMode.Box3:
                        filterKernel = VoxelDataFloat.FilterKernel_Box3;
                        break;
                    case FilterMode.Tent3:
                        filterKernel = VoxelDataFloat.FilterKernel_Tent3;
                        break;
                    case FilterMode.Gaussian5:
                        filterKernel = VoxelDataFloat.FilterKernel_Gaussian5;
                        break;
                    default:
                        break;
                }
                for (int i = 0; i < filterIterations; i++)
                {
                    voxelData.HalfSize(filterKernel, false);
                }
            }
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

        bool computeNormals => (normalMode == NormalMode.Simple) || (normalMode == NormalMode.HardEdges);

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

                        for (int i = 0; i < 8; i++)
                        {
                            p[i] = GetPosWithNoise(x + MCTables.MC_INCS[i, 0], y + MCTables.MC_INCS[i, 1], z + MCTables.MC_INCS[i, 2]);
                            val[i] = voxelData[x + MCTables.MC_INCS[i, 0], y + MCTables.MC_INCS[i, 1], z + MCTables.MC_INCS[i, 2]];
                            if (val[i] < isoValue) cubeIndex |= (1 << i);
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
                            if ((edgeFlags & (1 << i)) != 0) edgeVert[i] = InterpolateVertex(isoValue, p[MCTables.MC_EDGE_INTERPOLATION[i, 0]], p[MCTables.MC_EDGE_INTERPOLATION[i, 1]], val[MCTables.MC_EDGE_INTERPOLATION[i, 0]] + epsBias, val[MCTables.MC_EDGE_INTERPOLATION[i, 1]] + epsBias);

                        }

                        // Emit triangles
                        for (int i = 0; MCTables.MC_TRI_TABLE[cubeIndex, i] != -1; i += 3)
                        {
                            int e0 = MCTables.MC_TRI_TABLE[cubeIndex, i];
                            int e1 = MCTables.MC_TRI_TABLE[cubeIndex, i + 1];
                            int e2 = MCTables.MC_TRI_TABLE[cubeIndex, i + 2];

                            int ia = GetOrCreateEdgeVertex(e0, x, y, z, isoValue, epsBias, p, val, verts, norms, uvs, h);
                            int ib = GetOrCreateEdgeVertex(e1, x, y, z, isoValue, epsBias, p, val, verts, norms, uvs, h);
                            int ic = GetOrCreateEdgeVertex(e2, x, y, z, isoValue, epsBias, p, val, verts, norms, uvs, h);

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
            if (computeNormals) mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            if (simplify)
            {
                var simplificationOptions = UnityMeshSimplifier.SimplificationOptions.Default;
                simplificationOptions.PreserveSurfaceCurvature = preserveSurfaceCurvature;
                simplificationOptions.PreserveBorderEdges = preserveBorderEdges || (normalMode == NormalMode.HardEdges);

                var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
                meshSimplifier.Initialize(mesh);
                meshSimplifier.SimplificationOptions = simplificationOptions;
                meshSimplifier.SimplifyMesh(quality);

                var newMesh = meshSimplifier.ToMesh();
                newMesh.name = mesh.name + "_Simplified";
                if (normalMode == NormalMode.Simple)
                {
                    newMesh.RecalculateNormals();
                    newMesh.RecalculateTangents();
                }
                else if (normalMode == NormalMode.HardEdges)
                {
                    // e.g. 45° crease; make this a serialized field if you want
                    MeshHardEdgeSplitter.SplitVerticesByFaceNormalAngle(newMesh, hardEdgeTolerance);
                }

                mesh = newMesh;
            }

            return mesh;
        }


        private Vector3 GetPos(int x, int y, int z) => new Vector3(voxelData.minBound.x + x * voxelData.voxelSize.x,
                                                                   voxelData.minBound.y + y * voxelData.voxelSize.y,
                                                                   voxelData.minBound.z + z * voxelData.voxelSize.z);
        private Vector3 GetPosWithNoise(int x, int y, int z)
        {
            var p = new Vector3(voxelData.minBound.x + x * voxelData.voxelSize.x,
                                voxelData.minBound.y + y * voxelData.voxelSize.y,
                                voxelData.minBound.z + z * voxelData.voxelSize.z);
            if (noiseMode == NoiseMode.Perlin)
            {
                var delta = Noise.PerlinDirection3d(p * noiseFrequency);
                return p + new Vector3(noiseStrength * perAxisStrength.x * delta.x, noiseStrength * perAxisStrength.y * delta.y, noiseStrength * perAxisStrength.z * delta.z);
            }

            return p;
        }

        public static class MeshHardEdgeSplitter
        {
            /// <summary>
            /// Splits vertices where incident triangle face normals differ by more than angleDeg.
            /// Operates in-place on the given mesh.
            /// </summary>
            public static void SplitVerticesByFaceNormalAngle(Mesh mesh, float angleDeg)
            {
                if (!mesh) return;

                // Pull data
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                var uv0 = mesh.uv;
                var uv2 = mesh.uv2;
                var uv3 = mesh.uv3;
                var uv4 = mesh.uv4;
                var colors = mesh.colors;
                var tangents = mesh.tangents;

                int triCount = tris.Length / 3;
                if (triCount == 0) return;

                // 1) Per-triangle (face) normals
                var faceN = new Vector3[triCount];
                for (int t = 0; t < triCount; t++)
                {
                    int i0 = tris[3 * t + 0];
                    int i1 = tris[3 * t + 1];
                    int i2 = tris[3 * t + 2];
                    Vector3 a = verts[i0];
                    Vector3 b = verts[i1];
                    Vector3 c = verts[i2];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    float m = n.magnitude;
                    faceN[t] = (m > 1e-20f) ? (n / m) : Vector3.up; // fallback
                }

                // 2) Build corner lists per vertex (each corner is a "slot" in tris[])
                var cornersPerVertex = new List<int>[verts.Length];
                for (int c = 0; c < tris.Length; c++)
                {
                    int v = tris[c];
                    (cornersPerVertex[v] ??= new List<int>(4)).Add(c);
                }

                float cosThresh = Mathf.Cos(angleDeg * Mathf.Deg2Rad);

                // Output buffers (start as copies of original)
                var newVerts = new List<Vector3>(verts);
                var newUv0 = (uv0 is { Length: > 0 }) ? new List<Vector2>(uv0) : null;
                var newUv2 = (uv2 is { Length: > 0 }) ? new List<Vector2>(uv2) : null;
                var newUv3 = (uv3 is { Length: > 0 }) ? new List<Vector2>(uv3) : null;
                var newUv4 = (uv4 is { Length: > 0 }) ? new List<Vector2>(uv4) : null;
                var newColors = (colors is { Length: > 0 }) ? new List<Color>(colors) : null;
                var newTangents = (tangents is { Length: > 0 }) ? new List<Vector4>(tangents) : null;

                int splitsDone = 0;

                // 3) For each original vertex, cluster its incident triangle corners by face-normal angle
                for (int v = 0; v < cornersPerVertex.Length; v++)
                {
                    var cornerList = cornersPerVertex[v];
                    if (cornerList == null || cornerList.Count <= 1) continue; // nothing to split

                    int n = cornerList.Count;

                    // Union-Find over corners: connect if angle <= threshold (dot >= cosThresh)
                    int[] parent = new int[n];
                    for (int i = 0; i < n; i++) parent[i] = i;

                    int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
                    void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }

                    for (int i = 0; i < n; i++)
                    {
                        int triI = cornerList[i] / 3;
                        Vector3 Ni = faceN[triI];
                        for (int j = i + 1; j < n; j++)
                        {
                            int triJ = cornerList[j] / 3;
                            Vector3 Nj = faceN[triJ];
                            float d = Vector3.Dot(Ni, Nj);
                            if (d >= cosThresh) Union(i, j);
                        }
                    }

                    // Group corners by root
                    var groups = new Dictionary<int, List<int>>();
                    for (int i = 0; i < n; i++)
                    {
                        int r = Find(i);
                        if (!groups.TryGetValue(r, out var g)) groups[r] = g = new List<int>();
                        g.Add(i);
                    }

                    if (groups.Count <= 1) continue; // still one smooth group, no split

                    // Keep first group on original vertex index v; new groups duplicate v
                    bool first = true;
                    foreach (var kv in groups)
                    {
                        var groupCornerIndices = kv.Value; // list of indices into cornerList

                        if (first) { first = false; continue; }

                        // Duplicate vertex v -> newIdx and copy attributes
                        int newIdx = newVerts.Count;
                        newVerts.Add(verts[v]);
                        if (newUv0 != null) newUv0.Add(uv0[v]);
                        if (newUv2 != null) newUv2.Add(uv2[v]);
                        if (newUv3 != null) newUv3.Add(uv3[v]);
                        if (newUv4 != null) newUv4.Add(uv4[v]);
                        if (newColors != null) newColors.Add(colors[v]);
                        if (newTangents != null) newTangents.Add(tangents[v]);

                        // Rewire the triangle corners in this group to the new vertex index
                        foreach (int localCornerIdx in groupCornerIndices)
                        {
                            int triCornerSlot = cornerList[localCornerIdx]; // index into tris[]
                            tris[triCornerSlot] = newIdx;
                        }

                        splitsDone++;
                    }
                }

                if (splitsDone == 0)
                {
                    // Nothing met the threshold — either geometry is already “smooth” at that angle,
                    // or inputs weren’t wired as expected.
                    return;
                }

                // 4) If vertex count grows > 65k, ensure 32-bit indices are used
                if (newVerts.Count > 65535)
                    mesh.indexFormat = IndexFormat.UInt32;

                // 5) Push data back and recompute derived attributes
                mesh.SetVertices(newVerts);
                if (newUv0 != null) mesh.SetUVs(0, newUv0);
                if (newUv2 != null) mesh.SetUVs(1, newUv2);
                if (newUv3 != null) mesh.SetUVs(2, newUv3);
                if (newUv4 != null) mesh.SetUVs(3, newUv4);
                if (newColors != null) mesh.SetColors(newColors);
                if (newTangents != null) mesh.SetTangents(newTangents);

                mesh.triangles = tris;                 // IMPORTANT: assign back
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();            // will keep hard edges because indices are now split
                mesh.RecalculateTangents();
            }
        }


        private void OnDrawGizmos()
        {
            if (!debugEnabled) return;

            if ((debugShowGrid) && (voxelData != null))
            {
                Vector3Int gs = voxelData.gridSize;

                // 1) Draw grid lines by marching between corner points:

                if (debugShowGridLines)
                {
                    Gizmos.color = Color.gray;

                    // Helper
                    Vector3 P(int xi, int yi, int zi) => GetPosWithNoise(xi, yi, zi);

                    // 1) For every cell, draw the 3 edges from its min corner: +X, +Y, +Z.
                    for (int z = 0; z < gs.z - 1; z++)
                        for (int y = 0; y < gs.y - 1; y++)
                            for (int x = 0; x < gs.x - 1; x++)
                            {
                                var p = P(x, y, z);
                                Gizmos.DrawLine(p, P(x + 1, y, z)); // +X
                                Gizmos.DrawLine(p, P(x, y + 1, z)); // +Y
                                Gizmos.DrawLine(p, P(x, y, z + 1)); // +Z
                            }

                    // 2) Stitch the three "max" faces that have no owning cell:
                    int Xmax = gs.x - 1, Ymax = gs.y - 1, Zmax = gs.z - 1;

                    // Face x = Xmax: draw +Y and +Z edges
                    for (int y = 0; y < gs.y - 1; y++)
                        for (int z = 0; z < gs.z; z++) Gizmos.DrawLine(P(Xmax, y, z), P(Xmax, y + 1, z));
                    for (int y = 0; y < gs.y; y++)
                        for (int z = 0; z < gs.z - 1; z++) Gizmos.DrawLine(P(Xmax, y, z), P(Xmax, y, z + 1));

                    // Face y = Ymax: draw +X and +Z edges
                    for (int x = 0; x < gs.x - 1; x++)
                        for (int z = 0; z < gs.z; z++) Gizmos.DrawLine(P(x, Ymax, z), P(x + 1, Ymax, z));
                    for (int x = 0; x < gs.x; x++)
                        for (int z = 0; z < gs.z - 1; z++) Gizmos.DrawLine(P(x, Ymax, z), P(x, Ymax, z + 1));

                    // Face z = Zmax: draw +X and +Y edges
                    for (int x = 0; x < gs.x - 1; x++)
                        for (int y = 0; y < gs.y; y++) Gizmos.DrawLine(P(x, y, Zmax), P(x + 1, y, Zmax));
                    for (int x = 0; x < gs.x; x++)
                        for (int y = 0; y < gs.y - 1; y++) Gizmos.DrawLine(P(x, y, Zmax), P(x, y + 1, Zmax));
                }
                // 2) Draw a small colored sphere at each corner sample:
                if (debugShowVoxelData)
                {
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

            if (showNormals)
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null)
                {
                    Mesh mesh = mf.sharedMesh;
                    if (mesh != null)
                    {
                        var vertices = mesh.vertices;
                        var normals = mesh.normals;

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            Gizmos.color = Color.green;
                            DebugHelpers.DrawArrow(vertices[i], normals[i], 0.1f, 0.025f, normals[i].Perpendicular());
                        }
                    }
                }
            }
        }
    }
}