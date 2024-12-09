using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class Graph<N> where N : IEquatable<N>
{
    [Serializable]
    public class Edge
    {
        public int i1;
        public int i2;
        public float weight;
    };

    [Serializable]
    public class Node
    {
        public N node;
    };
    [SerializeField] protected List<Node> nodes = new();
    [SerializeField] protected List<Edge> edges = new();
    [SerializeField] protected bool directed;

    public int nodeCount => (nodes != null) ? (nodes.Count) : 0;
    public bool nodeExists(int i) => nodes[i] != null;
    public N GetNode(int i) => (nodes[i] == null) ? (default(N)) : (nodes[i].node);

    public int edgeCount => (edges != null) ? (edges.Count) : 0;
    public Edge GetEdge(int i) => edges[i];

    public Edge GetEdge(int i1, int i2)
    {
        int edgeId = FindEdge(i1, i2);
        if (edgeId == -1) return null;

        return edges[edgeId];
    }

    public bool isDirected => directed;

    public Graph(bool directed = false)
    {
        this.directed = directed;
    }

    public int Add(N node)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null)
            {
                nodes[i] = new Node { node = node };
                return i;
            }
        }

        nodes.Add(new Node { node = node });
        return nodes.Count - 1;
    }

    public int Add(int i1, int i2, float w = 1.0f)
    {
        int edgeId = FindEdge(i1, i2);
        if (edgeId == -1)
        {
            var newEdge = new Edge
            {
                i1 = i1,
                i2 = i2,
                weight = w
            };

            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i] == null)
                {
                    edges[i] = newEdge;
                    return i;
                }
            }

            edges.Add(newEdge);
            return edges.Count - 1;
        }
        
        return edgeId;
    }

    public (int nodeId1, int nodeId2, int edgeId) Add(N n1, N n2, float w = 1.0f)
    {
        int i1 = FindOrAddNode(n1);
        int i2 = FindOrAddNode(n2);

        return (i1, i2, Add(i1, i2, w));
    }

    public int FindEdge(int i1, int i2)
    {
        if (directed)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null) continue;
                if ((edge.i1 == i1) && (edge.i2 == i2)) return i;
            }
        }
        else
        {
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null) continue;
                if (((edge.i1 == i1) && (edge.i2 == i2)) ||
                    ((edge.i2 == i1) && (edge.i1 == i2))) return i;
            }
        }

        return -1;
    }

    public int FindNode(N n)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;
            if (nodes[i].Equals(n)) return i;
        }

        return -1;
    }

    public int FindOrAddNode(N n)
    {
        int index = FindNode(n);
        if (index != -1) return index;

        return Add(n);
    }

    public delegate float WeightFunction(Graph<N> graph, int n1, int n2);

    public void MakeComplete(WeightFunction weightFunc)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;
            for (int j = (isDirected) ? (0) : (i + 1); j < nodes.Count; j++)
            {
                if (nodes[j] == null) continue;

                if (i == j) continue;

                Add(i, j, weightFunc(this, i, j));
            }
        }
    }

    public float DijkstraDistance(int n1, int n2)
    {
        var distances = Enumerable.Repeat(float.MaxValue, nodeCount).ToArray();
        distances[n1] = 0;

        var pq = new SortedSet<(float distance, int vertex)>();
        pq.Add((0, n1));

        while (pq.Any())
        {
            var (currentDistance, currentVertex) = pq.First();
            pq.Remove(pq.First());

            if (currentVertex == n2) return currentDistance;

            foreach (var edge in edges.Where(e => e.i1 == currentVertex || e.i2 == currentVertex))
            {
                if (edge == null) continue;

                int neighbor = edge.i1 == currentVertex ? edge.i2 : edge.i1;
                float newDistance = currentDistance + edge.weight;

                if (newDistance < distances[neighbor])
                {
                    pq.Remove((distances[neighbor], neighbor));
                    distances[neighbor] = newDistance;
                    pq.Add((newDistance, neighbor));
                }
            }
        }

        return float.MaxValue; // Target not reachable
    }

    public List<int> DijkstraShortestPath(int start, int target)
    {
        var distances = Enumerable.Repeat(float.MaxValue, nodeCount).ToArray();
        var previous = Enumerable.Repeat(-1, nodeCount).ToArray();
        var visited = new HashSet<int>();
        var pq = new SortedSet<(float distance, int node)>();

        distances[start] = 0;
        pq.Add((0, start));

        while (pq.Count > 0)
        {
            var (currentDistance, currentNode) = pq.Min;
            pq.Remove(pq.Min);

            if (currentNode == target)
            {
                // Reconstruct the path
                var path = new List<int>();
                for (int at = target; at != -1; at = previous[at])
                {
                    path.Add(at);
                }
                path.Reverse();
                return path;
            }

            visited.Add(currentNode);

            var neighbourEdges = (directed) ? 
                                    (edges.Where(e => (e != null) && (e.i1 == currentNode))) : 
                                    (edges.Where(e => (e != null) && ((e.i1 == currentNode) || (e.i2 == currentNode))));
            foreach (var edge in neighbourEdges)
            {
                if (edge == null) continue;

                int neighbor = edge.i1 == currentNode ? edge.i2 : edge.i1;

                if (visited.Contains(neighbor)) continue;

                float newDist = distances[currentNode] + edge.weight;
                if (newDist < distances[neighbor])
                {
                    pq.Remove((distances[neighbor], neighbor));
                    distances[neighbor] = newDist;
                    previous[neighbor] = currentNode;
                    pq.Add((newDist, neighbor));
                }
            }
        }

        return null; // Target not reachable
    }

    public Graph<N> FindMinimumSpanningTree_Kruskal()
    {
        var sortedEdges = edges.Where(e => e != null).OrderBy(e => e.weight).ToList();
        var mst = new Graph<N>();
        foreach (var node in nodes)
        {
            mst.Add(node.node);
        }

        var parent = Enumerable.Range(0, nodeCount).ToArray();
        int Find(int v) => parent[v] == v ? v : (parent[v] = Find(parent[v]));
        void Union(int u, int v) => parent[Find(u)] = Find(v);

        foreach (var edge in sortedEdges)
        {
            if (Find(edge.i1) != Find(edge.i2))
            {
                mst.Add(edge.i1, edge.i2, edge.weight);
                Union(edge.i1, edge.i2);
            }
        }

        return mst;
    }

    public void CopyNodesFrom(Graph<N> srcGraph)
    {
        nodes.Clear();
        foreach (var node in srcGraph.nodes)
        {
            Add(node.node);
        }
    }

    public int Degree(int nodeId)
    {
        if (directed) return InDegree(nodeId) + OutDegree(nodeId);

        return edges.Count(e => (e.i1 == nodeId) || (e.i2 == nodeId));
    }

    public int InDegree(int nodeId)
    {
        if (!directed) return Degree(nodeId);

        return edges.Count(e => (e.i2 == nodeId));
    }

    public int OutDegree(int nodeId)
    {
        if (!directed) return Degree(nodeId);

        return edges.Count(e => (e.i1 == nodeId));
    }

    public List<int> GetLeaves()
    {
        var leaves = new List<int>();

        if (directed)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                // Calculate in-degree and out-degree
                int inDegree = edges.Count(e => e.i2 == i);
                int outDegree = edges.Count(e => e.i1 == i);

                // Check if the node is a leaf (only one incoming or one outgoing edge)
                if ((inDegree == 1 && outDegree == 0) || (inDegree == 0 && outDegree == 1))
                {
                    leaves.Add(i);
                }
            }
        }
        else
        {
            for (int i = 0; i < nodeCount; i++)
            {
                var degree = edges.Count(e => (e != null) && (e.i1 == i || e.i2 == i));
                if (degree == 1) // A leaf has exactly one connection
                {
                    leaves.Add(i);
                }
            }
        }

        return leaves;
    }

    public void SetNodeToNull(int nodeId)
    {
        // Iterate through the edges and set to null any edge connected to the leaf
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (edge == null) continue;
            if ((edge.i1 == nodeId) || (edge.i2 == nodeId))
            {
                edges[i] = null;
            }
        }

        nodes[nodeId] = null;
    }

    public void RemoveNode(int nodeId)
    {
        edges.RemoveAll(e => e.i1 == nodeId || e.i2 == nodeId);

        // Need to re-index edges
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (edge == null) continue;
            if (edge.i1 > nodeId) edge.i1--;
            if (edge.i2 > nodeId) edge.i2--;
        }

        nodes.RemoveAt(nodeId);
    }

    public void RemoveUnusedNodes()
    {
        bool changesMade = false;
        do
        {
            changesMade = false;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (Degree(i) == 0)
                {
                    RemoveNode(i);
                    changesMade = true;
                    break;
                }
            }

        }
        while (changesMade);
    }

    public List<int> FindLinkedNodes(int nodeId)
    {
        var ret = new List<int>();
        foreach (var edge in edges)
        {
            if (edge == null) continue;

            if (edge.i1 == nodeId) ret.Add(edge.i2);
            else if (edge.i2 == nodeId) ret.Add(edge.i1);
        }
        return ret;
    }
}
