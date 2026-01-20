using System;
using System.Collections.Generic;
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
            Shader shader = Shader.Find("Hidden/VoxelizeSlicing/VoxelSliceShader");
            Material backfacesWhiteMat = new Material(shader);
            backfacesWhiteMat.SetColor("_Color", Color.white);
            backfacesWhiteMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);

            Material frontfacesBlackMat = new Material(shader);
            frontfacesBlackMat.SetColor("_Color", Color.red);
            frontfacesBlackMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);

            if (volumeDef == null)
            {
                volumeDef = new();
                volumeDef.bounds = GetWorldBounds();
                volumeDef.origin = volumeDef.bounds.min;

                Vector3 voxelCount = volumeDef.bounds.size / voxelSize;
                volumeDef.dims = new Vector3Int();
                volumeDef.dims.x = Mathf.CeilToInt(voxelCount.x);
                volumeDef.dims.y = Mathf.CeilToInt(voxelCount.y);
                volumeDef.dims.z = Mathf.CeilToInt(voxelCount.z);
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

            /*Matrix4x4 viewMatrix = new Matrix4x4();

            viewMatrix.SetRow(0, new Vector4(r.x, r.y, r.z, -Vector3.Dot(r, camPos)));
            viewMatrix.SetRow(1, new Vector4(u.x, u.y, u.z, -Vector3.Dot(u, camPos)));
            viewMatrix.SetRow(2, new Vector4(-d.x, -d.y, -d.z, Vector3.Dot(d, camPos)));
            viewMatrix.SetRow(3, new Vector4(0, 0, 0, 1));*/

            Matrix4x4 viewMatrix  = Matrix4x4.LookAt(camPos, camPos + d, u);
            var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
            viewMatrix = scaleMatrix * viewMatrix.inverse;

            ComputeOrthoFromView(volumeDef.bounds, viewMatrix, out float left, out float right, out float bottom, out float top, out float nearAll, out float farAll);

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

                Vector3 pStart = volumeDef.bounds.center + d * (sliceStart - centerDot);
                Vector3 pEnd = volumeDef.bounds.center + d * (sliceEnd - centerDot);

                float zNear = -viewMatrix.MultiplyPoint(pStart).z;
                float zFar = -viewMatrix.MultiplyPoint(pEnd).z;
                if (zNear > zFar) (zNear, zFar) = (zFar, zNear);

                zNear = Mathf.Max(zNear, nearAll);
                zFar = farAll;
                if (zFar <= zNear + 1e-5f) continue;
                float eps = voxelSize * 1e-3f;
                zFar -= eps;
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

        public Bounds GetWorldBounds()
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            for (int i = 0; i < renderMeshes.Count; i++)
            {
                var mesh = renderMeshes[i].mesh;
                var b = mesh.bounds.ToWorld(renderMeshes[i].transform);

                // Convert bounds to be relative to this one
                if (first)
                {
                    bounds = b;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return bounds;
        }

        static void MapPixelToCanonical(Vector3 dir, int px, int py, int slice, int nx, int ny, int nz, out int x, out int y, out int z)
        {
            // Default
            x = y = z = 0;

            if (dir == Vector3.forward) // +Z
            {
                x = px;
                y = py;
                z = slice;
            }
            else if (dir == Vector3.back) // -Z
            {
                x = px;
                y = py;
                z = (nz - 1) - slice;
            }
            else if (dir == Vector3.right) // +X
            {
                x = slice;
                y = py;
                z = px;
            }
            else if (dir == Vector3.left) // -X
            {
                x = (nx - 1) - slice;
                y = py;
                z = px;
            }
            else if (dir == Vector3.up) // +Y
            {
                x = px;
                y = slice;
                z = py;
            }
            else if (dir == Vector3.down) // -Y
            {
                x = px;
                y = (ny - 1) - slice;
                z = py;
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

        static void ComputeOrthoFromView(Bounds b, Matrix4x4 view, out float left, out float right, out float bottom, out float top, out float near, out float far)
        {
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

            left = bottom = float.PositiveInfinity;
            right = top = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 v = view.MultiplyPoint(corners[i]);

                if (v.x < left) left = v.x;
                if (v.x > right) right = v.x;
                if (v.y < bottom) bottom = v.y;
                if (v.y > top) top = v.y;

                // View space forward is -Z, so distances are -v.z
                float dz = -v.z;
                if (dz < minZ) minZ = dz;
                if (dz > maxZ) maxZ = dz;
            }

            near = Mathf.Max(0.01f, minZ);
            far = Mathf.Max(near + 0.01f, maxZ);
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

                    MapPixelToCanonical(dir, px, py, sliceIndex, nx, ny, nz, out int x, out int y, out int z);

                    // Safety (until you lock mapping)
                    if (((uint)x < (uint)nx) && ((uint)y < (uint)ny) && ((uint)z < (uint)nz))
                        bits.Set(x, y, z, inside);
                }
            }
        }
    }
}