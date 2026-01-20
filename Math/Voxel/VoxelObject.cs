using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{
    public class VoxelObject : MonoBehaviour
    {
        [SerializeField] private VoxelData<byte> _voxelData;
        [SerializeField] private VoxelTools.MaterialMode    _materialMode;
        [SerializeField] private Dictionary<int, int>       _voxelValueToMaterialId;

        private Mesh _mesh;

        public VoxelData<byte> voxelData
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
            get => _voxelData.minBound;
            set
            {
                if (_voxelData.minBound != value) _mesh = null;

                _voxelData.minBound = value;
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

        public VoxelTools.MaterialMode materialMode
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

            _mesh = VoxelTools.ConvertToMesh(voxelData, _materialMode, uvScale, ref _voxelValueToMaterialId);

            return _mesh;
        }

        public void Set(int x, int y, int z, byte val)
        {
            _voxelData.data[x + (y * _voxelData.gridSize.x) + (z * _voxelData.gridSize.x * _voxelData.gridSize.y)] = val;
        }

        public void Set(VoxelData<byte> data)
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