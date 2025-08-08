using System;
using UnityEngine;

namespace UC
{

    [System.Serializable]
    public class VoxelData<DATA_TYPE> where DATA_TYPE : struct, IEquatable<DATA_TYPE>
    {
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

        public DATA_TYPE this[int x, int y, int z]
        {
            get { return data[x + y * gridSize.x + z * gridSize.x * gridSize.y]; }
            set { data[x + y * gridSize.x + z * gridSize.x * gridSize.y] = value; }
        }


        public DATA_TYPE this[Vector3Int p]
        {
            get { return data[p.x + p.y * gridSize.x + p.z * gridSize.x * gridSize.y]; }
            set { data[p.x + p.y * gridSize.x + p.z * gridSize.x * gridSize.y] = value; }
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
    }

    public class VoxelDataByte : VoxelData<byte>
    {

    }
}