using System.Collections.Generic;
using UnityEngine;

static class MeshExtensions
{
    public static Mesh Clone(this Mesh src)
    {
        Mesh ret = new Mesh();

        ret.vertices = src.vertices;
        ret.normals = src.normals;
        ret.uv = src.uv;
        ret.uv2 = src.uv2;
        ret.uv3 = src.uv3;
        ret.uv4 = src.uv4;
        ret.uv5 = src.uv5;
        ret.uv6 = src.uv6;
        ret.uv7 = src.uv7;
        ret.uv8 = src.uv8;
        ret.colors = src.colors;
        ret.tangents = src.tangents;
        ret.boneWeights = src.boneWeights;
        ret.triangles = src.triangles;

        return ret;
    }

    public static bool Raycast(this Mesh src, Vector3 origin, Vector3 dir, float maxDist)
    {
        float   t;
        var     vertices = src.vertices;

        for (int submesh = 0; submesh < src.subMeshCount; submesh++)
        {
            var triangles = src.GetTriangles(submesh);

            for (int triIndex = 0; triIndex < triangles.Length; triIndex+=3)
            {
                Triangle tri = new Triangle(vertices[triangles[triIndex]],
                                            vertices[triangles[triIndex + 1]],
                                            vertices[triangles[triIndex + 2]]);

                if (tri.Raycast(origin, dir, maxDist, out t))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool Raycast(this Mesh src, Vector3 origin, Vector3 dir, float maxDist, out int submeshId, out int triHit)
    {
        float t;
        var vertices = src.vertices;

        for (int submesh = 0; submesh < src.subMeshCount; submesh++)
        {
            var triangles = src.GetTriangles(submesh);

            for (int triIndex = 0; triIndex < triangles.Length; triIndex += 3)
            {
                Triangle tri = new Triangle(vertices[triangles[triIndex]],
                                            vertices[triangles[triIndex + 1]],
                                            vertices[triangles[triIndex + 2]]);

                if (tri.Raycast(origin, dir, maxDist, out t))
                {
                    submeshId = submesh;
                    triHit = triIndex / 3;
                    return true;
                }
            }
        }

        submeshId = -1;
        triHit = -1;
        return false;
    }


    public static bool Raycast(this Mesh src, Vector3 origin, Vector3 dir, float maxDist, out int submeshId, out int triHit, out float out_t)
    {
        out_t = float.MaxValue;
        submeshId = -1;
        triHit = -1;

        float t;
        var     vertices = src.vertices;

        for (int submesh = 0; submesh < src.subMeshCount; submesh++)
        {
            var triangles = src.GetTriangles(submesh);

            for (int triIndex = 0; triIndex < triangles.Length; triIndex += 3)
            {
                Triangle tri = new Triangle(vertices[triangles[triIndex]],
                                            vertices[triangles[triIndex + 1]],
                                            vertices[triangles[triIndex + 2]]);

                if (tri.Raycast(origin, dir, maxDist, out t))
                {
                    if (out_t > t)
                    {
                        submeshId = submesh;
                        triHit = triIndex / 3;
                        out_t = t;
                    }
                }
            }
        }

        return (out_t != float.MaxValue);
    }

    public struct RaycastHit
    {
        public int     submeshIndex;
        public int     triIndex;
        public float   t;
    }

    public static RaycastHit[] RaycastAll(this Mesh src, Vector3 origin, Vector3 dir, float maxDist)
    {
        List<RaycastHit>    hits = null;
        float               t;
        var                 vertices = src.vertices;

        for (int submesh = 0; submesh < src.subMeshCount; submesh++)
        {
            var triangles = src.GetTriangles(submesh);

            for (int triIndex = 0; triIndex < triangles.Length; triIndex += 3)
            {
                Triangle tri = new Triangle(vertices[triangles[triIndex]],
                                            vertices[triangles[triIndex + 1]],
                                            vertices[triangles[triIndex + 2]]);

                if (tri.Raycast(origin, dir, maxDist, out t))
                {
                    RaycastHit ri = new RaycastHit
                    {
                        submeshIndex = submesh,
                        triIndex = triIndex / 3,
                        t = t
                    };
                    if (hits == null) hits = new List<RaycastHit>();
                    hits.Add(ri);
                }
            }
        }

        return hits?.ToArray();
    }

    public static Triangle GetTriangle(this Mesh src, int submeshIndex, int triIndex)
    {
        var vertices = src.vertices;
        var triangles = src.GetTriangles(submeshIndex);

        Triangle tri = new Triangle(vertices[triangles[triIndex * 3]],
                                    vertices[triangles[triIndex * 3 + 1]],
                                    vertices[triangles[triIndex * 3 + 2]]);

        return tri;
    }

    public static MeshOctree GetOctree(this Mesh src, int levels = 4)
    {
        return src.GetOctree(Matrix4x4.identity, levels);
    }

    public static MeshOctree GetOctree(this Mesh src, Matrix4x4 matrix, int levels = 4)
    {
        var vertices = src.vertices;

        // Transform vertices
        for (int i = 0; i < src.vertices.Length; i++)
        {
            vertices[i] = matrix * new Vector4(vertices[i].x, vertices[i].y, vertices[i].z, 1);
        }

        var     bounds = src.bounds;
        Vector3 min = matrix * new Vector4(bounds.min.x, bounds.min.y, bounds.min.z, 1);
        Vector3 max = matrix * new Vector4(bounds.max.x, bounds.max.y, bounds.max.z, 1);

        if (min.x > max.x) (min.x, max.x) = (max.x, min.x);
        if (min.y > max.y) (min.y, max.y) = (max.y, min.y);
        if (min.z > max.z) (min.z, max.z) = (max.z, min.z);

        var ret = new MeshOctree(min, max, levels);
        ret.sharedMesh = src;

        for (int submeshIndex = 0; submeshIndex < src.subMeshCount; submeshIndex++)
        {
            var triangles = src.GetTriangles(submeshIndex);

            for (int i = 0; i < triangles.Length; i+=3)
            {
                Triangle tri = new Triangle(vertices[triangles[i]],
                                            vertices[triangles[i + 1]],
                                            vertices[triangles[i + 2]]);

                ret.AddTriangle(tri);
            }
        }

        return ret;
    }
}
