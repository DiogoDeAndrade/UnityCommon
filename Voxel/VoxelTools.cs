using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class VoxelTools 
{
    public class VoxelData
    {
        public byte[] data;
        public Vector3Int gridSize;
        public Vector3 voxelSize;
        public Vector3 offset;
    }
    static public VoxelData Voxelize(Mesh mesh, float density, float triangleScale = 1.0f, float gridScale = 1.0f)
    {
        VoxelData ret = new VoxelData();

        Bounds bounds = mesh.bounds;
        bounds.Expand(bounds.size * gridScale);

        Vector3Int gridSize = new Vector3Int(Mathf.CeilToInt(bounds.size.x * density),
                                             Mathf.CeilToInt(bounds.size.y * density),
                                             Mathf.CeilToInt(bounds.size.z * density));
        float voxelSize = 1.0f / density;

        ret.gridSize = gridSize;
        ret.voxelSize = new Vector3(voxelSize, voxelSize, voxelSize);
        ret.offset = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
        ret.data = new byte[gridSize.x * gridSize.y * gridSize.z];

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

                if ((submesh == 1) && (index == 105))
                {
                    int a = 10;
                    a++;
                }

                var min_x = Mathf.FloorToInt((Mathf.Min(v1.x, v2.x, v3.x) - ret.offset.x) * density);
                var min_y = Mathf.FloorToInt((Mathf.Min(v1.y, v2.y, v3.y) - ret.offset.y) * density);
                var min_z = Mathf.FloorToInt((Mathf.Min(v1.z, v2.z, v3.z) - ret.offset.z) * density);
                var max_x = Mathf.CeilToInt((Mathf.Max(v1.x, v2.x, v3.x) - ret.offset.x) * density);
                var max_y = Mathf.CeilToInt((Mathf.Max(v1.y, v2.y, v3.y) - ret.offset.y) * density);
                var max_z = Mathf.CeilToInt((Mathf.Max(v1.z, v2.z, v3.z) - ret.offset.z) * density);

                // DEBUG CODE: Just cover everything
                /*min_x = min_y = min_z = 0;
                max_x = gridSize.x; max_y = gridSize.y; max_z = gridSize.z; //*/

                var offset = Vector3.zero;

                for (int z = Mathf.Max(0, min_z); z <= Mathf.Min(max_z, gridSize.z - 1); z++)
                {
                    offset.z = ret.offset.z + z * voxelSize;
                    for (int y = Mathf.Max(0, min_y); y <= Mathf.Min(max_y, gridSize.y - 1); y++)
                    {
                        offset.y = ret.offset.y + y * voxelSize;
                        for (int x = Mathf.Max(0, min_x); x <= Mathf.Min(max_x, gridSize.x - 1); x++)
                        {
                            int voxelIndex = x + (y * gridSize.x) + (z * gridSize.x * gridSize.y);
                            if (ret.data[voxelIndex] == 0)
                            {
                                // Check this voxel
                                offset.x = ret.offset.x + x * voxelSize;
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

        return ret;
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
}
