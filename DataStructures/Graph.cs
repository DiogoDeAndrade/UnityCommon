using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC
{

    [Serializable]
    public class Graph<N> where N : IEquatable<N>
    {
        public delegate int SelectTreeStartPointFunc(Graph<N> graph, List<int> candidates);
        public delegate float BuildTreeCriteria(int nodeId);

        public enum TreeBuildMode { Random, Degree, Centrality };
        public enum ComputeCentralityMode { Degree, Closeness, Betweenness, Harmonic };

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
        [SerializeField] protected List<int> degree;
        [SerializeField] protected List<float> centrality;

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

        public void Add(Tree<N> tree)
        {
            // Add a tree as a component of this graph
            Dictionary<int, int> nodeIds = new();
            for (int i = 0; i < tree.nodeCount; i++)
            {
                var parentNodeId = tree.GetParent(i);
                if ((parentNodeId == -1) && (i != tree.rootNodeId)) continue;
                nodeIds.Add(i, Add(tree.GetNode(i)));
            }

            AddEdgesFromTree(tree, tree.rootNodeId, nodeIds);
        }

        private void AddEdgesFromTree(Tree<N> tree, int nodeId, Dictionary<int, int> nodeIds)
        {
            foreach (var children in tree.GetChildren(nodeId))
            {
                Add(nodeIds[nodeId], nodeIds[children]);
                AddEdgesFromTree(tree, children, nodeIds);
            }
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
                var (currentDistance, currenfloat) = pq.First();
                pq.Remove(pq.First());

                if (currenfloat == n2) return currentDistance;

                foreach (var edge in edges.Where(e => e.i1 == currenfloat || e.i2 == currenfloat))
                {
                    if (edge == null) continue;

                    int neighbor = edge.i1 == currenfloat ? edge.i2 : edge.i1;
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

        public float ClosenessCentrality(int nodeId)
        {
            float accumReciprocDist = 0.0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i != nodeId)
                {
                    float d = DijkstraDistance(nodeId, i);
                    if (d != float.MaxValue) accumReciprocDist += 1.0f / d;
                    else accumReciprocDist += d;
                }
            }

            return accumReciprocDist;
        }

        public float HarmonicCentrality(int nodeId)
        {
            float accumReciprocDist = 0.0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i != nodeId)
                {
                    float d = DijkstraDistance(nodeId, i);
                    if (d != float.MaxValue) accumReciprocDist += 1.0f / d;
                }
            }

            return accumReciprocDist;
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

        public List<int> FindOutgoingNodes(int nodeId)
        {
            var ret = new List<int>();
            foreach (var edge in edges)
            {
                if (edge == null) continue;

                if (edge.i1 == nodeId) ret.Add(edge.i2);
            }
            return ret;
        }

        public void ComputeDegrees()
        {
            degree = new();
            for (int i = 0; i < nodes.Count; i++)
            {
                degree.Add(Degree(i));
            }
        }
        bool hasComputedDegree => (degree != null) && (degree.Count != nodes.Count);
        float GetCachedDegree(int nodeId) => degree[nodeId];

        public void ComputeCentrality(ComputeCentralityMode mode)
        {
            centrality = new();

            switch (mode)
            {
                case ComputeCentralityMode.Degree:
                    for (int i = 0; i < nodes.Count; i++) centrality.Add(Degree(i));
                    break;
                case ComputeCentralityMode.Closeness:
                    for (int i = 0; i < nodes.Count; i++) centrality.Add(ClosenessCentrality(i));
                    break;
                case ComputeCentralityMode.Betweenness:
                    // Set all to 0
                    for (int i = 0; i < nodes.Count; i++) centrality.Add(0.0f);
                    // Compute all shortest paths, and on each node traversed in each one, increase the count of paths by 1
                    for (int n1 = 0; n1 < nodes.Count; n1++)
                    {
                        for (int n2 = 0; n2 < nodes.Count; n2++)
                        {
                            if (n1 == n2) continue;
                            var path = DijkstraShortestPath(n1, n2);
                            if (path != null)
                            {
                                foreach (var n in path) centrality[n]++;
                            }
                        }
                    }
                    break;
                case ComputeCentralityMode.Harmonic:
                    for (int i = 0; i < nodes.Count; i++) centrality.Add(HarmonicCentrality(i));
                    break;
                default:
                    break;
            }
        }
        public void SetCentrality(List<float> data) { centrality = data; }
        bool hasComputedCentrality => (centrality != null) && (centrality.Count == nodes.Count);

        public float GetCentrality(int nodeId) => centrality[nodeId];

        public List<Tree<N>> BuildTrees(TreeBuildMode mode, SelectTreeStartPointFunc selectTreeStartPointFunc = null)
        {
            var ret = new List<Tree<N>>();

            switch (mode)
            {
                case TreeBuildMode.Random:
                    {
                        var visited = Enumerable.Repeat(false, nodes.Count).ToList();
                        var leafs = GetLeaves();
                        while (leafs.Count > 0)
                        {
                            int startNodeId = leafs.Random(false);
                            var tree = BuildTreeFromNode(startNodeId, visited);
                            if (tree != null)
                            {
                                ret.Add(tree);
                            }
                        }
                    }
                    break;
                case TreeBuildMode.Degree:
                case TreeBuildMode.Centrality:
                    {
                        BuildTreeCriteria criteria = null;

                        if (mode == TreeBuildMode.Degree)
                        {
                            if (!hasComputedDegree) ComputeDegrees();

                            criteria = GetCachedDegree;
                        }
                        else if (mode == TreeBuildMode.Centrality)
                        {
                            if (!hasComputedCentrality) ComputeCentrality(ComputeCentralityMode.Degree);

                            criteria = GetCentrality;
                        }

                        var visited = Enumerable.Repeat(false, nodes.Count).ToList();
                        bool allVisited = false;
                        while (!allVisited)
                        {
                            float maxValue = -float.MaxValue;
                            List<int> candidates = new List<int>();
                            for (int i = 0; i < nodes.Count; i++)
                            {
                                if (visited[i]) continue;
                                if (criteria(i) > maxValue)
                                {
                                    maxValue = criteria(i);
                                    candidates = new List<int>() { i };
                                }
                                else if (criteria(i) == maxValue) candidates.Add(i);
                            }

                            if (candidates.Count == 0) break;

                            int startNodeId = -1;
                            if (candidates.Count > 1)
                            {
                                if (selectTreeStartPointFunc == null) startNodeId = candidates.Random();
                                else startNodeId = selectTreeStartPointFunc(this, candidates);
                            }
                            else startNodeId = candidates[0];

                            var tree = BuildTreeFromNode(startNodeId, visited);
                            if (tree != null)
                            {
                                ret.Add(tree);
                            }

                            allVisited = visited.All(v => v);
                        }
                    }
                    break;
                default:
                    break;
            }

            return ret;
        }

        private Tree<N> BuildTreeFromNode(int startNodeId, List<bool> visited)
        {
            if (visited[startNodeId]) return null;

            visited[startNodeId] = true;

            Tree<N> tree = new Tree<N>(nodes[startNodeId].node);

            AddTreeNodesFromGraphNodes(tree, startNodeId, tree.rootNodeId, visited);

            return tree;
        }

        private void AddTreeNodesFromGraphNodes(Tree<N> tree, int graphNodeId, int treeParentNodeId, List<bool> visited)
        {
            List<int> neighbours = (directed) ? (FindOutgoingNodes(graphNodeId)) : (FindLinkedNodes(graphNodeId));

            foreach (var n in neighbours)
            {
                // Ignore already visited nodes (avoid loops)
                if (visited[n]) continue;
                visited[n] = true;

                // Create a node on the tree
                var newNodeId = tree.AddNode(nodes[n].node, treeParentNodeId);
                AddTreeNodesFromGraphNodes(tree, n, newNodeId, visited);
            }
        }

        public bool HasLink(int i, int j)
        {
            if (directed)
            {
                foreach (var edge in edges)
                {
                    if ((edge.i1 == i) && (edge.i2 == j)) return true;
                }
            }
            else
            {
                foreach (var edge in edges)
                {
                    if (((edge.i1 == i) && (edge.i2 == j)) ||
                        ((edge.i1 == j) && (edge.i2 == i))) return true;
                }
            }

            return false;
        }

        public float GetWeigth(int i, int j)
        {
            if (directed)
            {
                foreach (var edge in edges)
                {
                    if ((edge.i1 == i) && (edge.i2 == j)) return edge.weight;
                }
            }
            else
            {
                foreach (var edge in edges)
                {
                    if (((edge.i1 == i) && (edge.i2 == j)) ||
                        ((edge.i1 == j) && (edge.i2 == i))) return edge.weight;
                }
            }

            return 0.0f;
        }
    }
}