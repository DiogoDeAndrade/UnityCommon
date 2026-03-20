using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.ED
{
    public enum BindingMode { ClosestOne, NearestK };
    public enum GraphLinkMode { PartitionAdjacency, SharedBindings };

    [Serializable]
    public class EDNode
    {
        public Vector3 restPosition;
        public List<int> neighbors = new();
    }

    [Serializable]
    public struct EDVertexBinding
    {
        public int[] nodeIndices;   // k nearest
        public float[] weights;     // normalized
    }

    [Serializable]
    public struct EDConstraint
    {
        public int vertexIndex;
        public Vector3 targetPosition;
    }

    [Serializable]
    public class EmbededDeformation
    {
        public Vector3[]            restVertices;
        public int[]                triangles;

        public List<EDNode>         nodes = new();
        public EDVertexBinding[]    bindings;
        public List<EDConstraint>   constraints = new();

        public void BuildDeformationGraph(TopologyStatic topology, float minDistance, List<int> forcedVertices, BindingMode bindMode, GraphLinkMode graphLinkMode, int k = 4)
        {
            if (topology == null)
            {
                Debug.LogError("BuildDeformationGraph failed: topology is null.");
                return;
            }

            if (minDistance <= 0.0f)
            {
                Debug.LogWarning("BuildDeformationGraph: minDistance <= 0, clamping to a small value.");
                minDistance = 0.001f;
            }

            // -----------------------------------------------------------------
            // 1) Copy source navmesh into ED rest data
            // -----------------------------------------------------------------
            restVertices = topology.GetVertexPositions().ToArray();
            triangles = topology.GetTriangleIndices().ToArray();

            nodes.Clear();
            bindings = null;
            constraints.Clear();

            // -----------------------------------------------------------------
            // 2) Sample graph nodes from navmesh vertices
            //    - forced vertices first
            //    - then radius-pruned fill over remaining vertices
            // -----------------------------------------------------------------
            float minDistanceSq = minDistance * minDistance;
            List<int> sampledVertexIds = new();

            HashSet<int> forcedSet = (forcedVertices != null) ? (new HashSet<int>(forcedVertices)) : (new HashSet<int>());

            // Forced vertices first - min distance is set to 0.0f so that they're always added regardless of distance to each other
            // There are no duplicates for sure, so this code could probabably be optimized a bit, but it's not a big deal since the number of forced vertices is expected to be low.
            foreach (int vId in forcedSet)
            {
                if ((vId < 0) || (vId >= topology.vertexCount))
                    continue;

                TryAddSampleVertex(vId, topology, sampledVertexIds, 0.0f);
            }

            // Fill remaining graph with radius-pruned vertex samples
            for (int vId = 0; vId < topology.vertexCount; vId++)
            {
                if (forcedSet.Contains(vId))
                    continue;

                TryAddSampleVertex(vId, topology, sampledVertexIds, minDistanceSq);
            }

            // Fallback safety
            if ((sampledVertexIds.Count == 0) && (topology.vertexCount > 0))
            {
                sampledVertexIds.Add(0);
            }

            // Create ED nodes
            nodes.Capacity = sampledVertexIds.Count;
            for (int i = 0; i < sampledVertexIds.Count; i++)
            {
                int vId = sampledVertexIds[i];
                nodes.Add(new EDNode
                {
                    restPosition = topology.GetVertexPosition(vId),
                    neighbors = new List<int>()
                });
            }

            // -----------------------------------------------------------------
            // 3) Build bindings: each navmesh vertex gets k nearest nodes
            // -----------------------------------------------------------------
            BuildBindings(topology, bindMode, k);

            // -----------------------------------------------------------------
            // 4) Build graph edges from shared bindings
            // -----------------------------------------------------------------
            switch (graphLinkMode)
            {
                case GraphLinkMode.PartitionAdjacency:
                    BuildGraphFromPartitionAdjacency(topology);
                    break;

                case GraphLinkMode.SharedBindings:
                    BuildGraphFromBindings();
                    break;
            }

            Debug.Log($"ED graph built. Vertices={topology.vertexCount}, Triangles={topology.triangleCount}, Nodes={nodes.Count}");
        }

        private bool TryAddSampleVertex(int vertexId, TopologyStatic topology, List<int> sampledVertexIds, float minDistanceSq)
        {
            Vector3 p = topology.GetVertexPosition(vertexId);

            for (int i = 0; i < sampledVertexIds.Count; i++)
            {
                Vector3 q = topology.GetVertexPosition(sampledVertexIds[i]);
                if ((p - q).sqrMagnitude < minDistanceSq)
                    return false;
            }

            sampledVertexIds.Add(vertexId);
            return true;
        }

        private int GetClosestNodeIndex(Vector3 p)
        {
            int bestIndex = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                float dSq = (nodes[i].restPosition - p).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void AddUndirectedNeighbor(int a, int b)
        {
            if (a < 0 || b < 0 || a == b)
                return;

            if (!nodes[a].neighbors.Contains(b))
                nodes[a].neighbors.Add(b);

            if (!nodes[b].neighbors.Contains(a))
                nodes[b].neighbors.Add(a);
        }

        private void EnsureNoIsolatedNodes()
        {
            if (nodes.Count <= 1)
                return;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].neighbors.Count > 0)
                    continue;

                int bestJ = -1;
                float bestDistSq = float.MaxValue;
                Vector3 p = nodes[i].restPosition;

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j)
                        continue;

                    float dSq = (nodes[j].restPosition - p).sqrMagnitude;
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestJ = j;
                    }
                }

                if (bestJ >= 0)
                    AddUndirectedNeighbor(i, bestJ);
            }
        }

        private void BuildBindings(TopologyStatic topology, BindingMode bindMode, int k)
        {
            if (topology == null)
            {
                Debug.LogError("BuildBindings failed: topology is null.");
                return;
            }

            if ((nodes == null) || (nodes.Count == 0))
            {
                Debug.LogError("BuildBindings failed: no ED nodes exist.");
                return;
            }

            int nodeCount = nodes.Count;
            int vertexCount = topology.vertexCount;
            bindings = new EDVertexBinding[vertexCount];

            switch (bindMode)
            {
                case BindingMode.ClosestOne:
                    {
                        for (int vId = 0; vId < vertexCount; vId++)
                        {
                            Vector3 p = topology.GetVertexPosition(vId);
                            int closestNode = GetClosestNodeIndex(p);

                            bindings[vId] = new EDVertexBinding
                            {
                                nodeIndices = new int[] { closestNode },
                                weights = new float[] { 1.0f }
                            };
                        }
                    }
                    break;

                case BindingMode.NearestK:
                    {
                        int actualK = Mathf.Clamp(k, 1, nodeCount);

                        for (int vId = 0; vId < vertexCount; vId++)
                        {
                            Vector3 p = topology.GetVertexPosition(vId);

                            int[] bestIndices = new int[actualK];
                            float[] bestDistSq = new float[actualK];

                            for (int i = 0; i < actualK; i++)
                            {
                                bestIndices[i] = -1;
                                bestDistSq[i] = float.MaxValue;
                            }

                            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                            {
                                float dSq = (nodes[nodeIndex].restPosition - p).sqrMagnitude;

                                for (int slot = 0; slot < actualK; slot++)
                                {
                                    if (dSq < bestDistSq[slot])
                                    {
                                        for (int shift = actualK - 1; shift > slot; shift--)
                                        {
                                            bestDistSq[shift] = bestDistSq[shift - 1];
                                            bestIndices[shift] = bestIndices[shift - 1];
                                        }

                                        bestDistSq[slot] = dSq;
                                        bestIndices[slot] = nodeIndex;
                                        break;
                                    }
                                }
                            }

                            float[] weights = new float[actualK];
                            float w = 1.0f / actualK;
                            for (int i = 0; i < actualK; i++)
                                weights[i] = w;

                            bindings[vId] = new EDVertexBinding
                            {
                                nodeIndices = bestIndices,
                                weights = weights
                            };
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"BuildBindings: unsupported link mode {bindMode}.");
                    break;
            }
        }

        private void BuildGraphFromBindings()
        {
            if (bindings == null || bindings.Length == 0)
            {
                Debug.LogWarning("BuildGraphFromBindings: no bindings available.");
                return;
            }

            // Clear previous neighbors
            for (int i = 0; i < nodes.Count; i++)
                nodes[i].neighbors.Clear();

            // For each vertex, connect every pair of nodes that influence it
            for (int vId = 0; vId < bindings.Length; vId++)
            {
                int[] indices = bindings[vId].nodeIndices;
                if (indices == null)
                    continue;

                for (int i = 0; i < indices.Length; i++)
                {
                    int a = indices[i];
                    if (a < 0)
                        continue;

                    for (int j = i + 1; j < indices.Length; j++)
                    {
                        int b = indices[j];
                        if ((b < 0) || (a == b))
                            continue;

                        AddUndirectedNeighbor(a, b);
                    }
                }
            }
        }

        private void BuildGraphFromPartitionAdjacency(TopologyStatic topology)
        {
            if (bindings == null || bindings.Length == 0)
            {
                Debug.LogWarning("BuildGraphFromPartitionAdjacency: no bindings available.");
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
                nodes[i].neighbors.Clear();

            for (int edgeId = 0; edgeId < topology.edgeCount; edgeId++)
            {
                var edge = topology.GetEdgeVertex(edgeId);

                int n0 = bindings[edge.i1].nodeIndices[0];
                int n1 = bindings[edge.i2].nodeIndices[0];

                if (n0 != n1)
                    AddUndirectedNeighbor(n0, n1);
            }

            EnsureNoIsolatedNodes();
        }
    }
}
