using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public interface IOccupancyState
    {
        bool IsOccupied();
        bool IsEmpty();

        void Occupy();
        void ClearOccupancy();
    }

    public static class VoxelizerIntersectionCPU
    {
        static public void Voxelize(VoxelData<byte> voxelData, List<Mesh> meshes, List<Matrix4x4> transforms, float voxelSize, bool forcePowerOfTwo = false, float gridScale = 1.0f, int gridPad = 0, bool fillEmpty = false)
        {
            Bounds bounds = meshes[0].bounds;
            bounds = bounds.ToWorld(transforms[0]);

            for (int i = 1; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                var t = transforms[i];

                var b = mesh.bounds.ToWorld(t);
                bounds.Encapsulate(b);
            }

            float density = 1.0f / voxelSize;

            bounds.Expand(bounds.size * gridScale - bounds.size);
            if (gridPad > 0)
            {
                bounds.Expand(new Vector3(gridPad * voxelSize, gridPad * voxelSize, gridPad * voxelSize));
            }

            Vector3Int gridSize = new Vector3Int(Mathf.CeilToInt(bounds.size.x * density),
                                                 Mathf.CeilToInt(bounds.size.y * density),
                                                 Mathf.CeilToInt(bounds.size.z * density));
            if (forcePowerOfTwo)
            {
                var log2 = Mathf.Log(2.0f);
                gridSize.x = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.x) / log2));
                gridSize.y = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.y) / log2));
                gridSize.z = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.z) / log2));
            }
            else
            {
                gridSize.x = (gridSize.x % 2 == 0) ? (gridSize.x) : (gridSize.x + 1);
                gridSize.y = (gridSize.y % 2 == 0) ? (gridSize.y) : (gridSize.y + 1);
                gridSize.z = (gridSize.z % 2 == 0) ? (gridSize.z) : (gridSize.z + 1);
            }

            voxelData.gridSize = gridSize;
            voxelData.voxelSize = new Vector3(bounds.size.x / (float)gridSize.x,
                                              bounds.size.y / (float)gridSize.y,
                                              bounds.size.z / (float)gridSize.z);
            voxelData.minBound = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            voxelData.data = new byte[gridSize.x * gridSize.y * gridSize.z];

            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                var t = transforms[i];

                Vector3 densityV = new Vector3(gridSize.x / bounds.size.x, gridSize.y / bounds.size.y, gridSize.z / bounds.size.z);

                // Brute force approach, for all triangles see if any of the voxels is overlapping
                // Only concession is that we don't check for voxels that are already filled in
                var vertices = mesh.vertices;
                for (int j = 0; j < vertices.Length; j++)
                {
                    vertices[j] = t.MultiplyPoint3x4(vertices[j]);
                }

                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    var indices = mesh.GetTriangles(submesh);
                    for (var index = 0; index < indices.Length; index += 3)
                    {
                        var v1 = vertices[indices[index]];
                        var v2 = vertices[indices[index + 1]];
                        var v3 = vertices[indices[index + 2]];

                        var min_x = Mathf.FloorToInt((Mathf.Min(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                        var min_y = Mathf.FloorToInt((Mathf.Min(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                        var min_z = Mathf.FloorToInt((Mathf.Min(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);
                        var max_x = Mathf.CeilToInt((Mathf.Max(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                        var max_y = Mathf.CeilToInt((Mathf.Max(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                        var max_z = Mathf.CeilToInt((Mathf.Max(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);

                        var offset = Vector3.zero;

                        for (int z = Mathf.Max(0, min_z); z <= Mathf.Min(max_z, gridSize.z - 1); z++)
                        {
                            offset.z = voxelData.minBound.z + z * voxelData.voxelSize.z;
                            for (int y = Mathf.Max(0, min_y); y <= Mathf.Min(max_y, gridSize.y - 1); y++)
                            {
                                offset.y = voxelData.minBound.y + y * voxelData.voxelSize.y;
                                for (int x = Mathf.Max(0, min_x); x <= Mathf.Min(max_x, gridSize.x - 1); x++)
                                {
                                    int voxelIndex = x + (y * gridSize.x) + (z * gridSize.x * gridSize.y);
                                    if (voxelData.data[voxelIndex] == 0)
                                    {
                                        // Check this voxel
                                        offset.x = voxelData.minBound.x + x * voxelData.voxelSize.x;
                                        Vector3 aabb_min = offset;
                                        Vector3 aabb_max = aabb_min + voxelData.voxelSize;

                                        if (CheapPlaneReject(aabb_min, aabb_max, v1, v2, v3)) continue;
                                        if (AABB.Intersects(aabb_min, aabb_max, v1, v2, v3))
                                        {
                                            voxelData.data[voxelIndex] = 1;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (fillEmpty)
            {
                FindAndFillEmpty(voxelData);
            }
        }

        static public void Voxelize<T>(VoxelData<T> voxelData, List<Mesh> meshes, List<Matrix4x4> transforms, float voxelSize, bool forcePowerOfTwo = false, float gridScale = 1.0f, int gridPad = 0, bool fillEmpty = false)
            where T : struct, IEquatable<T>, IOccupancyState
        {
            Bounds bounds = meshes[0].bounds;
            bounds = bounds.ToWorld(transforms[0]);

            for (int i = 1; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                var t = transforms[i];

                var b = mesh.bounds.ToWorld(t);
                bounds.Encapsulate(b);
            }

            float density = 1.0f / voxelSize;

            bounds.Expand(bounds.size * gridScale - bounds.size);
            if (gridPad > 0)
            {
                bounds.Expand(new Vector3(gridPad * voxelSize, gridPad * voxelSize, gridPad * voxelSize));
            }

            Vector3Int gridSize = new Vector3Int(Mathf.CeilToInt(bounds.size.x * density),
                                                 Mathf.CeilToInt(bounds.size.y * density),
                                                 Mathf.CeilToInt(bounds.size.z * density));
            if (forcePowerOfTwo)
            {
                var log2 = Mathf.Log(2.0f);
                gridSize.x = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.x) / log2));
                gridSize.y = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.y) / log2));
                gridSize.z = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.z) / log2));
            }
            else
            {
                gridSize.x = (gridSize.x % 2 == 0) ? (gridSize.x) : (gridSize.x + 1);
                gridSize.y = (gridSize.y % 2 == 0) ? (gridSize.y) : (gridSize.y + 1);
                gridSize.z = (gridSize.z % 2 == 0) ? (gridSize.z) : (gridSize.z + 1);
            }

            voxelData.gridSize = gridSize;
            voxelData.voxelSize = new Vector3(bounds.size.x / (float)gridSize.x,
                                              bounds.size.y / (float)gridSize.y,
                                              bounds.size.z / (float)gridSize.z);
            voxelData.minBound = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            voxelData.data = new T[gridSize.x * gridSize.y * gridSize.z];

            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh = meshes[i];
                var t = transforms[i];

                Vector3 densityV = new Vector3(gridSize.x / bounds.size.x, gridSize.y / bounds.size.y, gridSize.z / bounds.size.z);

                // Brute force approach, for all triangles see if any of the voxels is overlapping
                // Only concession is that we don't check for voxels that are already filled in
                var vertices = mesh.vertices;
                for (int j = 0; j < vertices.Length; j++)
                {
                    vertices[j] = t.MultiplyPoint3x4(vertices[j]);
                }

                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    var indices = mesh.GetTriangles(submesh);
                    for (var index = 0; index < indices.Length; index += 3)
                    {
                        var v1 = vertices[indices[index]];
                        var v2 = vertices[indices[index + 1]];
                        var v3 = vertices[indices[index + 2]];

                        var min_x = Mathf.FloorToInt((Mathf.Min(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                        var min_y = Mathf.FloorToInt((Mathf.Min(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                        var min_z = Mathf.FloorToInt((Mathf.Min(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);
                        var max_x = Mathf.CeilToInt((Mathf.Max(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                        var max_y = Mathf.CeilToInt((Mathf.Max(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                        var max_z = Mathf.CeilToInt((Mathf.Max(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);

                        var offset = Vector3.zero;

                        for (int z = Mathf.Max(0, min_z); z <= Mathf.Min(max_z, gridSize.z - 1); z++)
                        {
                            offset.z = voxelData.minBound.z + z * voxelData.voxelSize.z;
                            for (int y = Mathf.Max(0, min_y); y <= Mathf.Min(max_y, gridSize.y - 1); y++)
                            {
                                offset.y = voxelData.minBound.y + y * voxelData.voxelSize.y;
                                for (int x = Mathf.Max(0, min_x); x <= Mathf.Min(max_x, gridSize.x - 1); x++)
                                {
                                    int voxelIndex = x + (y * gridSize.x) + (z * gridSize.x * gridSize.y);
                                    if (voxelData.data[voxelIndex].IsEmpty())
                                    {
                                        // Check this voxel
                                        offset.x = voxelData.minBound.x + x * voxelData.voxelSize.x;
                                        Vector3 aabb_min = offset;
                                        Vector3 aabb_max = aabb_min + voxelData.voxelSize;

                                        if (CheapPlaneReject(aabb_min, aabb_max, v1, v2, v3)) continue;
                                        if (AABB.Intersects(aabb_min, aabb_max, v1, v2, v3))
                                        {
                                            voxelData.data[voxelIndex].Occupy();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (fillEmpty)
            {
                FindAndFillEmpty(voxelData);
            }
        }

        static public void Voxelize(VoxelData<byte> voxelData, Mesh mesh, float density, bool forcePowerOfTwo = false, float triangleScale = 1.0f, float gridScale = 1.0f, bool fillEmpty = false)
        {
            Bounds bounds = mesh.bounds;
            bounds.Expand(bounds.size * gridScale - bounds.size);

            Vector3Int gridSize = new Vector3Int(Mathf.CeilToInt(bounds.size.x * density),
                                                 Mathf.CeilToInt(bounds.size.y * density),
                                                 Mathf.CeilToInt(bounds.size.z * density));
            if (forcePowerOfTwo)
            {
                var log2 = Mathf.Log(2.0f);
                gridSize.x = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.x) / log2));
                gridSize.y = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.y) / log2));
                gridSize.z = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.z) / log2));
            }
            else
            {
                gridSize.x = (gridSize.x % 2 == 0) ? (gridSize.x) : (gridSize.x + 1);
                gridSize.y = (gridSize.y % 2 == 0) ? (gridSize.y) : (gridSize.y + 1);
                gridSize.z = (gridSize.z % 2 == 0) ? (gridSize.z) : (gridSize.z + 1);
            }

            voxelData.gridSize = gridSize;
            voxelData.voxelSize = new Vector3(bounds.size.x / (float)gridSize.x,
                                        bounds.size.y / (float)gridSize.y,
                                        bounds.size.z / (float)gridSize.z);
            voxelData.minBound = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            voxelData.data = new byte[gridSize.x * gridSize.y * gridSize.z];

            Vector3 densityV = new Vector3(gridSize.x / bounds.size.x, gridSize.y / bounds.size.y, gridSize.z / bounds.size.z);

            // Brute force approach, for all triangles see if any of the voxels is overlapping
            // Only concession is that we don't check for voxels that are already filled in
            var vertices = mesh.vertices;
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetTriangles(submesh);
                for (var index = 0; index < indices.Length; index += 3)
                {
                    var v1 = vertices[indices[index]];
                    var v2 = vertices[indices[index + 1]];
                    var v3 = vertices[indices[index + 2]];
                    // Scale triangle relative to center
                    if (triangleScale != 1.0f)
                    {
                        var center = (v1 + v2 + v3) / 3.0f;
                        v1 = ((v1 - center) * triangleScale) + center;
                        v2 = ((v2 - center) * triangleScale) + center;
                        v3 = ((v3 - center) * triangleScale) + center;
                    }

                    var min_x = Mathf.FloorToInt((Mathf.Min(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                    var min_y = Mathf.FloorToInt((Mathf.Min(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                    var min_z = Mathf.FloorToInt((Mathf.Min(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);
                    var max_x = Mathf.CeilToInt((Mathf.Max(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                    var max_y = Mathf.CeilToInt((Mathf.Max(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                    var max_z = Mathf.CeilToInt((Mathf.Max(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);

                    // DEBUG CODE: Just cover everything
                    /*min_x = min_y = min_z = 0;
                    max_x = gridSize.x; max_y = gridSize.y; max_z = gridSize.z; //*/

                    var offset = Vector3.zero;

                    for (int z = Mathf.Max(0, min_z); z <= Mathf.Min(max_z, gridSize.z - 1); z++)
                    {
                        offset.z = voxelData.minBound.z + z * voxelData.voxelSize.z;
                        for (int y = Mathf.Max(0, min_y); y <= Mathf.Min(max_y, gridSize.y - 1); y++)
                        {
                            offset.y = voxelData.minBound.y + y * voxelData.voxelSize.y;
                            for (int x = Mathf.Max(0, min_x); x <= Mathf.Min(max_x, gridSize.x - 1); x++)
                            {
                                int voxelIndex = x + (y * gridSize.x) + (z * gridSize.x * gridSize.y);
                                if (voxelData.data[voxelIndex] == 0)
                                {
                                    // Check this voxel
                                    offset.x = voxelData.minBound.x + x * voxelData.voxelSize.x;
                                    Vector3 aabb_min = offset;
                                    Vector3 aabb_max = aabb_min + voxelData.voxelSize;

                                    if (AABB.Intersects(aabb_min, aabb_max, v1, v2, v3))
                                    {
                                        voxelData.data[voxelIndex] = 1;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (fillEmpty)
            {
                FindAndFillEmpty(voxelData);
            }
        }

        static public void Voxelize<T>(VoxelData<T> voxelData, Mesh mesh, float density, bool forcePowerOfTwo = false, float triangleScale = 1.0f, float gridScale = 1.0f, bool fillEmpty = false) 
            where T : struct, IEquatable<T>, IOccupancyState
        {
            Bounds bounds = mesh.bounds;
            bounds.Expand(bounds.size * gridScale - bounds.size);

            Vector3Int gridSize = new Vector3Int(Mathf.CeilToInt(bounds.size.x * density),
                                                 Mathf.CeilToInt(bounds.size.y * density),
                                                 Mathf.CeilToInt(bounds.size.z * density));
            if (forcePowerOfTwo)
            {
                var log2 = Mathf.Log(2.0f);
                gridSize.x = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.x) / log2));
                gridSize.y = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.y) / log2));
                gridSize.z = (int)Mathf.Pow(2, Mathf.Ceil(Mathf.Log(gridSize.z) / log2));
            }
            else
            {
                gridSize.x = (gridSize.x % 2 == 0) ? (gridSize.x) : (gridSize.x + 1);
                gridSize.y = (gridSize.y % 2 == 0) ? (gridSize.y) : (gridSize.y + 1);
                gridSize.z = (gridSize.z % 2 == 0) ? (gridSize.z) : (gridSize.z + 1);
            }

            voxelData.gridSize = gridSize;
            voxelData.voxelSize = new Vector3(bounds.size.x / (float)gridSize.x,
                                        bounds.size.y / (float)gridSize.y,
                                        bounds.size.z / (float)gridSize.z);
            voxelData.minBound = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            voxelData.data = new T[gridSize.x * gridSize.y * gridSize.z];

            Vector3 densityV = new Vector3(gridSize.x / bounds.size.x, gridSize.y / bounds.size.y, gridSize.z / bounds.size.z);

            // Brute force approach, for all triangles see if any of the voxels is overlapping
            // Only concession is that we don't check for voxels that are already filled in
            var vertices = mesh.vertices;
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetTriangles(submesh);
                for (var index = 0; index < indices.Length; index += 3)
                {
                    var v1 = vertices[indices[index]];
                    var v2 = vertices[indices[index + 1]];
                    var v3 = vertices[indices[index + 2]];
                    // Scale triangle relative to center
                    if (triangleScale != 1.0f)
                    {
                        var center = (v1 + v2 + v3) / 3.0f;
                        v1 = ((v1 - center) * triangleScale) + center;
                        v2 = ((v2 - center) * triangleScale) + center;
                        v3 = ((v3 - center) * triangleScale) + center;
                    }

                    var min_x = Mathf.FloorToInt((Mathf.Min(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                    var min_y = Mathf.FloorToInt((Mathf.Min(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                    var min_z = Mathf.FloorToInt((Mathf.Min(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);
                    var max_x = Mathf.CeilToInt((Mathf.Max(v1.x, v2.x, v3.x) - voxelData.minBound.x) * densityV.x);
                    var max_y = Mathf.CeilToInt((Mathf.Max(v1.y, v2.y, v3.y) - voxelData.minBound.y) * densityV.y);
                    var max_z = Mathf.CeilToInt((Mathf.Max(v1.z, v2.z, v3.z) - voxelData.minBound.z) * densityV.z);

                    // DEBUG CODE: Just cover everything
                    /*min_x = min_y = min_z = 0;
                    max_x = gridSize.x; max_y = gridSize.y; max_z = gridSize.z; //*/

                    var offset = Vector3.zero;

                    for (int z = Mathf.Max(0, min_z); z <= Mathf.Min(max_z, gridSize.z - 1); z++)
                    {
                        offset.z = voxelData.minBound.z + z * voxelData.voxelSize.z;
                        for (int y = Mathf.Max(0, min_y); y <= Mathf.Min(max_y, gridSize.y - 1); y++)
                        {
                            offset.y = voxelData.minBound.y + y * voxelData.voxelSize.y;
                            for (int x = Mathf.Max(0, min_x); x <= Mathf.Min(max_x, gridSize.x - 1); x++)
                            {
                                int voxelIndex = x + (y * gridSize.x) + (z * gridSize.x * gridSize.y);
                                ref T voxel = ref voxelData.data[voxelIndex];
                                if (voxel.IsEmpty())
                                {
                                    // Check this voxel
                                    offset.x = voxelData.minBound.x + x * voxelData.voxelSize.x;
                                    Vector3 aabb_min = offset;
                                    Vector3 aabb_max = aabb_min + voxelData.voxelSize;

                                    if (AABB.Intersects(aabb_min, aabb_max, v1, v2, v3))
                                    {
                                        voxel.Occupy();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (fillEmpty)
            {
                FindAndFillEmpty(voxelData);
            }
        }

        private static void FindAndFillEmpty(VoxelData<byte> vd)
        {
            byte GetAdjacent(VoxelData<byte> vd, int x, int y, int z)
            {
                byte v;
                v = vd[x - 1, y, z]; if (v != 0) return v;
                v = vd[x + 1, y, z]; if (v != 0) return v;
                v = vd[x, y - 1, z]; if (v != 0) return v;
                v = vd[x, y + 1, z]; if (v != 0) return v;
                v = vd[x, y, z - 1]; if (v != 0) return v;
                v = vd[x, y, z + 1]; if (v != 0) return v;

                return 0;
            }

            bool FloodFillAndCheckBounds(VoxelData<byte> vd, int sx, int sy, int sz, byte v)
            {
                Stack<Vector3Int> stack = new Stack<Vector3Int>();
                int lx = vd.gridSize.x - 1;
                int ly = vd.gridSize.y - 1;
                int lz = vd.gridSize.z - 1;

                bool AddNeighbour(int x, int y, int z)
                {
                    if ((x >= 0) && (y >= 0) && (z >= 0) &&
                        (x <= lx) && (y <= ly) && (z <= lz))
                    {
                        if (vd[x, y, z] == 0)
                        {
                            stack.Push(new Vector3Int(x, y, z));
                            return !((x == 0) || (y == 0) || (z == 0) || (x == lx) || (y == ly) || (z == lz));
                        }
                    }

                    return true;
                }

                stack.Push(new Vector3Int(sx, sy, sz));
                bool ret = true;

                while (stack.Count > 0)
                {
                    var current = stack.Pop();

                    vd[current] = v;

                    ret &= AddNeighbour(current.x - 1, current.y, current.z);
                    ret &= AddNeighbour(current.x + 1, current.y, current.z);
                    ret &= AddNeighbour(current.x, current.y - 1, current.z);
                    ret &= AddNeighbour(current.x, current.y + 1, current.z);
                    ret &= AddNeighbour(current.x, current.y, current.z - 1);
                    ret &= AddNeighbour(current.x, current.y, current.z + 1);
                }

                return ret;
            }

            byte v;
            // Find an initial place
            for (int z = 1; z < vd.gridSize.z - 1; z++)
            {
                for (int y = 1; y < vd.gridSize.y - 1; y++)
                {
                    for (int x = 1; x < vd.gridSize.x - 1; x++)
                    {
                        if (vd[x, y, z] == 0)
                        {
                            // Check if this is adjacent to something
                            v = GetAdjacent(vd, x, y, z);
                            if (v != 0)
                            {
                                // Flood fill here, using 255 as the fill value
                                if (FloodFillAndCheckBounds(vd, x, y, z, 255))
                                {
                                    // Transform this into a wall, because it's encolosed
                                    vd.Replace(255, 3);
                                }
                                else
                                {
                                    // Transform this into a permanent block (for now), so it doesn't get selected again
                                    vd.Replace(255, 254);
                                }
                            }
                        }
                    }
                }
            }

            // The ones that were marked as not enclosed, mark them as empty space again
            vd.Replace(254, 0);
        }

        private static void FindAndFillEmpty<T>(VoxelData<T> vd)
            where T : struct, IEquatable<T>, IOccupancyState
        {
            if ((vd == null) || (vd.data == null) || (vd.data.Length == 0)) return;

            int sx = vd.gridSize.x;
            int sy = vd.gridSize.y;
            int sz = vd.gridSize.z;

            if ((sx <= 0) || (sy <= 0) || (sz <= 0)) return;

            int sliceSize = sx * sy;
            int voxelCount = vd.data.Length;

            // Exterior regions need to be remembered between flood-fills, but
            // this should not be encoded in T. The only persistent mutation we
            // want on the voxel data is: enclosed empty region -> Occupy().
            bool[] knownExterior = new bool[voxelCount];

            // Stamp array avoids allocating/clearing a bool[] for each region.
            int[] visitedStamp = new int[voxelCount];
            int currentStamp = 0;

            Stack<Vector3Int> stack = new Stack<Vector3Int>();
            List<int> region = new List<int>();

            int IndexOf(int x, int y, int z) => x + y * sx + z * sliceSize;

            bool IsBoundary(int x, int y, int z) =>
                (x == 0) || (y == 0) || (z == 0) ||
                (x == sx - 1) || (y == sy - 1) || (z == sz - 1);

            bool IsInside(int x, int y, int z) =>
                (x >= 0) && (y >= 0) && (z >= 0) &&
                (x < sx) && (y < sy) && (z < sz);

            void AddNeighbour(int x, int y, int z, ref bool enclosed)
            {
                if (!IsInside(x, y, z))
                {
                    enclosed = false;
                    return;
                }

                int index = IndexOf(x, y, z);

                // If this flood reaches a region already known to be exterior,
                // the current region is exterior as well.
                if (knownExterior[index])
                {
                    enclosed = false;
                    return;
                }

                if (visitedStamp[index] == currentStamp) return;
                if (!vd.data[index].IsEmpty()) return;

                visitedStamp[index] = currentStamp;
                stack.Push(new Vector3Int(x, y, z));
                region.Add(index);

                if (IsBoundary(x, y, z)) enclosed = false;
            }

            bool FloodFillAndCheckBounds(int startX, int startY, int startZ)
            {
                currentStamp++;

                // Extremely unlikely, but keeps the stamp logic correct if this
                // is ever used on many regions over a very long-running process.
                if (currentStamp == int.MaxValue)
                {
                    Array.Clear(visitedStamp, 0, visitedStamp.Length);
                    currentStamp = 1;
                }

                stack.Clear();
                region.Clear();

                bool enclosed = true;
                AddNeighbour(startX, startY, startZ, ref enclosed);

                while (stack.Count > 0)
                {
                    var current = stack.Pop();

                    AddNeighbour(current.x - 1, current.y, current.z, ref enclosed);
                    AddNeighbour(current.x + 1, current.y, current.z, ref enclosed);
                    AddNeighbour(current.x, current.y - 1, current.z, ref enclosed);
                    AddNeighbour(current.x, current.y + 1, current.z, ref enclosed);
                    AddNeighbour(current.x, current.y, current.z - 1, ref enclosed);
                    AddNeighbour(current.x, current.y, current.z + 1, ref enclosed);
                }

                return enclosed;
            }

            for (int z = 0; z < sz; z++)
            {
                for (int y = 0; y < sy; y++)
                {
                    for (int x = 0; x < sx; x++)
                    {
                        int seedIndex = IndexOf(x, y, z);

                        if (knownExterior[seedIndex]) continue;
                        if (!vd.data[seedIndex].IsEmpty()) continue;

                        bool enclosed = FloodFillAndCheckBounds(x, y, z);

                        if (enclosed)
                        {
                            for (int i = 0; i < region.Count; i++)
                            {
                                ref T voxel = ref vd.data[region[i]];
                                voxel.Occupy();
                            }
                        }
                        else
                        {
                            for (int i = 0; i < region.Count; i++)
                            {
                                knownExterior[region[i]] = true;
                            }
                        }
                    }
                }
            }
        }

        // Cheap early reject for triangle-vs-AABB intersection.
        // Returns true if the triangle plane does NOT intersect the box, meaning the full triangle/AABB test can be skipped safely.
        private static bool CheapPlaneReject(Vector3 aabbMin, Vector3 aabbMax, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            // Box center and half extents
            Vector3 c = (aabbMin + aabbMax) * 0.5f;
            Vector3 e = (aabbMax - aabbMin) * 0.5f;

            // Triangle plane normal
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);

            // Degenerate triangle: can't reject on plane reliably
            float nLenSq = n.sqrMagnitude;
            if (nLenSq < 1e-20f)
                return false;

            // Plane equation: dot(n, x) + d = 0
            float d = -Vector3.Dot(n, v0);

            // Signed distance of box center to plane (scaled by |n|)
            float s = Vector3.Dot(n, c) + d;

            // Projection interval radius of the box onto plane normal
            float r = Mathf.Abs(n.x) * e.x +
                      Mathf.Abs(n.y) * e.y +
                      Mathf.Abs(n.z) * e.z;

            // If the whole box is on one side of the plane, no intersection
            return Mathf.Abs(s) > r;
        }
    }
}