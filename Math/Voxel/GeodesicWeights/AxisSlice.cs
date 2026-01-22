using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC
{
    class AxisSlice
    {
        public class VolumeDef
        {
            public Bounds bounds;
            public Vector3Int dims;
            public Vector3 origin;
        }

        struct RenderMesh
        {
            public Mesh mesh;
            public Matrix4x4 transform;
        };

        private float voxelSize = 0.1f;
        private Vector3 direction;
        private VolumeDef volumeDef;

        private List<RenderMesh> renderMeshes;
        private List<RenderTexture> renderTargets;
        private BitVolume _sliceData;
        public BitVolume sliceData => _sliceData;

        public AxisSlice(float voxelSize, Vector3 direction, bool keepRT = false)
        {
            this.voxelSize = voxelSize;
            this.direction = direction;

            renderTargets = (keepRT) ? (new()) : (null);
        }

        public int renderTargetCount => (renderTargets == null) ? 0 : renderTargets.Count;
        public Texture GetRenderTarget(int i)
        {
            return (renderTargets == null) ? null : renderTargets[i];
        }

        public void AddMesh(Mesh mesh, Matrix4x4 transform)
        {
            renderMeshes ??= new();

            var m = new RenderMesh();
            m.mesh = mesh;
            m.transform = transform;
            renderMeshes.Add(m);
        }

        public void AddMesh(Mesh[] mesh, Matrix4x4[] transforms)
        {
            renderMeshes ??= new();

            for (int i = 0; i < mesh.Length; i++) AddMesh(mesh[i], transforms[i]);
        }

        public void AddMesh(List<Mesh> mesh, List<Matrix4x4> transforms)
        {
            renderMeshes ??= new();

            for (int i = 0; i < mesh.Count; i++) AddMesh(mesh[i], transforms[i]);
        }

        public void SetVolume(VolumeDef volumeDef)
        {
            this.volumeDef = volumeDef;
        }

        public void Render()
        {
            Shader shader = Shader.Find("Unity Common/VoxelizeSlicing/VoxelSliceShader");
            Material backfacesWhiteMat = new Material(shader);
            backfacesWhiteMat.SetColor("_Color", Color.white);
            backfacesWhiteMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);

            Material frontfacesBlackMat = new Material(shader);
            frontfacesBlackMat.SetColor("_Color", Color.black);
            frontfacesBlackMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);

            if (volumeDef == null)
            {
                volumeDef = new();
                volumeDef.bounds = MeshExtensions.GetWorldBounds(renderMeshes.Select(r => r.mesh).ToList(), renderMeshes.Select(r => r.transform).ToList());
                volumeDef.origin = volumeDef.bounds.min;

                var size = volumeDef.bounds.size;
                volumeDef.dims = new Vector3Int(Mathf.CeilToInt(size.x / voxelSize), Mathf.CeilToInt(size.y / voxelSize), Mathf.CeilToInt(size.z / voxelSize));
            }

            ComputeDirectionalExtents(volumeDef.bounds, direction, out var r, out var u, out var d, out float xMin, out float xMax, out float yMin, out float yMax, out float zMin, out float zMax);

            float depthLength = zMax - zMin;

            int sliceCount = volumeDef.dims.y;
            if ((direction == Vector3.forward) || (direction == Vector3.back)) sliceCount = volumeDef.dims.z;
            if ((direction == Vector3.right) || (direction == Vector3.left)) sliceCount = volumeDef.dims.x;

            // camera start a bit before first slice
            float margin = voxelSize * 2f;
            float centerZ = Vector3.Dot(volumeDef.bounds.center, d);
            Vector3 camPos = volumeDef.bounds.center + d * ((zMin - margin) - centerZ);
            Vector3 lookAt = camPos + d;

            Matrix4x4 viewMatrix = GraphicsHelper.GetUnityCameraMatrix(camPos, camPos + d, u);

            ComputeOrthoFromView(volumeDef.bounds, viewMatrix, out float left, out float right, out float bottom, out float top);

            float centerDot = Vector3.Dot(volumeDef.bounds.center, d);

            int rtW = Mathf.Max(1, Mathf.CeilToInt((right - left) / voxelSize));
            int rtH = Mathf.Max(1, Mathf.CeilToInt((top - bottom) / voxelSize));

            if ((rtW > 2048) || (rtH > 2048))
            {
                throw new System.Exception("Render target too large!");
            }

            float snappedW = rtW * voxelSize;
            float snappedH = rtH * voxelSize;

            float cx = 0.5f * (left + right);
            float cy = 0.5f * (bottom + top);

            left = cx - snappedW * 0.5f;
            right = cx + snappedW * 0.5f;
            bottom = cy - snappedH * 0.5f;
            top = cy + snappedH * 0.5f;

            RenderTexture rt = null;

            var scratch = new Texture2D(rtW, rtH, TextureFormat.RGBA32, false, true);
            scratch.filterMode = FilterMode.Point;
            scratch.wrapMode = TextureWrapMode.Clamp;

            _sliceData = new BitVolume(volumeDef.dims.x, volumeDef.dims.y, volumeDef.dims.z);

            for (int i = 0; i < sliceCount; i++)
            {
                float sliceStart = zMin + i * voxelSize;
                float sliceEnd = sliceStart + voxelSize;

                // Camera is placed at (zMin - margin) along d, so slab distances are:
                float zNear = margin + i * voxelSize;
                float zFar = (zMax - zMin) + margin + voxelSize * 2f;

                // sanity
                if (zFar <= zNear + 1e-5f) continue;

                var projMatrix = Matrix4x4.Ortho(left, right, bottom, top, zNear, zFar);

                if (rt == null)
                {
                    rt = new RenderTexture(rtW, rtH, 32, RenderTextureFormat.ARGB32);
                    rt.name = "RenderSlice_RT";
                    rt.filterMode = FilterMode.Point;
                    rt.wrapMode = TextureWrapMode.Clamp;
                    rt.useMipMap = false;
                    rt.autoGenerateMips = false;
                    rt.Create();
                }

                GraphicsHelper.QuickDraw(rt, viewMatrix, projMatrix,
                    (cmd) =>
                    {
                        foreach (var m in renderMeshes)
                        {
                            var mesh = m.mesh;
                            var modelMatrix = m.transform;

                            for (int sm = 0; sm < mesh.subMeshCount; sm++)
                                cmd.DrawMesh(mesh, modelMatrix, backfacesWhiteMat, sm, 0);
                        }

                        foreach (var m in renderMeshes)
                        {
                            var mesh = m.mesh;
                            var modelMatrix = m.transform;

                            for (int sm = 0; sm < mesh.subMeshCount; sm++)
                                cmd.DrawMesh(mesh, modelMatrix, frontfacesBlackMat, sm, 0);
                        }
                    },
                    true, Color.black, true, 1);

                ReadSliceIntoBits(rt, scratch, sliceData, direction, i, volumeDef.dims);

                if (renderTargets != null)
                {
                    renderTargets.Add(rt);
                    rt = null; // Force to recreate
                }
            }
        }

        static void ComputeDirectionalExtents(Bounds b, Vector3 direction, out Vector3 r, out Vector3 u, out Vector3 d, out float xMin, out float xMax, out float yMin, out float yMax, out float zMin, out float zMax)
        {
            d = direction.normalized;

            // Pick a non-parallel up candidate
            Vector3 upCand = (Mathf.Abs(Vector3.Dot(d, Vector3.up)) > 0.99f) ? Vector3.forward : Vector3.up;

            r = Vector3.Normalize(Vector3.Cross(upCand, d));
            u = Vector3.Cross(d, r); // already normalized if r and d are

            Vector3 c = b.center;
            Vector3 e = b.extents;

            Vector3[] corners =
            {
                c + new Vector3(-e.x,-e.y,-e.z),
                c + new Vector3(-e.x,-e.y, e.z),
                c + new Vector3(-e.x, e.y,-e.z),
                c + new Vector3(-e.x, e.y, e.z),
                c + new Vector3( e.x,-e.y,-e.z),
                c + new Vector3( e.x,-e.y, e.z),
                c + new Vector3( e.x, e.y,-e.z),
                c + new Vector3( e.x, e.y, e.z),
            };

            xMin = yMin = zMin = float.PositiveInfinity;
            xMax = yMax = zMax = float.NegativeInfinity;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 p = corners[i];

                float x = Vector3.Dot(p, r);
                float y = Vector3.Dot(p, u);
                float z = Vector3.Dot(p, d);

                if (x < xMin) xMin = x; if (x > xMax) xMax = x;
                if (y < yMin) yMin = y; if (y > yMax) yMax = y;
                if (z < zMin) zMin = z; if (z > zMax) zMax = z;
            }
        }

        static void ComputeOrthoFromView(Bounds b, Matrix4x4 view, out float left, out float right, out float bottom, out float top)
        {
            Vector3 c = b.center;
            Vector3 e = b.extents;

            left = bottom = float.PositiveInfinity;
            right = top = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < 8; i++)
            {
                Vector3 v = view.MultiplyPoint(b.GetCorner(i));

                if (v.x < left) left = v.x;
                if (v.x > right) right = v.x;
                if (v.y < bottom) bottom = v.y;
                if (v.y > top) top = v.y;

                // View space forward is -Z, so distances are -v.z
                float dz = -v.z;
                if (dz < minZ) minZ = dz;
                if (dz > maxZ) maxZ = dz;
            }
        }

        static (Vector3Int origin, Vector3Int deltaX, Vector3Int deltaY, Vector3Int deltaZ) GetMapping(Vector3 direction, Vector3Int dims)
        {
            if (direction == Vector3.forward)
                return (new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, 0, 1));
            else if (direction == Vector3.back)
                return (new Vector3Int(dims.x - 1, 0, dims.z - 1), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, 0, -1));
            else if (direction == Vector3.right)
                return (new Vector3Int(0, 0, dims.z - 1), new Vector3Int(0, 0, -1), new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0));
            else if (direction == Vector3.left)
                return (new Vector3Int(dims.x - 1, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 1, 0), new Vector3Int(-1, 0, 0));
            else if (direction == Vector3.up)
                return (new Vector3Int(dims.x - 1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 1, 0));
            else if (direction == Vector3.down)
                return (new Vector3Int(0, dims.y - 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, -1, 0));

            throw new Exception($"No mapping available - non canonical axis used ({direction})");
        }

        static void ReadSliceIntoBits(RenderTexture rt, Texture2D scratch, BitVolume bits, Vector3 dir, int sliceIndex, Vector3Int dims, float threshold = 0.5f)
        {
            // Ensure scratch matches rt
            if ((scratch.width != rt.width) || (scratch.height != rt.height))
                throw new Exception("Scratch Texture2D size mismatch. Recreate it when RT size changes.");

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            scratch.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            scratch.Apply(false, false);

            var mapping = GetMapping(dir, dims);

            RenderTexture.active = prev;

            var raw = scratch.GetRawTextureData<Color32>();
            int w = rt.width;
            int h = rt.height;

            int nx = dims.x;
            int ny = dims.y;
            int nz = dims.z;

            for (int py = 0; py < h; py++)
            {
                int row = py * w;
                for (int px = 0; px < w; px++)
                {
                    byte r = raw[row + px].r;
                    bool inside = r >= (byte)(threshold * 255.0f);
                    if (!inside) continue;

                    var p = mapping.origin + mapping.deltaX * px + mapping.deltaY * py + mapping.deltaZ * sliceIndex;

                    /*if ((p.x < 0) || (p.x >= dims.x) ||
                        (p.y < 0) || (p.y >= dims.y) ||
                        (p.z < 0) || (p.z >= dims.z))
                    {
                        throw new Exception("Out of bounds on bit volume!");
                    }//*/

                    bits.Set(p, inside);

                    /*MapPixelToCanonical(dir, px, py, sliceIndex, nx, ny, nz, out int x, out int y, out int z);

                    // Safety (until you lock mapping)
                    if (((uint)x < (uint)nx) && ((uint)y < (uint)ny) && ((uint)z < (uint)nz))
                        bits.Set(x, y, z, inside);
                    */
                }
            }
        }
    }
}