using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelNavMesh
{
    public enum TriangulationMesh { Simplification, EarClipping };

    Mesh mesh;
    Mesh unsimplifiedMesh;

    public void Build(VoxelData vd, List<int> validVoxels, 
                      TriangulationMesh method, bool simplifyBoundary,
                      float stepSize, bool simplify, bool keepUnsimplifiedMesh)
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();

        int FindOrAdd(Vector3 p, float stepSize)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (p.xz() == vertices[i].xz())
                {
                    if (Mathf.Abs(p.y - vertices[i].y) <= stepSize)
                    {
                        if (p.y < vertices[i].y)
                        {
                            vertices[i] = p;
                        }
                        return i;
                    }
                }
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

                        int i1 = FindOrAdd(p1, stepSize);
                        int i2 = FindOrAdd(p2, stepSize);
                        int i3 = FindOrAdd(p3, stepSize);
                        int i4 = FindOrAdd(p4, stepSize);

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
            if (method == TriangulationMesh.Simplification)
            {
                if (keepUnsimplifiedMesh) unsimplifiedMesh = mesh;
                mesh = MeshTools.SimplifyMeshInterior(mesh, 0.01f);
            }
            else if (method == TriangulationMesh.EarClipping)
            {
                // Get boundary and build triangulation from there
                var topology = new Topology(mesh, Matrix4x4.identity);
                var boundary = topology.GetBoundary();

                Debug.LogError("Ear clipping simplification not implemented!");

//                mesh = MeshTools.TriangulateEarClipping(boundary.Get(0));
            }
        }
    }

    public Mesh GetMesh()
    {
        return mesh;
    }
    public Mesh GetUnsimplifiedMesh()
    {
        return unsimplifiedMesh;
    }
}
