using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GraphComponent : MonoBehaviour
{
    public enum WeightMode { Constant, Distance, Explicit };

    [SerializeField] private bool                       directed = false;
    [SerializeField] private WeightMode                 weightMode;
    [SerializeField, Header("Test data")]
    private List<int>   terminalPoints;
    [SerializeField, ShowIf("weightMode", WeightMode.Explicit)]
    private List<float> edgeWeights;

    [SerializeField, Header("Data")] 
    private Graph<GraphNodeComponent>  graph;
    [SerializeField] 
    private Graph<GraphNodeComponent>  originalGraph;

    public bool isDirected => directed;

    [Button("Build from components")]
    void BuildFromComponents()
    {
        var nodes = GetComponentsInChildren<GraphNodeComponent>();

        graph = new(directed);

        foreach (GraphNodeComponent node in nodes)
        {
            node.id = graph.Add(node);
        }

        int edgeIndex = 0;
        foreach (GraphNodeComponent node in nodes)
        {
            var links = node.GetLinks();
            foreach (var link in links) 
            {
                if (link != null)
                {
                    float w = 1.0f;
                    if (weightMode == WeightMode.Distance) w = Vector3.Distance(node.transform.position, link.transform.position);
                    else if (weightMode == WeightMode.Explicit)
                    {
                        if ((edgeWeights != null) && (edgeWeights.Count > edgeIndex))
                            w = edgeWeights[edgeIndex];
                        else
                            w = float.MaxValue;
                    }
                    graph.Add(node, link, w);

                    edgeIndex++;
                }
            }
        }

        originalGraph = graph;
    }

    [Button("Build complete graph from terminals with Dijkstra")]
    void BuildCompleteFromTerminals()
    {
        Graph<GraphNodeComponent> newGraph = new(false);

        if (graph == null)
        {
            Debug.LogWarning("Need a graph to be able to build complete from terminal points");
            return;
        }
        if ((terminalPoints == null) || (terminalPoints.Count== 0))
        {
            Debug.LogWarning("Define terminal points!");
            return;
        }
        foreach (var nodeId in terminalPoints)
        {
            newGraph.Add(graph.GetNode(nodeId));
        }

        newGraph.MakeComplete((_, n1, n2) => graph.DijkstraDistance(n1, n2));

        graph = newGraph;
    }

    [Button("Build MST with Kruskal's algorithm")]
    void BuildMST_Kruskal()
    {
        graph = graph.FindMinimumSpanningTree_Kruskal();
    }

    [Button("Convert distance links to paths")]
    void ConvertDistanceLinksToPaths()
    {
        // Create a new graph to represent the subgraph G_s
        Graph<GraphNodeComponent> newGraph = new(directed);

        // Add all nodes from the original graph
        newGraph.CopyNodesFrom(originalGraph);

        // Process edges in the current MST
        for (int edgeId = 0; edgeId < graph.edgeCount; edgeId++)
        {
            var edge = graph.GetEdge(edgeId);
            // Get the original nodes connected by this edge
            var node1 = graph.GetNode(edge.i1);
            var node2 = graph.GetNode(edge.i2);

            // Use Dijkstra to find the shortest path between these two nodes
            List<int> path = originalGraph.DijkstraShortestPath(edge.i1, edge.i2);

            if (path != null && path.Count > 1)
            {
                // Add each edge in the shortest path to the subgraph
                for (int i = 0; i < path.Count - 1; i++)
                {
                    int u = path[i];
                    int v = path[i + 1];
                    float weight = originalGraph.GetEdge(u, v).weight;

                    newGraph.Add(u, v, weight);
                }
            }
        }

        // Replace the current graph with the constructed subgraph
        graph = newGraph;
    }

    [Button("Prune Non-Terminal Leaves")]
    void PruneNonTerminalLeaves()
    {
        if (graph == null)
        {
            Debug.LogWarning("Graph is null! Build or load the graph first.");
            return;
        }

        // Create a set of terminal nodes for quick lookup
        var terminalSet = new HashSet<int>(terminalPoints);

        // Iterate until no more changes are made
        bool madeChanges;
        do
        {
            madeChanges = false;

            // Find all leaves in the graph
            var leaves = graph.GetLeaves();

            foreach (var leaf in leaves)
            {
                // If the leaf is not a terminal, mark it for removal
                if (!terminalSet.Contains(leaf))
                {
                    graph.RemoveNode(leaf);
                    madeChanges = true;
                }
            }
        } while (madeChanges);

        graph.RemoveUnusedNodes();
    }

    private void OnDrawGizmosSelected()
    {
        if (graph != null)
        {
            Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.5f);
            for (int i = 0; i < graph.nodeCount; i++)
            {
                var node = graph.GetNode(i);
                if (node == null) continue;

                Gizmos.DrawSphere(node.transform.position, node.radius);
                DebugHelpers.DrawTextAt(node.transform.position, Vector3.zero, 16, Color.white, $"{node.id}", true);
            }

            Gizmos.color = Color.cyan;
            for (int j = 0; j < graph.edgeCount; j++)
            {
                var edge = graph.GetEdge(j);
                if (edge == null) continue;
                var n1 = graph.GetNode(edge.i1);
                var n2 = graph.GetNode(edge.i2);

                Vector3 delta = n2.transform.position - n1.transform.position;
                float deltaMag = delta.magnitude;
                Vector3 dir = delta / deltaMag;

                Vector3 p1 = n1.transform.position + dir * n1.radius;
                Vector3 p2 = n2.transform.position - dir * n2.radius;

                if (graph.isDirected)
                {
                    Vector3 d = (p2 - p1);
                    float mag = d.magnitude;
                    d /= mag;
                    DebugHelpers.DrawArrow(p1, d, mag, 0.05f * mag, 45.0f);
                }
                else
                {
                    Gizmos.DrawLine(p1, p2);
                }

                DebugHelpers.DrawTextAt((p1 + p2) * 0.5f, Vector3.zero, 14, Color.blue, $"{edge.weight}", false);
            }
        }
    }
}
