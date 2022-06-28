using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelTools 
{
    static public VoxelData Voxelize(Mesh mesh, float density, bool forcePowerOfTwo = false, float triangleScale = 1.0f, float gridScale = 1.0f, bool fillEmpty = false)
    {
        VoxelData ret = new VoxelData();

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

        ret.gridSize = gridSize;
        ret.voxelSize = new Vector3(bounds.size.x / (float)gridSize.x,
                                    bounds.size.y / (float)gridSize.y,
                                    bounds.size.z / (float)gridSize.z);
        ret.offset = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
        ret.data = new byte[gridSize.x * gridSize.y * gridSize.z];

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

                var min_x = Mathf.FloorToInt((Mathf.Min(v1.x, v2.x, v3.x) - ret.offset.x) * densityV.x);
                var min_y = Mathf.FloorToInt((Mathf.Min(v1.y, v2.y, v3.y) - ret.offset.y) * densityV.y);
                var min_z = Mathf.FloorToInt((Mathf.Min(v1.z, v2.z, v3.z) - ret.offset.z) * densityV.z);
                var max_x = Mathf.CeilToInt((Mathf.Max(v1.x, v2.x, v3.x) - ret.offset.x) * densityV.x);
                var max_y = Mathf.CeilToInt((Mathf.Max(v1.y, v2.y, v3.y) - ret.offset.y) * densityV.y);
                var max_z = Mathf.CeilToInt((Mathf.Max(v1.z, v2.z, v3.z) - ret.offset.z) * densityV.z);

                // DEBUG CODE: Just cover everything
                /*min_x = min_y = min_z = 0;
                max_x = gridSize.x; max_y = gridSize.y; max_z = gridSize.z; //*/

                var offset = Vector3.zero;

                for (int z = Mathf.Max(0, min_z); z <= Mathf.Min(max_z, gridSize.z - 1); z++)
                {
                    offset.z = ret.offset.z + z * ret.voxelSize.z;
                    for (int y = Mathf.Max(0, min_y); y <= Mathf.Min(max_y, gridSize.y - 1); y++)
                    {
                        offset.y = ret.offset.y + y * ret.voxelSize.y;
                        for (int x = Mathf.Max(0, min_x); x <= Mathf.Min(max_x, gridSize.x - 1); x++)
                        {
                            int voxelIndex = x + (y * gridSize.x) + (z * gridSize.x * gridSize.y);
                            if (ret.data[voxelIndex] == 0)
                            {
                                // Check this voxel
                                offset.x = ret.offset.x + x * ret.voxelSize.x;
                                Vector3 aabb_min = offset;
                                Vector3 aabb_max = aabb_min + ret.voxelSize;

                                if (AABB.Intersects(aabb_min, aabb_max, v1, v2, v3))
                                {
                                    ret.data[voxelIndex] = 1;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (fillEmpty)
        {
            FindAndFillEmpty(ret);
        }

        return ret;
    }

    private static void FindAndFillEmpty(VoxelData vd)
    {
        byte GetAdjacent(VoxelData vd, int x, int y, int z)
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

        bool FloodFillAndCheckBounds(VoxelData vd, int sx, int sy, int sz, byte v)
        {
            Stack<Vector3Int>   stack = new Stack<Vector3Int>();
            int                 lx = vd.gridSize.x - 1;
            int                 ly = vd.gridSize.y - 1;
            int                 lz = vd.gridSize.z - 1;

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
                    if (vd[x,y,z] == 0)
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

    public static void MarkTop(VoxelData vd, byte value)
    {
        int index = 0;
        int incAboveItem = vd.gridSize.x;

        for (int z = 0; z < vd.gridSize.z; z++)
        {
            for (int y = 0; y < vd.gridSize.y; y++)
            {
                for (int x = 0; x < vd.gridSize.x; x++)
                {
                    if (vd.data[index] != 0)
                    {
                        if ((y == vd.gridSize.y - 1) ||
                            (vd.data[index + incAboveItem] == 0))
                        {
                            vd.data[index] = value;
                        }
                    }
                    index++;
                }
            }
        }
    }

    public static void MarkMinHeight(VoxelData vd, int voxelHeight, byte value)
    {
        int index = 0;

        for (int z = 0; z < vd.gridSize.z; z++)
        {
            for (int x = 0; x < vd.gridSize.x; x++)
            {
                // Check starting Y
                for (int y = 0; y < vd.gridSize.y; y++)
                {
                    index = x + (y * vd.gridSize.x) + (z * vd.gridSize.y * vd.gridSize.x);
                    if (vd.data[index] != 0)
                    {
                        int height = GetHeight(vd, x, y, z);
                        if (height >= voxelHeight)
                        {
                            vd.data[index] = value;
                        }
                    }
                    index++;
                }
            }
        }
    }

    public static int GetHeight(VoxelData vd, int x, int y, int z)
    {
        int index = x + ((y + 1) * vd.gridSize.x) + (z * vd.gridSize.x * vd.gridSize.y);
        int incAboveItem = vd.gridSize.x;

        for (int yy = y + 1; yy < vd.gridSize.y; yy++)
        {
            if (vd.data[index] != 0)
            {
                return yy - y - 1;
            }
            index += incAboveItem;
        }

        // Nothing on top, infinite height
        return int.MaxValue;
    }

    public static (int, int, int)? FindVoxel(VoxelData vd, byte value)
    {
        int index = 0;

        for (int z = 0; z < vd.gridSize.z; z++)
        {
            for (int y = 0; y < vd.gridSize.y; y++)
            {
                for (int x = 0; x < vd.gridSize.x; x++)
                {
                    if (vd.data[index] == value)
                    {
                        return (x, y, z);
                    }
                    index++;
                }
            }
        }

        return null;
    }

    public static void FloodFill(VoxelData vd, int x, int y, int z, byte value, byte newValue)
    {
        if ((x < 0) || (y < 0) || (z < 0)) return;
        if ((x >= vd.gridSize.x) || (y >= vd.gridSize.y) || (z >= vd.gridSize.z)) return;

        int index = x + (y * vd.gridSize.x) + (z * vd.gridSize.x * vd.gridSize.y);
        if (vd.data[index] == value)
        {
            vd.data[index] = newValue;

            FloodFill(vd, x - 1, y, z, value, newValue);
            FloodFill(vd, x + 1, y, z, value, newValue);
            FloodFill(vd, x, y - 1, z, value, newValue);
            FloodFill(vd, x, y + 1, z, value, newValue);
            FloodFill(vd, x, y, z - 1, value, newValue);
            FloodFill(vd, x, y, z + 1, value, newValue);
        }
    }

    public static int FloodFillWithStep(VoxelData vd, int stepSize, int x, int y, int z, byte value, byte newValue)
    {
        int count = 0;
        Queue<Vector3Int> explore = new Queue<Vector3Int>();

        explore.Enqueue(new Vector3Int(x, y, z));

        while (explore.Count > 0)
        {
            var current = explore.Dequeue();

            if ((current.x < 0) || (current.y < 0) || (current.z < 0)) continue;
            if ((current.x >= vd.gridSize.x) || (current.y >= vd.gridSize.y) || (current.z >= vd.gridSize.z)) continue;

            int index = current.x + (current.y * vd.gridSize.x) + (current.z * vd.gridSize.x * vd.gridSize.y);
            if (vd.data[index] == value)
            {
                vd.data[index] = newValue;
                count++;

                for (int dy = -stepSize; dy <= stepSize; dy++)
                {
                    explore.Enqueue(new Vector3Int(current.x - 1, current.y + dy, current.z));
                    explore.Enqueue(new Vector3Int(current.x + 1, current.y + dy, current.z));
                    explore.Enqueue(new Vector3Int(current.x, current.y + dy, current.z - 1));
                    explore.Enqueue(new Vector3Int(current.x, current.y + dy, current.z + 1));
                }
                explore.Enqueue(new Vector3Int(current.x, current.y - 1, current.z));
                explore.Enqueue(new Vector3Int(current.x, current.y + 1, current.z));
            }
        }

        return count;
    }

    public static int CountVoxel(VoxelData vd, byte value)
    {
        int index = 0;
        int count = 0;

        for (int z = 0; z < vd.gridSize.z; z++)
        {
            for (int y = 0; y < vd.gridSize.y; y++)
            {
                for (int x = 0; x < vd.gridSize.x; x++)
                {
                    if (vd.data[index] == value) count++;
                    index++;
                }
            }
        }

        return count;
    }

    public struct VoxelRegion
    {
        public int  startX, startY, startZ;
        public int  size;
        public byte voxelId;
    }

    public static List<VoxelRegion> MarkRegions(VoxelData vd, int stepSize, byte value, byte firstRegion, byte lastRegion)
    {
        int regionId = 0;
        int regionRange = lastRegion - firstRegion + 1;

        List<VoxelRegion>   ret = null;

        var pos = FindVoxel(vd, value);
        while (pos.HasValue)
        {
            // Found a start position for the region
            int x, y, z;
            (x, y, z) = pos.Value;

            if (ret == null) ret = new List<VoxelRegion>();
            VoxelRegion vr = new VoxelRegion();
            vr.startX = x; vr.startY = y; vr.startZ = z;
            vr.size = FloodFillWithStep(vd, stepSize, x, y, z, value, (byte)(regionId + firstRegion));
            vr.voxelId = (byte)(regionId + firstRegion);
            ret.Add(vr);

            regionId = (regionId + 1) % regionRange;

            pos = FindVoxel(vd, value);
        }

        return ret;
    }
}
