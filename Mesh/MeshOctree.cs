using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshOctree : Octree<Triangle>
{
    public    Mesh    sharedMesh;

    public MeshOctree(Vector3 min, Vector3 max, int nLevels) : base(min, max, nLevels)
    {
    }

    public void AddTriangle(Triangle triangle)
    {
        AddTriangle(rootNode, triangle);
    }

    void AddTriangle(Node node, Triangle triangle)
    {
        if (node.bounds.IntersectTriangle(triangle))
        {
            if (node.isLeaf)
            {
                node.objects.Add(triangle);
            }
            else
            {
                for (int i = 0; i < node.children.Length; i++)
                {
                    AddTriangle(node.children[i], triangle);
                }
            }
        }
    }

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, ref Triangle triangle, ref float t)
    {
        t = float.MaxValue;
        return Raycast(rootNode, origin, dir, maxDist, ref triangle, ref t);
    }

    bool Raycast(Node node, Vector3 origin, Vector3 dir, float maxDist, ref Triangle triangle, ref float t)
    {
        float d;
        if (node.bounds.IntersectRay(new Ray(origin, dir), out d))
        {
            bool ret = false;

            if (d > maxDist)
            {
                return false;
            }

            if (node.isLeaf)
            {
                foreach (var tri in node.objects)
                {
                    if (tri.Raycast(origin, dir, maxDist, out d))
                    {
                        if (d < t)
                        {
                            ret = true;
                            triangle = tri;
                            t = d;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < node.children.Length; i++)
                {
                    ret |= Raycast(node.children[i], origin, dir, maxDist, ref triangle, ref t);
                }
            }

            return ret;
        }

        return false;
    }

    public bool RaycastWithGizmos(Vector3 origin, Vector3 dir, float maxDist, ref Triangle triangle, ref float t)
    {
        t = float.MaxValue;
        return RaycastWithGizmos(rootNode, origin, dir, maxDist, ref triangle, ref t);
    }

    bool RaycastWithGizmos(Node node, Vector3 origin, Vector3 dir, float maxDist, ref Triangle triangle, ref float t)
    {
        float d;
        if (node.bounds.IntersectRay(new Ray(origin, dir), out d))
        {
            bool ret = false;

            if (d > maxDist)
            {
                return false;
            }

            if (node.isLeaf)
            {
                foreach (var tri in node.objects)
                {
                    Gizmos.color = Color.green;
                    tri.DrawGizmo();

                    if (tri.Raycast(origin, dir, maxDist, out d))
                    {
                        if (d < t)
                        {
                            ret = true;
                            triangle = tri;
                            t = d;
                        }
                    }
                }
            }
            else
            {
                Gizmos.color = Color.yellow;
                node.bounds.DrawGizmo();

                for (int i = 0; i < node.children.Length; i++)
                {
                    ret |= RaycastWithGizmos(node.children[i], origin, dir, maxDist, ref triangle, ref t);
                }
            }

            return ret;
        }

        return false;
    }

    public void DrawGizmos(int maxLevel)
    {
        DrawGizmos(rootNode, maxLevel);
    }

    void DrawGizmos(Node node, int maxLevel)
    {
        if (maxLevel == 0) return;

        node.bounds.DrawGizmo();

        if (!node.isLeaf)
        {
            for (int i = 0; i < node.children.Length; i++)
            {
                DrawGizmos(node.children[i], maxLevel - 1);
            }
        }
    }
}
