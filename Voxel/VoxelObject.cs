using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{

    public class VoxelObject : MonoBehaviour
    {
        [Serializable]
        public enum MaterialMode { Single, Palette, Multi };

        [SerializeField] private VoxelData _voxelData;
        private Mesh _mesh;
        [SerializeField] private MaterialMode _materialMode;
        [SerializeField] private Dictionary<int, int> _voxelValueToMaterialId;

        public VoxelData voxelData
        {
            get => _voxelData;
            set
            {
                if (_voxelData != value) _mesh = null;

                _voxelData = value;
            }
        }

        public Vector3Int gridSize
        {
            get => _voxelData.gridSize;
            set
            {
                if (_voxelData.gridSize != value) _mesh = null;

                _voxelData.gridSize = value;
            }
        }

        public Vector3 voxelSize
        {
            get => _voxelData.voxelSize;
            set
            {
                if (_voxelData.voxelSize != value) _mesh = null;

                _voxelData.voxelSize = value;
            }
        }

        public byte[] data
        {
            get => _voxelData.data;
            set
            {
                _voxelData.data = value;
                if (_voxelData.data != null)
                {
                    if (_voxelData.data.Length != _voxelData.gridSize.x * _voxelData.gridSize.y * _voxelData.gridSize.z)
                    {
                        Debug.LogWarning("Voxel grid size incompatible with provided data: set grid size first!");
                    }
                }
                _mesh = null;
            }
        }

        public Vector3 offset
        {
            get => _voxelData.offset;
            set
            {
                if (_voxelData.offset != value) _mesh = null;

                _voxelData.offset = value;
            }
        }

        public Vector3 uvScale
        {
            get => _voxelData.uvScale;
            set
            {
                if (_voxelData.uvScale != value)
                {
                    _mesh = null;
                    _voxelData.uvScale = value;
                }
            }
        }

        public MaterialMode materialMode
        {
            get => _materialMode;
            set
            {
                if (_materialMode != value)
                {
                    _mesh = null;
                    _materialMode = value;
                }
            }
        }

        public Dictionary<int, int> voxelValueToMaterialId
        {
            get => _voxelValueToMaterialId;
        }

        public void AllocateData()
        {
            _voxelData.data = new byte[_voxelData.gridSize.x * _voxelData.gridSize.y * _voxelData.gridSize.z];
            _mesh = null;
        }

        public Mesh GetMesh()
        {
            if (_mesh) return _mesh;
            if (_voxelData.data == null) return null;

            _voxelValueToMaterialId = new Dictionary<int, int>();

            var _indices = new List<List<int>>();

            if (_materialMode == MaterialMode.Multi)
            {
                // Count materials in use
                for (int i = 0; i < _voxelData.gridSize.x * _voxelData.gridSize.y * _voxelData.gridSize.z; i++)
                {
                    var tmp = _voxelData.data[i];
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
            Vector3 half_size = _voxelData.voxelSize * 0.5f;
            Vector3 p1, p2, p3, p4, n;
            Vector2 uv1, uv2, uv3, uv4;
            int incX = 1;
            int incY = _voxelData.gridSize.x;
            int incZ = _voxelData.gridSize.x * _voxelData.gridSize.y;
            Vector3 o = _voxelData.offset + _voxelData.voxelSize * 0.5f;
            byte value;
            float uvPaletteScale = 1.0f / 16.0f;
            float uvPaletteOffset = 0.5f / 16.0f;

            uv1 = uv2 = uv3 = uv4 = Vector2.zero;

            for (int z = 0; z < _voxelData.gridSize.z; z++)
            {
                center.z = o.z + z * _voxelData.voxelSize.z;
                for (int y = 0; y < _voxelData.gridSize.y; y++)
                {
                    center.y = o.y + y * _voxelData.voxelSize.y;
                    for (int x = 0; x < _voxelData.gridSize.x; x++)
                    {
                        center.x = o.x + x * _voxelData.voxelSize.x;
                        value = _voxelData.data[index];
                        if (value > 0)
                        {
                            List<int> targetIndices = null;
                            switch (_materialMode)
                            {
                                case MaterialMode.Single:
                                    targetIndices = _indices[0];
                                    break;
                                case MaterialMode.Palette:
                                    targetIndices = _indices[0];
                                    uv1 = uv2 = uv3 = uv4 = new Vector2((value % 16) * uvPaletteScale + uvPaletteOffset, 1 - ((value / 16) * uvPaletteScale + uvPaletteOffset));
                                    break;
                                case MaterialMode.Multi:
                                    targetIndices = _indices[_voxelValueToMaterialId[value]];
                                    break;
                                default:
                                    break;
                            }
                            // Add the 6 quads that compose the cube
                            // -Z
                            if ((z == 0) || (_voxelData.data[index - incZ] == 0))
                            {
                                p1 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p2 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p3 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p4 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                                n = -Vector3.forward;
                                if (_materialMode != MaterialMode.Palette)
                                {
                                    uv1 = new Vector2(p1.x * uvScale.x, p1.y * uvScale.y);
                                    uv2 = new Vector2(p2.x * uvScale.x, p2.y * uvScale.y);
                                    uv3 = new Vector2(p3.x * uvScale.x, p3.y * uvScale.y);
                                    uv4 = new Vector2(p4.x * uvScale.x, p4.y * uvScale.y);
                                }
                                GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                            }
                            // +X
                            if ((x == _voxelData.gridSize.x - 1) || (_voxelData.data[index + incX] == 0))
                            {
                                p1 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p2 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p3 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p4 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                                n = Vector3.right;
                                if (_materialMode != MaterialMode.Palette)
                                {
                                    uv1 = new Vector2(p1.z * uvScale.z, p1.y * uvScale.y);
                                    uv2 = new Vector2(p2.z * uvScale.z, p2.y * uvScale.y);
                                    uv3 = new Vector2(p3.z * uvScale.z, p3.y * uvScale.y);
                                    uv4 = new Vector2(p4.z * uvScale.z, p4.y * uvScale.y);
                                }
                                GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                            }
                            // +Z
                            if ((z == _voxelData.gridSize.z - 1) || (_voxelData.data[index + incZ] == 0))
                            {
                                p1 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p2 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p3 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p4 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                                n = Vector3.forward;
                                if (_materialMode != MaterialMode.Palette)
                                {
                                    uv1 = new Vector2(p1.x * uvScale.x, p1.y * uvScale.y);
                                    uv2 = new Vector2(p2.x * uvScale.x, p2.y * uvScale.y);
                                    uv3 = new Vector2(p3.x * uvScale.x, p3.y * uvScale.y);
                                    uv4 = new Vector2(p4.x * uvScale.x, p4.y * uvScale.y);
                                }
                                GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                            }
                            // -X
                            if ((x == 0) || (_voxelData.data[index - incX] == 0))
                            {
                                p1 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p2 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p3 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p4 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                                n = -Vector3.right;
                                if (_materialMode != MaterialMode.Palette)
                                {
                                    uv1 = new Vector2(p1.z * uvScale.z, p1.y * uvScale.y);
                                    uv2 = new Vector2(p2.z * uvScale.z, p2.y * uvScale.y);
                                    uv3 = new Vector2(p3.z * uvScale.z, p3.y * uvScale.y);
                                    uv4 = new Vector2(p4.z * uvScale.z, p4.y * uvScale.y);
                                }
                                GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                            }
                            // +Y
                            if ((y == _voxelData.gridSize.y - 1) || (_voxelData.data[index + incY] == 0))
                            {
                                p1 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p2 = center + Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p3 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p4 = center - Vector3.forward * half_size.z + Vector3.up * half_size.y + Vector3.right * half_size.x;
                                n = Vector3.up;
                                if (_materialMode != MaterialMode.Palette)
                                {
                                    uv1 = new Vector2(p1.x * uvScale.x, p1.z * uvScale.z);
                                    uv2 = new Vector2(p2.x * uvScale.x, p2.z * uvScale.z);
                                    uv3 = new Vector2(p3.x * uvScale.x, p3.z * uvScale.z);
                                    uv4 = new Vector2(p4.x * uvScale.x, p4.z * uvScale.z);
                                }
                                GeometricFactory.AddQuad(p1, n, uv1, p2, n, uv2, p3, n, uv3, p4, n, uv4, ref _vertices, ref _normals, ref _uvs, ref targetIndices);
                            }
                            // -Y
                            if ((y == 0) || (_voxelData.data[index - incY] == 0))
                            {
                                p1 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                                p2 = center + Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p3 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y + Vector3.right * half_size.x;
                                p4 = center - Vector3.forward * half_size.z - Vector3.up * half_size.y - Vector3.right * half_size.x;
                                n = -Vector3.up;
                                if (_materialMode != MaterialMode.Palette)
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

            _mesh = new Mesh();
            _mesh.name = name + "VoxelMesh";
            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetUVs(0, _uvs);
            if (_vertices.Count > 65535) _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.subMeshCount = _indices.Count;
            if (_materialMode == MaterialMode.Multi)
            {
                for (int i = 0; i < 256; i++)
                {
                    if (_voxelValueToMaterialId.ContainsKey(i))
                    {
                        _mesh.SetTriangles(_indices[_voxelValueToMaterialId[i]], _voxelValueToMaterialId[i]);
                    }
                }
            }
            else
            {
                _mesh.SetTriangles(_indices[0], 0);
            }
            _mesh.UploadMeshData(true);

            _vertices = null;
            _normals = null;
            _indices = null;

            return _mesh;
        }

        public void Set(int x, int y, int z, byte val)
        {
            _voxelData.data[x + (y * _voxelData.gridSize.x) + (z * _voxelData.gridSize.x * _voxelData.gridSize.y)] = val;
        }

        public void Set(VoxelData data)
        {
            _voxelData = data;
            _mesh = null;
        }

        public byte Get(int x, int y, int z)
        {
            return _voxelData.data[x + (y * _voxelData.gridSize.x) + (z * _voxelData.gridSize.x * _voxelData.gridSize.y)];
        }

        public int GetMaterialId(int value)
        {
            int matId;
            if (_voxelValueToMaterialId.TryGetValue(value, out matId)) return matId;

            return -1;
        }

        public (Vector3, Vector3) GetAABB(Vector3Int coords)
        {
            return GetAABB(coords.x, coords.y, coords.z);
        }
        public (Vector3, Vector3) GetAABB(int x, int y, int z)
        {
            Vector3 aabb_min = transform.position + offset + x * voxelSize.x * transform.right + y * voxelSize.y * transform.up + z * voxelSize.z * transform.forward;
            Vector3 aabb_max = aabb_min + voxelSize.x * transform.right + voxelSize.y * transform.up + voxelSize.z * transform.forward;

            return (aabb_min, aabb_max);
        }

        public void UpdateMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = GetMesh();
            }
        }
    }
}