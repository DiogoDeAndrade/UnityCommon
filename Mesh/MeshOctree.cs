using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshOctree : Octree<Triangle>
{
    public MeshOctree(Vector3 min, Vector3 max, int nLevels) : base(min, max, nLevels)
    {
    }

    public void AddTriangle(Triangle triangle)
    {
        if (!AddTriangle(rootNode, triangle))
        {
            Debug.LogError("Can't bin triangle in octree!");
        }
    }

    bool AddTriangle(Node node, Triangle triangle)
    {
        if (node.bounds.IntersectTriangle(triangle))
        {
            if (node.isLeaf)
            {
                node.objects.Add(triangle);
                return true;
            }
            else
            {
                bool b = false;
                for (int i = 0; i < node.children.Length; i++)
                {
                    b |= AddTriangle(node.children[i], triangle);
                }
                return b;
            }
        }
        else
        {
            // SANITY CHECK
            /*var p0 = triangle.GetVertex(0);
            var p1 = triangle.GetVertex(1);
            var p2 = triangle.GetVertex(2);

            var m0 = node.bounds.min;
            var m1 = node.bounds.max;

            if (((p0.x >= m0.x) && (p0.x <= m1.x) && (p0.y >= m0.y) && (p0.y <= m1.y) && (p0.z >= m0.z) && (p0.z <= m1.z)) ||
                ((p1.x >= m0.x) && (p1.x <= m1.x) && (p1.y >= m0.y) && (p1.y <= m1.y) && (p1.z >= m0.z) && (p1.z <= m1.z)) ||
                ((p2.x >= m0.x) && (p2.x <= m1.x) && (p2.y >= m0.y) && (p2.y <= m1.y) && (p2.z >= m0.z) && (p2.z <= m1.z)))
            {
                int a = 10;
                a++;

                bool b = node.bounds.IntersectTriangle(triangle);
            }*/
        }
        return false;
    }

    //public bool         addGizmo = false;
    //public Matrix4x4    gizmoTransform;

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, ref Triangle triangle, ref float t)
    {
        /*if (addGizmo)
        {
            DebugGizmo.AddSphere("Type=Raycast", origin, 0.05f, Color.magenta, gizmoTransform);
            DebugGizmo.AddLine("Type=Raycast", origin, origin + dir * maxDist, Color.magenta, gizmoTransform);
        }*/

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
                    if (tri.normal.y > 0)
                    {
                        int a = 10;
                        a++;
                    }

                    if (tri.Raycast(origin, dir, maxDist, out d))
                    {
                        //if ((addGizmo) && (tri.normal.y > 0)) DebugGizmo.AddTriangle("Type=Raycast", tri, Color.green, gizmoTransform);

                        if (d < t)
                        {
                            ret = true;
                            triangle = tri;
                            t = d;
                        }
                    }
                    else
                    {
                        //if ((addGizmo) && (tri.normal.y > 0)) DebugGizmo.AddTriangle("Type=Raycast", tri, Color.red, gizmoTransform);
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
