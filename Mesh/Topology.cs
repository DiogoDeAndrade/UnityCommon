using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Topology
{
    public class Edge
    {
        public int              id;
        public int              i1, i2;
        public List<Triangle>   triangles;

        public int GetSharedVertex(Edge otherEdge)
        {
            if (i1 == otherEdge.i1) return i1;
            if (i2 == otherEdge.i1) return i2;
            if (i1 == otherEdge.i2) return i1;
            if (i2 == otherEdge.i2) return i2;
            return -1;
        }

        public bool Contains(int index)
        {
            return (i1 == index) || (i2 == index);
        }
    }

    public class Triangle
    {
        public int  id;
        public int  v1, v2, v3;
        public Edge e1, e2, e3;
    }

    public List<Vector3>    vertices;
    public int              nEdges;
    public List<Edge>       edges;
    public int              nTriangles;
    public List<Triangle>   triangles;

    public Topology(Mesh mesh)
    {
        vertices = new List<Vector3>(mesh.vertices);
        triangles = new List<Triangle>();
        edges = new List<Edge>();
        nEdges = 0;
        nTriangles = 0;

        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
        {
            var indices = mesh.GetIndices(submesh);
            for (int i = 0; i < indices.Length; i += 3)
            {
                Triangle tri = new Triangle();
                tri.id = triangles.Count;
                tri.v1 = indices[i];
                tri.v2 = indices[i + 1];
                tri.v3 = indices[i + 2];
                tri.e1 = FindOrAddEdge(tri.v1, tri.v2);
                tri.e2 = FindOrAddEdge(tri.v2, tri.v3);
                tri.e3 = FindOrAddEdge(tri.v3, tri.v1);
                tri.e1.triangles.Add(tri);
                tri.e2.triangles.Add(tri);
                tri.e3.triangles.Add(tri);

                triangles.Add(tri);

                nTriangles++;
            }
        }
    }

    public Edge FindEdge(int i1, int i2)
    {
        foreach (var edge in edges)
        {
            if (edge == null) continue;
            if (((edge.i1 == i1) && (edge.i2 == i2)) ||
                ((edge.i2 == i1) && (edge.i1 == i2)))
            {
                return edge;
            }
        }

        return null;
    }
    public Edge FindEdgeExcluding(int i1, int i2, Edge excludedEdge)
    {
        foreach (var edge in edges)
        {
            if (edge == null) continue;
            if (edge == excludedEdge) continue;
            if (((edge.i1 == i1) && (edge.i2 == i2)) ||
                ((edge.i2 == i1) && (edge.i1 == i2)))
            {
                return edge;
            }
        }

        return null;
    }

    public Edge FindOrAddEdge(int i1, int i2)
    {
        var edge = FindEdge(i1, i2);
        if (edge == null)
        {
            edge = new Edge() { id = edges.Count, i1 = i1, i2 = i2, triangles = new List<Triangle>() };
            edges.Add(edge);

            nEdges++;
        }

        return edge;
    }

    public bool CheckIfCollapseIntersects(int src, int dest)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            Edge edge = edges[i];
            if (edge == null) continue;
            if (!edge.Contains(src)) continue;

            Vector3 p1 = (edge.i1 == src) ? (vertices[dest]) : (vertices[edge.i1]);
            Vector3 p2 = (edge.i2 == src) ? (vertices[dest]) : (vertices[edge.i2]);

            // If this leads to a degenerate edge, no need to check if it will intersect anything else
            if (p1 == p2) continue;

            // Check if this will intersect any other edge
            for (int j = 0; j < edges.Count; j++)
            {
                Edge otherEdge = edges[j];
                if (otherEdge == null) continue;
                if (otherEdge == edge) continue;

                // Check if edges share endpoints
                if (edge.GetSharedVertex(otherEdge) != -1) continue;

                Vector3 p3 = (otherEdge.i1 == src) ? (vertices[dest]) : (vertices[otherEdge.i1]);
                Vector3 p4 = (otherEdge.i2 == src) ? (vertices[dest]) : (vertices[otherEdge.i2]);

                // Nudge vectors so that they don't detect intersections at the points where it's supposed to intersect
                Vector3 delta = (p4 - p3).normalized * 0.0001f;
                p3 = p3 + delta;
                p4 = p4 - delta;

                // Check if this edge intersects the other one
                Vector3 intersection;
                if (Line.Intersect(p1, p2, p3, p4, out intersection))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void CollapseEdge(int src, int dest)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            Edge edge = edges[i];
            if (edge == null) continue;
            if (!edge.Contains(src)) continue;

            if (edge.i1 == src) edge.i1 = dest; 
            else if (edge.i2 == src) edge.i2 = dest;

            if (edge.i1 == edge.i2)
            {
                // Edge is degenerate - Remove this edge and remove any triangles that use this edge
                edges[i] = null;
                nEdges--;
                foreach (var tri in edge.triangles)
                {
                    if (triangles[tri.id] != null)
                    {
                        triangles[tri.id] = null;
                        nTriangles--;
                    }
                }
            }
            else
            {
                // Change vertices in triangles of this edge
                foreach (var tri in edge.triangles)
                {
                    if (tri.v1 == src) tri.v1 = dest;
                    if (tri.v2 == src) tri.v2 = dest;
                    if (tri.v3 == src) tri.v3 = dest;
                }

                // Check if this edge already exists under another name
                var otherEdge = FindEdgeExcluding(edge.i1, edge.i2, edge);
                if (otherEdge != null)
                {
                    // It exists, so move triangles from this edge to the other and remove it
                    foreach (var tri in edge.triangles)
                    {
                        if (tri.e1 == edge) tri.e1 = otherEdge;
                        if (tri.e2 == edge) tri.e2 = otherEdge;
                        if (tri.e3 == edge) tri.e3 = otherEdge;

                        if (!otherEdge.triangles.Contains(tri))
                        {
                            otherEdge.triangles.Add(tri);
                        }
                    }
                        
                    edges[edge.id] = null;
                    nEdges--;
                }
                else
                {
                    // Don't do anything, this edge is (probably) still valid
                }
            }
        }

        for (int i = 0; i < edges.Count; i++)
        {
            Edge edge = edges[i];
            if (edge == null) continue;

            edge.triangles.RemoveAll((t) => triangles[t.id] == null);
        }

        // Remove vertex src
        RemoveVertex(src);
    }

    private void RemoveVertex(int vertex)
    {
        foreach (var edge in edges)
        {
            if (edge == null) continue;

            if (edge.i1 > vertex) edge.i1--;
            if (edge.i2 > vertex) edge.i2--;
        }
        foreach (var tri in triangles)
        {
            if (tri == null) continue;

            if (tri.v1 > vertex) tri.v1--;
            if (tri.v2 > vertex) tri.v2--;
            if (tri.v3 > vertex) tri.v3--;
        }

        vertices.RemoveAt(vertex);
    }

    public HashSet<int> GetBoundary()
    {
        var pinnedVertex = new HashSet<int>();
        foreach (var edge in edges)
        {
            if (edge == null) continue;
            if (edge.triangles.Count == 1)
            {
                pinnedVertex.Add(edge.i1);
                pinnedVertex.Add(edge.i2);
            }
        }

        return pinnedVertex;
    }

    public void OptimizeBoundary(float cosTolerance, bool checkIntersections)
    {
        // Collapse colinear boundary edges
        bool exit = false;
        while (!exit)
        {
            exit = true;

            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null) continue;
                if (edge.triangles.Count != 1) continue;

                for (int j = i + 1; j < edges.Count; j++)
                {
                    var otherEdge = edges[j];
                    if (otherEdge == null) continue;
                    if (otherEdge == edge) continue;
                    if (otherEdge.triangles.Count != 1) continue;

                    int sharedVertex = edge.GetSharedVertex(otherEdge);
                    if (sharedVertex != -1)
                    {
                        // They share endpoints, check their directions
                        Vector3 d1 = (vertices[edge.i1] - vertices[edge.i2]).normalized;
                        Vector3 d2 = (vertices[otherEdge.i1] - vertices[otherEdge.i2]).normalized;

                        float dp = Mathf.Abs(Vector3.Dot(d1, d2));
                        if (dp >= cosTolerance)
                        {
                            // Colinear, collapse
                            int src = sharedVertex;
                            int dest;
                            if (edge.i1 == sharedVertex)
                            {
                                dest = edge.i2;
                            }
                            else
                            {
                                dest = edge.i1;
                            }

                            bool validCollapse = true;
                            if (checkIntersections)
                            {
                                validCollapse = !CheckIfCollapseIntersects(src, dest);
                            }

                            if (validCollapse)
                            {
                                CollapseEdge(src, dest);
                                exit = false;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    public void OptimizeInterior()
    {
        // Mark vertices as "pinned" if they belong to outside (so we don't move one of them to 
        // the inside)
        //t0 = stopwatch.ElapsedMilliseconds;
        var pinnedVertex = GetBoundary();
        //Debug.Log("Get boundary = " + (stopwatch.ElapsedMilliseconds - t0));

        // Collapse interior edges
        bool exit = false;
        while (!exit)
        {
            exit = true;

            // For all edges
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                // If this edge is not valid, continue
                if (edge == null) continue;

                // If this edge is not shared by triangles, it is an "outside" edge, don't change anything about it
                if (edge.triangles.Count == 1) continue;

                // Edge is interior and shared by triangles
                int srcVertex = edge.i1;
                int destVertex = edge.i2;

                // Edge doesn't include pinned vertices (ones on the edge)
                if (pinnedVertex.Contains(srcVertex))
                {
                    // srcVertex is pinned, do not move it, move the other one
                    srcVertex = edge.i2;
                    destVertex = edge.i1;

                    // Check if new source is pinned
                    if (pinnedVertex.Contains(srcVertex))
                    {
                        // Both vertices on this edge are pinned, so can't collapse this edge
                        // Carry on
                        continue;
                    }
                }

                //t0 = stopwatch.ElapsedMilliseconds;
                CollapseEdge(srcVertex, destVertex);
                //accumCollapse += (stopwatch.ElapsedMilliseconds - t0);
                exit = false;

                // Recompute pinned vertices
                //t0 = stopwatch.ElapsedMilliseconds;
                var tmp = pinnedVertex.ToList();
                for (int j = 0; j < tmp.Count; j++)
                {
                    if (tmp[j] > srcVertex) tmp[j]--;
                }
                pinnedVertex = new HashSet<int>(tmp);
                //accumPinned += (stopwatch.ElapsedMilliseconds - t0);
            }
        }//*/
    }
}
