using System;
using System.Collections.Generic;
using UnityEngine;
#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif   

namespace UC.ED
{
    public enum BindingMode { ClosestOne, NearestK };
    public enum GraphLinkMode { PartitionAdjacency, SharedBindings };

    [Serializable]
    public class EDNode
    {
        public Vector3      restPosition;
        public List<int>    neighbors = new();

        public Vector3      currentTranslation = Vector3.zero;
        public Quaternion   currentRotation = Quaternion.identity;
        public Matrix4x4    currentMatrix;

        public void UpdateMatrix()
        {
            currentMatrix = Matrix4x4.TRS(currentTranslation, currentRotation, Vector3.one);
        }

        public Vector3 currentPosition => currentMatrix.MultiplyPoint(restPosition);
    }

    [Serializable]
    public struct EDVertexBinding
    {
        public int[] nodeIndices;   // k nearest
        public float[] weights;     // normalized
    }

    [Serializable]
    public struct EDHandleConstraint
    {
        public Matrix4x4 restHandleMatrix;
        public Matrix4x4 currentHandleMatrix;
        public List<int> vertexIndices;
    }

    [Serializable]
    public struct EDVertexConstraint
    {
        public int      vertexIndex;
        public Vector3  targetPosition;
    }

    [Serializable]
    public class EmbededDeformation
    {
        public Vector3[]                restVertices;
        public int[]                    triangles;

        public List<EDNode>             nodes = new();
        public EDVertexBinding[]        bindings;
        public List<EDHandleConstraint> handleConstraints = new();
        public List<EDVertexConstraint> vertexConstraints = new();

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
            handleConstraints.Clear();

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

        public void UpdateConstraints(List<EDHandleConstraint> handleData)
        {
            handleConstraints = new(handleData);

            vertexConstraints.Clear();

            foreach (var hc in handleData)
            {
                Matrix4x4 delta = hc.currentHandleMatrix * hc.restHandleMatrix.inverse;

                foreach (int vId in hc.vertexIndices)
                {
                    Vector3 restPos = restVertices[vId];
                    Vector3 targetPos = delta.MultiplyPoint3x4(restPos);

                    vertexConstraints.Add(new EDVertexConstraint
                    {
                        vertexIndex = vId,
                        targetPosition = targetPos
                    });
                }
            }
        }

        public void ResetDeformation()
        {
            foreach (var node in nodes)
            {
                node.currentTranslation = Vector3.zero;
                node.currentRotation = Quaternion.identity;
                node.UpdateMatrix();
            }
        }

        public Vector3[] DeformVerticesFromCurrentNodeTransforms()
        {
            foreach (var node in nodes) node.UpdateMatrix();

            Vector3[] deformed = new Vector3[restVertices.Length];

            for (int vId = 0; vId < restVertices.Length; vId++)
            {                
                deformed[vId] = DeformVertexFromCurrentNodeTransforms(vId);
            }

            return deformed;
        }

        public Vector3 DeformVertexFromCurrentNodeTransforms(int vertexId)
        {
            Vector3 v = restVertices[vertexId];
            var binding = bindings[vertexId];
            Vector3 result = Vector3.zero;

            for (int i = 0; i < binding.nodeIndices.Length; i++)
            {
                int nodeIndex = binding.nodeIndices[i];
                float w = binding.weights[i];

                var node = nodes[nodeIndex];

                Vector3 g = node.restPosition;

                Vector3 transformed = node.currentMatrix.MultiplyPoint3x4(v - g) + g;

                result += w * transformed;
            }

            return result;
        }

