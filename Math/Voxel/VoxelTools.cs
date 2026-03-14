using System.Collections.Generic;
using UC;
using UnityEngine;
using UnityEngine.Rendering;

public static class VoxelTools 
{
    public static void MarkTop(VoxelData<byte> vd, byte value)
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

    public static void MarkMinHeight(VoxelData<byte> vd, int voxelHeight, byte value)
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

    public static int GetHeight(VoxelData<byte> vd, int x, int y, int z)
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

    public static (int, int, int)? FindVoxel(VoxelData<byte> vd, byte value)
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

    public static void FloodFill(VoxelData<byte> vd, int x, int y, int z, byte value, byte newValue)
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

    public static int FloodFillWithStep(VoxelData<byte> vd, int stepSize, int x, int y, int z, byte value, byte newValue)
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

    public static int CountVoxel(VoxelData<byte> vd, byte value)
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
        public int startX, startY, startZ;
        public int size;
        public byte voxelId;
    }

    public static List<VoxelRegion> MarkRegions(VoxelData<byte> vd, int stepSize, byte value, byte firstRegion, byte lastRegion)
    {
        int regionId = 0;
        int regionRange = lastRegion - firstRegion + 1;

        List<VoxelRegion> ret = null;

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

    public enum MaterialMode { Single, Palette, Multi };
    public static Mesh ConvertToMesh(VoxelData<byte> voxelData, MaterialMode materialMode, Vector3 uvScale, ref Dictionary<int, int> voxelValueToMaterialId)
    {
        voxelValueToMaterialId ??= new Dictionary<int, int>();

        var _indices = new List<List<int>>();

        if (materialMode == MaterialMode.Multi)
        {
            // Count materials in use
            for (int i = 0; i < voxelData.gridSize.x * voxelData.gridSize.y * voxelData.gridSize.z; i++)
            {
                var tmp = voxelData.data[i];
                if (tmp > 0)
                {
                    if (!voxelValueToMaterialId.ContainsKey(tmp))
                    {
                        voxelValueToMaterialId.Add(tmp, _indices.Count);
                        _indices.Add(new List<int>());
                    }
                }
            }
        }
        else
        {
            _indices.Add(new List<int>());
        }

        var _vertices = new List<Vector3>();
        var _normals = new List<Vector3>();
        var _uvs = new List<Vector2>();

        int index = 0;
        Vector3 center = Vector3.zero;
        Vector3 half_size = voxelData.voxelSize * 0.5f;
        Vector3 p1, p2, p3, p4, n;
        Vector2 uv1, uv2, uv3, uv4;
        int incX = 1;
        int incY = voxelData.gridSize.x;
        int incZ = voxelData.gridSize.x * voxelData.gridSize.y;
        Vector3 o = voxelData.minBound + voxelData.voxelSize * 0.5f;
        byte value;
        float uvPaletteScale = 1.0f / 16.0f;
        float uvPaletteOffset = 0.5f / 16.0f;

        uv1 = uv2 = uv3 = uv4 = Vector2.zero;

        for (int z = 0; z < voxelData.gridSize.z; z++)
        {
            center.z = o.z + z * voxelData.voxelSize.z;
            for (int y = 0; y < voxelData.gridSize.y; y++)
            {
                center.y = o.y + y * voxelData.voxelSize.y;
                for (int x = 0; x < voxelData.gridSize.x; x++)
                {
                    center.x = o.x + x * voxelData.voxelSize.x;
                    value = voxelData.data[index];
                    if (value > 0)
                    {
                        List<int> targetIndices = null;
                        switch (materialMode)
                        {
                            case MaterialMode.Single:
                                targetIndices = _indices[0];
                                break;
                            case MaterialMode.Palette:
                                targetIndices = _indices[0];
                                uv1 = uv2 = uv3 = uv4 = new Vector2((value % 16) * uvPaletteScale + uvPaletteOffset, 1 - ((value / 16) * uvPaletteScale + uvPaletteOffset));
                                break;
                            case MaterialMode.Multi:
                                targetIndices = _indices[voxelValueToMaterialId[value]];
                                break;
                            default:
                                break;
                        }
                        // Add the 6 quads that compose the cube
                        // -Z
                        if ((z == 0) || (voxelData.data[index - incZ] == 0))
                        {
                            p1 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p2 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p3 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p4 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                            n = -Vector3.forward;
                            if (materialMode != MaterialMode.Palette)
                            {
                                uv1 = new Vector2(p1.x * uvScale.x, p1.y * uvScale.y);
                                uv2 = new Vector2(p2.x * uvScale.x, p2.y * uvScale.y);
                                uv3 = new Vector2(p3.x * uvScale.x, p3.y * uvScale.y);
                                uv4 = new Vector2(p4.x * uvScale.x, p4.y * uvScale.y);
                            }
                            GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                        }
                        // +X
                        if ((x == voxelData.gridSize.x - 1) || (voxelData.data[index + incX] == 0))
                        {
                            p1 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p2 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p3 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p4 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                            n = Vector3.right;
                            if (materialMode != MaterialMode.Palette)
                            {
                                uv1 = new Vector2(p1.z * uvScale.z, p1.y * uvScale.y);
                                uv2 = new Vector2(p2.z * uvScale.z, p2.y * uvScale.y);
                                uv3 = new Vector2(p3.z * uvScale.z, p3.y * uvScale.y);
                                uv4 = new Vector2(p4.z * uvScale.z, p4.y * uvScale.y);
                            }
                            GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                        }
                        // +Z
                        if ((z == voxelData.gridSize.z - 1) || (voxelData.data[index + incZ] == 0))
                        {
                            p1 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p2 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p3 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p4 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                            n = Vector3.forward;
                            if (materialMode != MaterialMode.Palette)
                            {
                                uv1 = new Vector2(p1.x * uvScale.x, p1.y * uvScale.y);
                                uv2 = new Vector2(p2.x * uvScale.x, p2.y * uvScale.y);
                                uv3 = new Vector2(p3.x * uvScale.x, p3.y * uvScale.y);
                                uv4 = new Vector2(p4.x * uvScale.x, p4.y * uvScale.y);
                            }
                            GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                        }
                        // -X
                        if ((x == 0) || (voxelData.data[index - incX] == 0))
                        {
                            p1 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p2 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p3 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p4 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                            n = -Vector3.right;
                            if (materialMode != MaterialMode.Palette)
                            {
                                uv1 = new Vector2(p1.z * uvScale.z, p1.y * uvScale.y);
                                uv2 = new Vector2(p2.z * uvScale.z, p2.y * uvScale.y);
                                uv3 = new Vector2(p3.z * uvScale.z, p3.y * uvScale.y);
                                uv4 = new Vector2(p4.z * uvScale.z, p4.y * uvScale.y);
                            }
                            GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                        }
                        // +Y
                        if ((y == voxelData.gridSize.y - 1) || (voxelData.data[index + incY] == 0))
                        {
                            p1 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p2 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p3 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p4 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                            n = Vector3.up;
                            if (materialMode != MaterialMode.Palette)
                            {
                                uv1 = new Vector2(p1.x * uvScale.x, p1.z * uvScale.z);
                                uv2 = new Vector2(p2.x * uvScale.x, p2.z * uvScale.z);
                                uv3 = new Vector2(p3.x * uvScale.x, p3.z * uvScale.z);
                                uv4 = new Vector2(p4.x * uvScale.x, p4.z * uvScale.z);
                            }
                            GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                        }
                        // -Y
                        if ((y == 0) || (voxelData.data[index - incY] == 0))
                        {
                            p1 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                            p2 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p3 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                            p4 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                            n = -Vector3.up;
                            if (materialMode != MaterialMode.Palette)
                            {
                                uv1 = new Vector2(p1.x * uvScale.x, p1.z * uvScale.z);
                                uv2 = new Vector2(p2.x * uvScale.x, p2.z * uvScale.z);
                                uv3 = new Vector2(p3.x * uvScale.x, p3.z * uvScale.z);
                                uv4 = new Vector2(p4.x * uvScale.x, p4.z * uvScale.z);
                            }
                            GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                        }
                    }

                    index++;
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "VoxelMesh";
        mesh.SetVertices(_vertices);
        mesh.SetNormals(_normals);
        mesh.SetUVs(0, _uvs);
        if (_vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.subMeshCount = _indices.Count;
        if (materialMode == MaterialMode.Multi)
        {
            for (int i = 0; i < 256; i++)
            {
                if (voxelValueToMaterialId.ContainsKey(i))
                {
                    // My functions for AddQuad are reverting the cull order, for some unknowable reason from the past, need to refactor this
                    int id = voxelValueToMaterialId[i];
                    GeometricFactory.InvertOrder(_indices[id]);

                    mesh.SetTriangles(_indices[id], id);
                }
            }
        }
        else
        {
            // My functions for AddQuad are reverting the cull order, for some unknowable reason from the past, need to refactor this
            GeometricFactory.InvertOrder(_indices[0]);

            mesh.SetTriangles(_indices[0], 0);
        }
        mesh.UploadMeshData(true);

        return mesh;
    }

    public static Mesh ConvertToMesh(VoxelData<float> voxelData, bool normalize = false, float colorScalePower = 1.0f, Gradient gradient = null)
    {
        var _indices = new List<int>();
        var _vertices = new List<Vector3>();
        var _normals = new List<Vector3>();
        var _uvs = new List<Vector2>();
        var _colors = new List<Color>();

        int index = 0;
        Vector3 center = Vector3.zero;
        Vector3 halfSize = voxelData.voxelSize * 0.5f;
        Vector3 p1, p2, p3, p4, n;
        Vector2 uv1, uv2, uv3, uv4;

        int incX = 1;
        int incY = voxelData.gridSize.x;
        int incZ = voxelData.gridSize.x * voxelData.gridSize.y;
        Vector3 o = voxelData.minBound + voxelData.voxelSize * 0.5f;

        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        // Optional normalization range from valid/renderable voxels only
        if (normalize)
        {
            int total = voxelData.gridSize.x * voxelData.gridSize.y * voxelData.gridSize.z;
            for (int i = 0; i < total; i++)
            {
                float v = voxelData.data[i];
                if (!IsRenderableFloatVoxel(v))
                    continue;

                if (v < minValue) minValue = v;
                if (v > maxValue) maxValue = v;
            }

            if (minValue == float.MaxValue)
            {
                minValue = 0f;
                maxValue = 1f;
            }
            else if (Mathf.Approximately(minValue, maxValue))
            {
                maxValue = minValue + 1f;
            }
        }

        uv1 = uv2 = uv3 = uv4 = Vector2.zero;

        for (int z = 0; z < voxelData.gridSize.z; z++)
        {
            center.z = o.z + z * voxelData.voxelSize.z;

            for (int y = 0; y < voxelData.gridSize.y; y++)
            {
                center.y = o.y + y * voxelData.voxelSize.y;

                for (int x = 0; x < voxelData.gridSize.x; x++)
                {
                    center.x = o.x + x * voxelData.voxelSize.x;

                    float value = voxelData.data[index];
                    if (IsRenderableFloatVoxel(value))
                    {
                        Color voxelColor = GetVoxelColor(value, normalize, colorScalePower, minValue, maxValue, gradient);

                        // -Z
                        if ((z == 0) || (!IsRenderableFloatVoxel(voxelData.data[index - incZ])))
                        {
                            p1 = center - Vector3.forward * halfSize.z - Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p2 = center - Vector3.forward * halfSize.z - Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p3 = center - Vector3.forward * halfSize.z + Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p4 = center - Vector3.forward * halfSize.z + Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            n = -Vector3.forward;
                            AddColoredQuad(p1, p2, p3, p4, n, voxelColor, ref _vertices, ref _normals, ref _uvs, ref _colors, ref _indices);
                        }

                        // +X
                        if ((x == voxelData.gridSize.x - 1) || (!IsRenderableFloatVoxel(voxelData.data[index + incX])))
                        {
                            p1 = center - Vector3.forward * halfSize.z + Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p2 = center - Vector3.forward * halfSize.z - Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p3 = center + Vector3.forward * halfSize.z - Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p4 = center + Vector3.forward * halfSize.z + Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            n = Vector3.right;
                            AddColoredQuad(p1, p2, p3, p4, n, voxelColor, ref _vertices, ref _normals, ref _uvs, ref _colors, ref _indices);
                        }

                        // +Z
                        if ((z == voxelData.gridSize.z - 1) || (!IsRenderableFloatVoxel(voxelData.data[index + incZ])))
                        {
                            p1 = center + Vector3.forward * halfSize.z + Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p2 = center + Vector3.forward * halfSize.z + Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p3 = center + Vector3.forward * halfSize.z - Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p4 = center + Vector3.forward * halfSize.z - Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            n = Vector3.forward;
                            AddColoredQuad(p1, p2, p3, p4, n, voxelColor, ref _vertices, ref _normals, ref _uvs, ref _colors, ref _indices);
                        }

                        // -X
                        if ((x == 0) || (!IsRenderableFloatVoxel(voxelData.data[index - incX])))
                        {
                            p1 = center + Vector3.forward * halfSize.z + Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p2 = center + Vector3.forward * halfSize.z - Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p3 = center - Vector3.forward * halfSize.z - Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p4 = center - Vector3.forward * halfSize.z + Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            n = -Vector3.right;
                            AddColoredQuad(p1, p2, p3, p4, n, voxelColor, ref _vertices, ref _normals, ref _uvs, ref _colors, ref _indices);
                        }

                        // +Y
                        if ((y == voxelData.gridSize.y - 1) || (!IsRenderableFloatVoxel(voxelData.data[index + incY])))
                        {
                            p1 = center + Vector3.forward * halfSize.z + Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p2 = center + Vector3.forward * halfSize.z + Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p3 = center - Vector3.forward * halfSize.z + Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p4 = center - Vector3.forward * halfSize.z + Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            n = Vector3.up;
                            AddColoredQuad(p1, p2, p3, p4, n, voxelColor, ref _vertices, ref _normals, ref _uvs, ref _colors, ref _indices);
                        }

                        // -Y
                        if ((y == 0) || (!IsRenderableFloatVoxel(voxelData.data[index - incY])))
                        {
                            p1 = center + Vector3.forward * halfSize.z - Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            p2 = center + Vector3.forward * halfSize.z - Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p3 = center - Vector3.forward * halfSize.z - Vector3.up * halfSize.y + Vector3.right * halfSize.x;
                            p4 = center - Vector3.forward * halfSize.z - Vector3.up * halfSize.y - Vector3.right * halfSize.x;
                            n = -Vector3.up;
                            AddColoredQuad(p1, p2, p3, p4, n, voxelColor, ref _vertices, ref _normals, ref _uvs, ref _colors, ref _indices);
                        }
                    }

                    index++;
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "VoxelFloatMesh";

        if (_vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(_vertices);
        mesh.SetNormals(_normals);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_colors);

        // Keep your old winding convention
        GeometricFactory.InvertOrder(_indices);
        mesh.SetTriangles(_indices, 0);

        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);

        return mesh;
    }

    private static bool IsRenderableFloatVoxel(float value)
    {
        if ((float.IsNaN(value)) || (float.IsInfinity(value))) return false;

        if (value == float.MaxValue) return false;

        return true;
    }

    private static Color GetVoxelColor(float value, bool normalize, float colorScalePower, float minValue, float maxValue, Gradient gradient)
    {
        float t;

        if (normalize)
        {
            t = Mathf.Pow(Mathf.InverseLerp(minValue, maxValue, value), colorScalePower);
        }
        else
        {
            t = Mathf.Pow(Mathf.Clamp01(value), colorScalePower);
        }

        if (gradient != null)
            return gradient.Evaluate(Mathf.Pow(t, colorScalePower));

        return new Color(t, t, t, 1f);
    }

    private static void AddColoredQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4,
                                       Vector3 normal,
                                       Color color,
                                       ref List<Vector3> vertices,
                                       ref List<Vector3> normals,
                                       ref List<Vector2> uvs,
                                       ref List<Color> colors,
                                       ref List<int> indices)
    {
        int start = vertices.Count;

        vertices.Add(p1);
        vertices.Add(p2);
        vertices.Add(p3);
        vertices.Add(p4);

        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);

        uvs.Add(Vector2.zero);
        uvs.Add(Vector2.right);
        uvs.Add(Vector2.one);
        uvs.Add(Vector2.up);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        indices.Add(start + 0);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start + 0);
        indices.Add(start + 2);
        indices.Add(start + 3);
    }
}
