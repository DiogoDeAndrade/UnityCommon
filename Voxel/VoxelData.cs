using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VoxelData
{
    [HideInInspector]
    public byte[]       data;
    public Vector3Int   gridSize;
    public Vector3      voxelSize;
    public Vector3      offset;
    public Vector3      uvScale;

    public Vector3      size => new Vector3(gridSize.x * voxelSize.x, gridSize.y * voxelSize.y, gridSize.z * voxelSize.z);
    public Vector3      extents => size * 0.5f;
    public Bounds       bounds
    {
        get
        {
            var s = size;
            return new Bounds(offset + s * 0.5f, s);
        }
    }

    public byte this[int x,int y, int z]
    {
        get { return data[x + y * gridSize.x + z * gridSize.x * gridSize.y]; }
        set { data[x + y * gridSize.x + z * gridSize.x * gridSize.y] = value; }
    }


    public byte this[Vector3Int p]
    {
        get { return data[p.x + p.y * gridSize.x + p.z * gridSize.x * gridSize.y]; }
        set { data[p.x + p.y * gridSize.x + p.z * gridSize.x * gridSize.y] = value; }
    }

    public void Replace(byte src, byte dest)
    {
        for (int i  = 0; i < data.Length; i++)
        {
            if (data[i] == src) data[i] = dest;
        }
    }
}