        public bool SolveTranslationsOnly(double constraintWeight = 1.0, double smoothnessWeight = 0.1)
        {
#if MATH_NET_AVAILABLE
            if ((nodes == null) || (nodes.Count == 0))
            {
                Debug.LogError("SolveTranslationsOnly failed: no nodes.");
                return false;
            }

            if ((bindings == null) || (bindings.Length != restVertices.Length))
            {
                Debug.LogError("SolveTranslationsOnly failed: bindings are missing or invalid.");
                return false;
            }

            if ((vertexConstraints == null) || (vertexConstraints.Count == 0))
            {
                Debug.LogWarning("SolveTranslationsOnly: no vertex constraints, resetting translations.");
                for (int i = 0; i < nodes.Count; i++)
                {
                    nodes[i].currentTranslation = Vector3.zero;
                    nodes[i].currentRotation = Quaternion.identity;
                    nodes[i].UpdateMatrix();
                }
                return true;
            }

            int nodeCount = nodes.Count;

            // Count unique undirected graph edges.
            List<(int a, int b)> edges = CollectUniqueEdges();
            int edgeCount = edges.Count;

            int constraintRowCount = vertexConstraints.Count;
            int smoothRowCount = edgeCount;
            int rowCount = constraintRowCount + smoothRowCount;

            if (rowCount == 0)
            {
                Debug.LogWarning("SolveTranslationsOnly: system has zero rows.");
                return false;
            }

            Matrix<double> A = DenseMatrix.Create(rowCount, nodeCount, 0.0);
            Vector<double> bx = DenseVector.Create(rowCount, 0.0);
            Vector<double> by = DenseVector.Create(rowCount, 0.0);
            Vector<double> bz = DenseVector.Create(rowCount, 0.0);

            int row = 0;

            // -----------------------------------------------------------------
            // 1) Positional constraints
            //     sum_j w_j(v) * t_j = target(v) - rest(v)
            // -----------------------------------------------------------------
            for (int c = 0; c < vertexConstraints.Count; c++, row++)
            {
                EDVertexConstraint vc = vertexConstraints[c];

                if ((vc.vertexIndex < 0) || (vc.vertexIndex >= restVertices.Length))
                    continue;

                Vector3 rest = restVertices[vc.vertexIndex];
                Vector3 delta = vc.targetPosition - rest;

                EDVertexBinding binding = bindings[vc.vertexIndex];
                if ((binding.nodeIndices == null) || (binding.nodeIndices.Length == 0))
                    continue;

                for (int k = 0; k < binding.nodeIndices.Length; k++)
                {
                    int nodeIndex = binding.nodeIndices[k];
                    if ((nodeIndex < 0) || (nodeIndex >= nodeCount))
                        continue;

                    double w = 0.0;

                    if ((binding.weights != null) && (k < binding.weights.Length))
                        w = binding.weights[k];
                    else
                        w = 1.0 / binding.nodeIndices.Length;

                    A[row, nodeIndex] += constraintWeight * w;
                }

                bx[row] = constraintWeight * delta.x;
                by[row] = constraintWeight * delta.y;
                bz[row] = constraintWeight * delta.z;
            }

            // -----------------------------------------------------------------
            // 2) Smoothness constraints
            //     t_i - t_j = 0
            // -----------------------------------------------------------------
            for (int e = 0; e < edgeCount; e++, row++)
            {
                var edge = edges[e];

                A[row, edge.a] += smoothnessWeight;
                A[row, edge.b] -= smoothnessWeight;

                // rhs stays zero
            }

            // -----------------------------------------------------------------
            // 3) Solve least squares independently for x/y/z
            // -----------------------------------------------------------------
            Vector<double> tx, ty, tz;

            try
            {
                tx = A.Solve(bx);
                ty = A.Solve(by);
                tz = A.Solve(bz);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SolveTranslationsOnly failed while solving: {ex.Message}");
                return false;
            }

            // -----------------------------------------------------------------
            // 4) Store result in nodes
            // -----------------------------------------------------------------
            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i].currentTranslation = new Vector3((float)tx[i], (float)ty[i], (float)tz[i]);
                nodes[i].currentRotation = Quaternion.identity;
                nodes[i].UpdateMatrix();
            }

            return true;
#else
            throw new NotImplementedException();            
#endif

        }

        private List<(int a, int b)> CollectUniqueEdges()
        {
            List<(int a, int b)> result = new();
            HashSet<ulong> seen = new();

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].neighbors == null)
                    continue;

                for (int j = 0; j < nodes[i].neighbors.Count; j++)
                {
                    int n = nodes[i].neighbors[j];
                    if ((n < 0) || (n >= nodes.Count) || (n == i))
                        continue;

                    int a = Mathf.Min(i, n);
                    int b = Mathf.Max(i, n);

                    ulong key = ((ulong)(uint)a << 32) | (uint)b;
                    if (seen.Add(key))
                        result.Add((a, b));
                }
            }

            return result;
        }
    }
}
