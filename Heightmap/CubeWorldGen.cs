using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeWorldGen
{
    int     sectionSizeX;
    int     sectionSizeY;
    Vector3 tileSize;
    bool    reuse_vertex = true;
    bool    noise = false;
    Vector3 noise_frequency;
    float   noise_amplitude;

    public CubeWorldGen(int sectionSizeX, int sectionSizeY, Vector3 tileSize)
    {
        this.sectionSizeX = sectionSizeX;
        this.sectionSizeY = sectionSizeY;
        this.tileSize = tileSize;
    }

    public void SetNoise(Vector3 frequency, float amplitude)
    {
        noise = true;
        noise_frequency = frequency;
        noise_amplitude = amplitude;
    }

    public void SetReuse(bool b)
    {
        reuse_vertex = b;
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

                        AddQuad(center, Vector3.right * tileSize.x, Vector3.forward * tileSize.z, verts, uvs, triangles0, 1, reuse_vertex);

                        float rightHeight = heightmap.SafeGet(tileX + 1, tileY, 0);
                        float leftHeight = heightmap.SafeGet(tileX - 1, tileY,0);
                        float downHeight = heightmap.SafeGet(tileX, tileY + 1, 0);
                        float upHeight = heightmap.SafeGet(tileX, tileY - 1, 0);

                        if (rightHeight < selfHeight)
                        {
                            float deltaHeight = (selfHeight - rightHeight) * tileSize.y; 
                            Vector3 rightCenter = new Vector3((tileX + 1.0f) * tileSize.x, rightHeight + deltaHeight * 0.5f, (tileY + 0.5f) * tileSize.z);
                            AddQuad(rightCenter, Vector3.forward * tileSize.z, Vector3.up * deltaHeight, verts, uvs, triangles1, 1, reuse_vertex);
                        }
                        
                        if (leftHeight < selfHeight)
                        {
                            float deltaHeight = (selfHeight - leftHeight) * tileSize.y;
                            Vector3 leftCenter = new Vector3(tileX * tileSize.x, leftHeight + deltaHeight * 0.5f, (tileY + 0.5f) * tileSize.z);
                            AddQuad(leftCenter, Vector3.up * tileSize.y, Vector3.forward * deltaHeight, verts, uvs, triangles1, 1, reuse_vertex);
                        }

                        if (downHeight < selfHeight)
                        {
                            float deltaHeight = (selfHeight - downHeight) * tileSize.y;
                            Vector3 downCenter = new Vector3((tileX + 0.5f) * tileSize.x, downHeight + deltaHeight * 0.5f, (tileY + 1.0f) * tileSize.z);
                            AddQuad(downCenter, Vector3.up * deltaHeight, Vector3.right * tileSize.z, verts, uvs, triangles1, 1, reuse_vertex);
                        }

                        if (upHeight < selfHeight)
                        {
                            float deltaHeight = (selfHeight - upHeight) * tileSize.y;
                            Vector3 downCenter = new Vector3((tileX + 0.5f) * tileSize.x, upHeight + deltaHeight * 0.5f, tileY * tileSize.z);
                            AddQuad(downCenter, Vector3.right * tileSize.z, Vector3.up * deltaHeight, verts, uvs, triangles1, 1, reuse_vertex);
                        }
                    }
                }

                Mesh mesh = new Mesh();
                mesh.name = "Section_" + sectionX + "_" + sectionY;
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(triangles0, 0);
                mesh.SetTriangles(triangles1, 1);
                mesh.RecalculateNormals();

                if (noise)
                {
                    Vector3[] normals = mesh.normals;

                    for (int i = 0; i < verts.Count; i++)
                    {
                        Vector3 pos = verts[i];

                        float magnitude = Mathf.PerlinNoise(pos.x * noise_frequency.x + pos.z * noise_frequency.z, pos.y * noise_frequency.y - pos.z * noise_frequency.z);

                        if (reuse_vertex)
                        {
                            verts[i] = pos + normals[i].x0z() * magnitude * noise_amplitude;
                        }
                        else
                        {
                            Vector3 direction = new Vector3(
                                                        Mathf.PerlinNoise(pos.x * noise_frequency.x * 1.3f  + pos.z * noise_frequency.z * 0.5f, pos.y * noise_frequency.y * 1.2f - pos.z * noise_frequency.z * 3.321f),
                                                        0.0f,
                                                        Mathf.PerlinNoise(pos.x * noise_frequency.x * 1.23f + pos.z * noise_frequency.z * 0.72f, pos.y * noise_frequency.y * 3.4f - pos.z * noise_frequency.z * 1.234f));
                            direction.Normalize();

                            verts[i] = pos + direction * noise_amplitude;
                        }
                    }

                    mesh.SetVertices(verts);
                    mesh.RecalculateNormals();
                }

                mesh.RecalculateTangents();

                mesh.UploadMeshData(false);
                meshes.Add(mesh);
            }
        }
    }

    void AddQuad(Vector3 center, Vector3 axisX, Vector3 axisZ, List<Vector3> verts, List<Vector2> uvs, List<int> triangles, int tesselation, bool reuse)
    {
        if (tesselation == 1)
        {
            Vector3 v1 = center - axisX * 0.5f - axisZ * 0.5f;
            Vector3 v2 = center - axisX * 0.5f + axisZ * 0.5f;
            Vector3 v3 = center + axisX * 0.5f + axisZ * 0.5f;
            Vector3 v4 = center + axisX * 0.5f - axisZ * 0.5f;
            Vector2 uv1 = new Vector2(0.0f, 1.0f);
            Vector2 uv2 = new Vector2(0.0f, 0.0f);
            Vector2 uv3 = new Vector2(1.0f, 0.0f);
            Vector2 uv4 = new Vector2(1.0f, 1.0f);

            if (reuse_vertex)
            { 
                int     i1 = GetOrAdd(verts, uvs, v1, uv1);
                int     i2 = GetOrAdd(verts, uvs, v2, uv2);
                int     i3 = GetOrAdd(verts, uvs, v3, uv3);
                int     i4 = GetOrAdd(verts, uvs, v4, uv4);

                triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                triangles.Add(i1); triangles.Add(i3); triangles.Add(i4);
            }
            else
            {
                int idx = verts.Count;
                verts.Add(v1); uvs.Add(uv1);
                verts.Add(v2); uvs.Add(uv2);
                verts.Add(v3); uvs.Add(uv3);
                verts.Add(v4); uvs.Add(uv4);

                triangles.Add(idx); triangles.Add(idx + 1); triangles.Add(idx + 2);
                triangles.Add(idx); triangles.Add(idx + 2); triangles.Add(idx + 3);
            }
        }
        else
        {
            throw new System.NotImplementedException();
        }
    }

    int GetOrAdd(List<Vector3> verts, List<Vector2> uvs, Vector3 pos, Vector2 uv)
    {
        int index = 0;
        foreach (var v in verts)
        {
            if (v == pos) return index;
            index++;
        }

        verts.Add(pos);
        uvs.Add(uv);
        return verts.Count - 1;
    }
}
