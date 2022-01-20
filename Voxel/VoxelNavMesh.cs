using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelNavMesh
{
    Mesh mesh;

    public void Build(VoxelData vd, List<int> validVoxels, bool simplify, int maxSimplificationIterations)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();

        int FindOrAdd(Vector3 p)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (p == vertices[i]) return i;
            }

            vertices.Add(p);
            return vertices.Count - 1;
        }

        int index = 0;
        for (int z = 0; z < vd.gridSize.z; z++)
        {
            for (int y = 0; y < vd.gridSize.y; y++)
            {
                for (int x = 0; x < vd.gridSize.x; x++)
                {
                    byte value = vd.data[index];

                    if (validVoxels.Exists((v) => v == value))
                    {
                        // Generate geometry for the top of this voxel for the nav mesh
                        Vector3 p1 = new Vector3((x - 0.0f) * vd.voxelSize.x + vd.offset.x,
                                                 (y + 1.0f) * vd.voxelSize.y + vd.offset.y,
                                                 (z - 0.0f) * vd.voxelSize.z + vd.offset.z);
                        Vector3 p2 = new Vector3((x - 0.0f) * vd.voxelSize.x + vd.offset.x,
                                                 (y + 1.0f) * vd.voxelSize.y + vd.offset.y,
                                                 (z + 1.0f) * vd.voxelSize.z + vd.offset.z);
                        Vector3 p3 = new Vector3((x + 1.0f) * vd.voxelSize.x + vd.offset.x,
                                                 (y + 1.0f) * vd.voxelSize.y + vd.offset.y,
                                                 (z + 1.0f) * vd.voxelSize.z + vd.offset.z);
                        Vector3 p4 = new Vector3((x + 1.0f) * vd.voxelSize.x + vd.offset.x,
                                                 (y + 1.0f) * vd.voxelSize.y + vd.offset.y,
                                                 (z - 0.0f) * vd.voxelSize.z + vd.offset.z);

                        int i1 = FindOrAdd(p1);
                        int i2 = FindOrAdd(p2);
                        int i3 = FindOrAdd(p3);
                        int i4 = FindOrAdd(p4);

                        indices.Add(i1); indices.Add(i2); indices.Add(i3);
                        indices.Add(i1); indices.Add(i3); indices.Add(i4);
                    }

                    index++;
                }
            }
        }

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();

        if (simplify)
        {
            mesh = MeshTools.SimplifyMeshInterior(mesh, maxSimplificationIterations);
        }
    }

    public Mesh GetMesh()
    {
        return mesh;
    }
}
