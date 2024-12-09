using SnapMeshPCG;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SteinerTree
{
    public static Graph<T> Build<T>(Graph<T> srcGraph, List<int> terminalPoints) where T : IEquatable<T>
    {
        var G1 = BuildCompleteFromTerminals(srcGraph, terminalPoints);

        var mst1 = G1.FindMinimumSpanningTree_Kruskal();

        var newGraph = ConvertDistanceLinksToPaths(mst1, srcGraph, terminalPoints);

        var mst2 = newGraph.FindMinimumSpanningTree_Kruskal();

        PruneNonTerminalLeaves(mst2, terminalPoints);

        return mst2;
    }

    static Graph<T> BuildCompleteFromTerminals<T>(Graph<T> srcGraph, List<int> terminalPoints) where T : IEquatable<T>
    {
        Graph<T> newGraph = new(false);

        foreach (var nodeId in terminalPoints)
        {
            newGraph.Add(srcGraph.GetNode(nodeId));
        }

        newGraph.MakeComplete((_, n1, n2) => srcGraph.DijkstraDistance(n1, n2));

        return newGraph;
    }

    static Graph<T> ConvertDistanceLinksToPaths<T>(Graph<T> srcGraph, Graph<T> originalGraph, List<int> terminalPoints) where T : IEquatable<T>
    {
        // Create a new graph to represent the subgraph G_s
        Graph<T> newGraph = new(false);

        // Add all nodes from the original graph
        newGraph.CopyNodesFrom(originalGraph);

        // Process edges in the current MST
        for (int edgeId = 0; edgeId < srcGraph.edgeCount; edgeId++)
        {
            var edge = srcGraph.GetEdge(edgeId);
            // These indexes are on the new graph, but it has correspondence with the indices on the terminal points
            int i1 = edge.i1;
            int i2 = edge.i2;

            i1 = terminalPoints[i1];
            i2 = terminalPoints[i2];

            // Use Dijkstra to find the shortest path between these two nodes
            List<int> path = originalGraph.DijkstraShortestPath(i1, i2);

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

        return newGraph;
    }

    static void PruneNonTerminalLeaves<T>(Graph<T> srcGraph, List<int> terminalPoints) where T : IEquatable<T>
    {
        // Create a set of terminal nodes for quick lookup
        var terminalSet = new HashSet<int>(terminalPoints);

        // Iterate until no more changes are made
        bool madeChanges;
        do
        {
            madeChanges = false;

            // Find all leaves in the graph
            var leaves = srcGraph.GetLeaves();

            foreach (var leaf in leaves)
            {
                // If the leaf is not a terminal, mark it for removal
                if (!terminalSet.Contains(leaf))
                {
                    srcGraph.RemoveNode(leaf);
                    madeChanges = true;
                    // Need to change IDs of nodes in terminal point list > leaf
                    var newHashSet = new HashSet<int>();
                    foreach (var tp in terminalSet)
                    {
                        if (tp > leaf) newHashSet.Add(tp - 1);
                        else newHashSet.Add(tp);
                    }
                    terminalSet = newHashSet;
                    break;
                }
            }
        } while (madeChanges);

        srcGraph.RemoveUnusedNodes();
    }
}
