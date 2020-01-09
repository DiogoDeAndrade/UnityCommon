using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeWorldGen
{
    int     sectionSizeX;
    int     sectionSizeY;
    Vector3 tileSize;

    public CubeWorldGen(int sectionSizeX, int sectionSizeY, Vector3 tileSize)
    {
        this.sectionSizeX = sectionSizeX;
        this.sectionSizeY = sectionSizeY;
        this.tileSize = tileSize;
    }

    public void GetMeshes(Heightmap heightmap, ref List<Mesh> meshes)
    {
        if (((heightmap.sizeX % sectionSizeX) != 0) ||
            ((heightmap.sizeY % sectionSizeY) != 0))
        {
            Debug.LogError("Section size must be divider of heightmap size!");
            return;
        }

        int sectionCountX = heightmap.sizeX / sectionSizeX;
        int sectionCountY = heightmap.sizeY / sectionSizeY;

        for (int sectionY = 0; sectionY < sectionCountY; sectionY++)
        {
            for (int sectionX = 0; sectionX < sectionCountX; sectionX++)
            {
                List<Vector3> verts = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int>     triangles0 = new List<int>();
                List<int>     triangles1 = new List<int>();

                for (int dy = 0; dy < sectionSizeX; dy++)
                {
                    int tileY = sectionY * sectionSizeY + dy;

                    for (int dx = 0; dx < sectionSizeX; dx++)
                    {
                        int     tileX = sectionX * sectionSizeX + dx;
                        float   selfHeight = heightmap.Get(tileX, tileY);
                        Vector3 center = new Vector3((tileX + 0.5f) * tileSize.x, selfHeight * tileSize.y, (tileY + 0.5f) * tileSize.z);

                        AddQuad(center, Vector3.right * tileSize.x, Vector3.forward * tileSize.z, verts, uvs, triangles0, 1);
                    }
                }

                Mesh mesh = new Mesh();
                mesh.name = "Section_" + sectionX + "_" + sectionY;
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles0, 0);
                if (triangles1.Count > 0) mesh.SetTriangles(triangles1, 1);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.UploadMeshData(true);
                meshes.Add(mesh);
            }
        }
    }

    void AddQuad(Vector3 center, Vector3 axisX, Vector3 axisZ, List<Vector3> verts, List<Vector2> uvs, List<int> triangles, int tesselation)
    {
        if (tesselation == 1)
        {
            Vector3 v1 = center - axisX * 0.5f - axisZ * 0.5f;
            Vector3 v2 = center - axisX * 0.5f + axisZ * 0.5f;
            Vector3 v3 = center + axisX * 0.5f + axisZ * 0.5f;
            Vector3 v4 = center + axisX * 0.5f - axisZ * 0.5f;
            int     idx = verts.Count;
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);
            verts.Add(v4);

            uvs.Add(new Vector2(0.0f, 1.0f));
            uvs.Add(new Vector2(0.0f, 0.0f));
            uvs.Add(new Vector2(1.0f, 0.0f));
            uvs.Add(new Vector2(1.0f, 1.0f));

            triangles.Add(idx); triangles.Add(idx + 1); triangles.Add(idx + 2);
            triangles.Add(idx); triangles.Add(idx + 2); triangles.Add(idx + 3);
        }
        else
        {
            throw new System.NotImplementedException();
        }
    }
}
