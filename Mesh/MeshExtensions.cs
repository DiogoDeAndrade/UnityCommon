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
}
