using System;
using UnityEngine;

namespace UC
{

    [System.Serializable]
    public class VoxelData<DATA_TYPE> where DATA_TYPE : struct, IEquatable<DATA_TYPE>
    {
        public delegate T VoxelFilterKernel<T>(Func<int, int, int, T> S, int sx, int sy, int sz);

        [HideInInspector]
        public DATA_TYPE[] data;
        public Vector3Int gridSize;
        public Vector3 voxelSize;
        public Vector3 minBound;
        public Vector3 uvScale;

        public Vector3 size => new Vector3(gridSize.x * voxelSize.x, gridSize.y * voxelSize.y, gridSize.z * voxelSize.z);
        public Vector3 extents => size * 0.5f;
        public Bounds bounds
        {
            get
            {
                var s = size;
                return new Bounds(minBound + s * 0.5f, s);
            }
        }

        public int IndexOf(int x, int y, int z) => x + y * gridSize.x + z * gridSize.x * gridSize.y;

        public DATA_TYPE this[int x, int y, int z]
        {
            get { return data[IndexOf(x, y, z)]; }
            set { data[IndexOf(x, y, z)] = value; }
        }


        public DATA_TYPE this[Vector3Int p]
        {
            get { return data[IndexOf(p.x, p.y, p.z)]; }
            set { data[IndexOf(p.x, p.y, p.z)] = value; }
        }

        public void Replace(DATA_TYPE src, DATA_TYPE dest)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].Equals(src)) data[i] = dest;
            }
        }

        public void Init(Vector3Int gridSize, Vector3 voxelSize)
        {
            data = new DATA_TYPE[gridSize.x * gridSize.y * gridSize.z];
            this.voxelSize = voxelSize;
            this.gridSize = gridSize;
        }

        public void HalfSize(VoxelFilterKernel<DATA_TYPE> kernel, bool copyEdges = true)
        {
            // Reduce the size of the voxel data by two (if possible), using the given kernel
            var gs = gridSize;
            if (gs.x <= 1 && gs.y <= 1 && gs.z <= 1) return;

            Vector3Int newGS = new Vector3Int((gs.x + 1) >> 1, (gs.y + 1) >> 1, (gs.z + 1) >> 1);
            var dst = new DATA_TYPE[newGS.x * newGS.y * newGS.z];

            // Clamped accessor for the kernel (so wide kernels can safely sample near borders)
            DATA_TYPE S(int ix, int iy, int iz)
            {
                if (ix < 0) ix = 0;
                else if (ix >= gs.x) ix = gs.x - 1;

                if (iy < 0) iy = 0;
                else if (iy >= gs.y) iy = gs.y - 1;

                if (iz < 0) iz = 0;
                else if (iz >= gs.z) iz = gs.z - 1;

                return this[ix, iy, iz];
            }

            bool CanFilter(int sx, int sy, int sz) =>
                (sx + 1 < gs.x) && (sy + 1 < gs.y) && (sz + 1 < gs.z);

            int DstIndex(int x, int y, int z) => x + y * newGS.x + z * newGS.x * newGS.y;

            for (int z = 0; z < newGS.z; z++)
            {
                int sz = z * 2;
                for (int y = 0; y < newGS.y; y++)
                {
                    int sy = y * 2;
                    for (int x = 0; x < newGS.x; x++)
                    {
                        int sx = x * 2;
                        int di = DstIndex(x, y, z);

                        if (CanFilter(sx, sy, sz))
                        {
                            dst[di] = kernel(S, sx, sy, sz);
                        }
                        else
                        {
                            // Keep your edge policy (exact copy). If you want filtered edges,
                            // pass copyEdges=false and it’ll still be safe because of clamping.
                            dst[di] = copyEdges ? this[sx, sy, sz] : kernel(S, sx, sy, sz);
                        }
                    }
                }
            }

            data = dst;
            gridSize = newGS;
            voxelSize *= 2.0f;
        }
    }

    public class VoxelDataFloat : VoxelData<float>
    {
        // 2x2x2 box filter (average of the 8-corner block), centered at (sx,sy,sz) with offsets {0,1}.
        public static float FilterKernel_Box2(Func<int, int, int, float> S, int sx, int sy, int sz)
        {
            float s000 = S(sx, sy, sz);
            float s100 = S(sx + 1, sy, sz);
            float s010 = S(sx, sy + 1, sz);
            float s110 = S(sx + 1, sy + 1, sz);
            float s001 = S(sx, sy, sz + 1);
            float s101 = S(sx + 1, sy, sz + 1);
            float s011 = S(sx, sy + 1, sz + 1);
            float s111 = S(sx + 1, sy + 1, sz + 1);
            return (s000 + s100 + s010 + s110 + s001 + s101 + s011 + s111) * (1f / 8f);
        }

        // 3x3x3 box filter (uniform average over a 3-cubed neighborhood), centered at (sx,sy,sz) with offsets {-1,0,+1}.
        public static float FilterKernel_Box3(Func<int, int, int, float> S, int sx, int sy, int sz)
        {
            float sum = 0f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        sum += S(sx + dx, sy + dy, sz + dz);
            return sum / 27f;
        }

        // 5x5x5 separable Gaussian-like binomial kernel with weights [1,4,6,4,1] per axis.
        public static float FilterKernel_Gaussian5(Func<int, int, int, float> S, int sx, int sy, int sz)
        {
            // Binomial coefficients (Pascal row 4): [1,4,6,4,1], sum per axis = 16, 3D sum = 16^3 = 4096
            // Offsets -2..+2
            ReadOnlySpan<int> w = stackalloc int[5] { 1, 4, 6, 4, 1 };

            double sum = 0.0;
            for (int kz = 0; kz < 5; kz++)
            {
                int wz = w[kz];
                int z = sz + (kz - 2);
                for (int ky = 0; ky < 5; ky++)
                {
                    int wy = w[ky];
                    int y = sy + (ky - 2);
                    int wzy = wz * wy;
                    for (int kx = 0; kx < 5; kx++)
                    {
                        int wx = w[kx];
                        int x = sx + (kx - 2);
                        sum += wzy * wx * S(x, y, z);
                    }
                }
            }
            return (float)(sum / 4096.0);
        }

        // 3x3x3 separable tent (binomial) kernel with weights [1,2,1] per axis.
        public static float FilterKernel_Tent3(Func<int, int, int, float> S, int sx, int sy, int sz)
        {
            // Offsets -1..+1; per-axis weights [1,2,1]
            ReadOnlySpan<int> w = stackalloc int[3] { 1, 2, 1 };

            float sum = 0f;
            for (int dz = -1; dz <= 1; dz++)
            {
                int wz = w[dz + 1];
                int z = sz + dz;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int wy = w[dy + 1];
                    int y = sy + dy;
                    int wzy = wz * wy;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int wx = w[dx + 1];
                        int x = sx + dx;
                        sum += (wzy * wx) * S(x, y, z);
                    }
                }
            }
            return sum / 8f;
        }
    }
}