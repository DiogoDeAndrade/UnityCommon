using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;

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
        public DoubleVector3    restPosition;
        public List<int>        neighbors = new();

        [SerializeField, HideInInspector]
        private DoubleVector3       _currentTranslation = DoubleVector3.zero;
        [SerializeField, HideInInspector]
        private DoubleQuaternion    _currentRotation = DoubleQuaternion.identity;
        [SerializeField, HideInInspector]
        private DoubleMatrix3x4     _currentMatrix = DoubleMatrix3x4.identity;
        [SerializeField, HideInInspector]
        private bool                _recomputeMatrix = false;
        [SerializeField, HideInInspector]
        private bool                _recomputeTranslation= false;
        [SerializeField, HideInInspector]
        private bool                _recomputeRotation = false;

        public DoubleVector3 currentTranslation
        {
            get { if (_recomputeTranslation) UpdateTranslationFromMatrix(); return _currentTranslation; }
            set { _currentTranslation = value; _recomputeMatrix = true; _recomputeTranslation = false; }
        }
        public DoubleQuaternion currentRotation
        {
            get { if (_recomputeRotation) UpdateRotationFromMatrix();  return _currentRotation; }
            set { _currentRotation = value; _recomputeMatrix = true; _recomputeRotation = false; }
        }

        public DoubleMatrix3x4 currentMatrix
        {
            get { if (_recomputeMatrix) UpdateMatrixFromTranslationRotation(); return _currentMatrix; }
            set 
            { 
                _currentMatrix =  value; 
                _recomputeMatrix = false;
                _recomputeTranslation = _recomputeRotation = true;
            }
        }

        public DoubleVector3 axisX
        {
            get { var M = currentMatrix; return new DoubleVector3(M.m00, M.m10, M.m20); }
        }
        public DoubleVector3 axisY
        {
            get { var M = currentMatrix; return new DoubleVector3(M.m01, M.m11, M.m21); }
        }
        public DoubleVector3 axisZ
        {
            get { var M = currentMatrix; return new DoubleVector3(M.m02, M.m12, M.m22); }
        }

        private void UpdateTranslationFromMatrix()
        {
            _currentTranslation = _currentMatrix.translation;
            _recomputeTranslation = false;
        }

        private void UpdateRotationFromMatrix()
        {
            _currentRotation = _currentMatrix.rotation;
            _recomputeRotation = false;
        }

        private void UpdateMatrixFromTranslationRotation()
        {
            currentMatrix = DoubleMatrix3x4.TRS(currentTranslation, currentRotation, DoubleVector3.one);
            _recomputeMatrix = false;
        }

        public DoubleVector3 debugNodePosition => restPosition + currentTranslation;
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
                    restPosition = topology.GetVertexPosition(vId).ToDoubleVector3(),
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

        private int GetClosestNodeIndex(DoubleVector3 p)
        {
            int bestIndex = -1;
            double bestDistSq = double.MaxValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                double dSq = (nodes[i].restPosition - p).sqrMagnitude;
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
                double bestDistSq = float.MaxValue;
                DoubleVector3 p = nodes[i].restPosition;

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j)
                        continue;

                    double dSq = (nodes[j].restPosition - p).sqrMagnitude;
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
                            DoubleVector3 p = topology.GetVertexPosition(vId).ToDoubleVector3();

                            int[] bestIndices = new int[actualK];
                            double[] bestDistSq = new double[actualK];

                            for (int i = 0; i < actualK; i++)
                            {
                                bestIndices[i] = -1;
                                bestDistSq[i] = float.MaxValue;
                            }

                            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                            {
                                double dSq = (nodes[nodeIndex].restPosition - p).sqrMagnitude;

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
                node.currentMatrix = Matrix4x4.identity;
            }
        }

        public Vector3[] DeformVerticesFromCurrentNodeTransforms()
        {
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

        public bool SolveTranslationsOnly(double constraintWeight = 1.0, double smoothnessWeight = 0.1, bool resetBeforeSolve = true)
        {
            if (resetBeforeSolve) ResetDeformation();

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
                    nodes[i].currentMatrix = Matrix4x4.identity;
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
            }

            return true;
#else
            throw new NotImplementedException();            
#endif
        }

        private Vector<double> EvaluateResidualVector(double rotationWeight = 1.0, 
                                                      double regularizationWeight = 10.0,  
                                                      double constraintWeight = 100.0)
        {
#if MATH_NET_AVAILABLE
            int nodeCount = nodes.Count;
            int directedEdgeCount = 0;
            for (int i = 0; i < nodes.Count; i++)
                directedEdgeCount += nodes[i].neighbors.Count;
            int constraintCount = vertexConstraints.Count;

            int residualCount = 6 * nodeCount + 3 * directedEdgeCount + 3 * constraintCount;

            Vector<double> residual = DenseVector.Create(residualCount, 0.0);

            double wRot = Math.Sqrt(rotationWeight);
            double wReg = Math.Sqrt(regularizationWeight);
            double wCon = Math.Sqrt(constraintWeight);

            int row = 0;

            // -------------------------------------------------------------
            // 1) Rotation residuals: 6 per node
            //    We extract the 3 basis (3 axis) and build 6 conditions:
            //    1. axisX and axisY are perpendicular
            //    2. axisX and axisZ are perpendicular
            //    3. axisY and axisZ are perpendicular
            //    4. axisX has unit length
            //    5. axisY has unit length
            //    6. axisZ has unit length
            //    This basically states - is this matrix a valid rotation?
            // -------------------------------------------------------------
            for (int i = 0; i < nodeCount; i++)
            {
                var axisX = nodes[i].axisX;
                var axisY = nodes[i].axisY;
                var axisZ = nodes[i].axisZ;

                residual[row++] = wRot * Vector3.Dot(axisX, axisY);
                residual[row++] = wRot * Vector3.Dot(axisX, axisZ);
                residual[row++] = wRot * Vector3.Dot(axisY, axisZ);

                residual[row++] = wRot * (Vector3.Dot(axisX, axisX) - 1.0);
                residual[row++] = wRot * (Vector3.Dot(axisY, axisY) - 1.0);
                residual[row++] = wRot * (Vector3.Dot(axisZ, axisZ) - 1.0);
            }

            // -------------------------------------------------------------
            // 2) Regularization residuals: 3 per undirected edge
            //
            //    R_j (g_k - g_j) + g_j + t_j - (g_k + t_k)
            //    Basically, we find where node j predicts node k should end up
            //    Then we compare (subtract) with the actual position of node k
            //    The idea here is to check if neighbour nodes are moving in a coherent 
            //    fashion.
            // -------------------------------------------------------------
            for (int j = 0; j < nodeCount; j++)
            {
                EDNode nodeJ = nodes[j];
                Vector3 gj = nodeJ.restPosition;
                Vector3 tj = nodeJ.currentTranslation;

                foreach (int k in nodeJ.neighbors)
                {
                    EDNode nodeK = nodes[k];
                    Vector3 gk = nodeK.restPosition;
                    Vector3 tk = nodeK.currentTranslation;

                    Vector3 diff = gk - gj;
                    Vector3 rotatedDiff = nodeJ.currentMatrix.MultiplyVector(diff);

                    Vector3 r = rotatedDiff + gj + tj - (gk + tk);

                    residual[row++] = wReg * r.x;
                    residual[row++] = wReg * r.y;
                    residual[row++] = wReg * r.z;
                }
            }

            // -------------------------------------------------------------
            // 3) Positional constraints: 3 per constrained vertex
            //
            //    deformed(v) - target
            //    Locks positions to anchor points
            // -------------------------------------------------------------
            for (int c = 0; c < constraintCount; c++)
            {
                EDVertexConstraint vc = vertexConstraints[c];

                if ((vc.vertexIndex < 0) || (vc.vertexIndex >= restVertices.Length))
                {
                    residual[row++] = 0.0;
                    residual[row++] = 0.0;
                    residual[row++] = 0.0;
                    continue;
                }

                Vector3 deformed = DeformVertexFromCurrentNodeTransforms(vc.vertexIndex);
                Vector3 r = deformed - vc.targetPosition;

                residual[row++] = wCon * r.x;
                residual[row++] = wCon * r.y;
                residual[row++] = wCon * r.z;
            }

            return residual;
#else
            throw new NotImplementedException();            
#endif
        }

        private int ParamBase(int nodeIndex) => nodeIndex * 12;
#if MATH_NET_AVAILABLE
        private Vector<double> PackNodeParameters()
        {
            int nodeCount = nodes.Count;
            Vector<double> x = DenseVector.Create(nodeCount * 12, 0.0);

            for (int i = 0; i < nodeCount; i++)
            {
                int baseIdx = ParamBase(i);
                Matrix4x4 m = nodes[i].currentMatrix;

                // 3x3 affine block
                x[baseIdx + 0] = m.m00;
                x[baseIdx + 1] = m.m01;
                x[baseIdx + 2] = m.m02;

                x[baseIdx + 3] = m.m10;
                x[baseIdx + 4] = m.m11;
                x[baseIdx + 5] = m.m12;

                x[baseIdx + 6] = m.m20;
                x[baseIdx + 7] = m.m21;
                x[baseIdx + 8] = m.m22;

                // translation
                x[baseIdx + 9] = m.m03;
                x[baseIdx + 10] = m.m13;
                x[baseIdx + 11] = m.m23;
            }

            return x;
        }
#endif

#if MATH_NET_AVAILABLE
        private void ApplyNodeParameters(Vector<double> x)
        {
            int nodeCount = nodes.Count;

            for (int i = 0; i < nodeCount; i++)
            {
                int baseIdx = ParamBase(i);

                Matrix4x4 m = Matrix4x4.identity;

                // 3x3 affine block
                m.m00 = (float)x[baseIdx + 0];
                m.m01 = (float)x[baseIdx + 1];
                m.m02 = (float)x[baseIdx + 2];

                m.m10 = (float)x[baseIdx + 3];
                m.m11 = (float)x[baseIdx + 4];
                m.m12 = (float)x[baseIdx + 5];

                m.m20 = (float)x[baseIdx + 6];
                m.m21 = (float)x[baseIdx + 7];
                m.m22 = (float)x[baseIdx + 8];

                // translation
                m.m03 = (float)x[baseIdx + 9];
                m.m13 = (float)x[baseIdx + 10];
                m.m23 = (float)x[baseIdx + 11];

                nodes[i].currentMatrix = m;
            }
        }

        private Matrix<double> BuildNumericalJacobian(Vector<double> x,
                                                      double rotationWeight = 1.0,
                                                      double regularizationWeight = 10.0,
                                                      double constraintWeight = 100.0,
                                                      double epsilon = 1e-6)
        {
            int paramCount = x.Count;

            // Apply base state
            ApplyNodeParameters(x);

            // Base residual
            Vector<double> f0 = EvaluateResidualVector(rotationWeight, regularizationWeight, constraintWeight);

            int residualCount = f0.Count;

            Matrix<double> J = DenseMatrix.Create(residualCount, paramCount, 0.0);

            // Temporary working vector
            Vector<double> xPerturbed = x.Clone();

            for (int i = 0; i < paramCount; i++)
            {
                double original = xPerturbed[i];

                // Perturb parameter
                xPerturbed[i] = original + epsilon;

                // Apply perturbed parameters
                ApplyNodeParameters(xPerturbed);

                // Evaluate new residual
                Vector<double> f1 = EvaluateResidualVector(rotationWeight, regularizationWeight, constraintWeight);

                // Compute column i
                for (int r = 0; r < residualCount; r++)
                {
                    J[r, i] = (f1[r] - f0[r]) / epsilon;
                }

                // Restore parameter
                xPerturbed[i] = original;
            }

            // Restore original state
            ApplyNodeParameters(x);

            return J;
        }

        private int FillRotationJacobianBlock(Matrix<double> J, int row, int nodeIndex, double wRot)
        {
            int p = ParamBase(nodeIndex);
            var aX = nodes[nodeIndex].axisX;
            var aY = nodes[nodeIndex].axisY;
            var aZ = nodes[nodeIndex].axisZ;

            // r0 = X·Y
            J[row, p + 0] = wRot * aY.x;
            J[row, p + 3] = wRot * aY.y;
            J[row, p + 6] = wRot * aY.z;
            J[row, p + 1] = wRot * aX.x;
            J[row, p + 4] = wRot * aX.y;
            J[row, p + 7] = wRot * aX.z;
            row++;

            // r1 = X·Z
            J[row, p + 0] = wRot * aZ.x;
            J[row, p + 3] = wRot * aZ.y;
            J[row, p + 6] = wRot * aZ.z;
            J[row, p + 2] = wRot * aX.x;
            J[row, p + 5] = wRot * aX.y;
            J[row, p + 8] = wRot * aX.z;
            row++;

            // r2 = Y·Z
            J[row, p + 1] = wRot * aZ.x;
            J[row, p + 4] = wRot * aZ.y;
            J[row, p + 7] = wRot * aZ.z;
            J[row, p + 2] = wRot * aY.x;
            J[row, p + 5] = wRot * aY.y;
            J[row, p + 8] = wRot * aY.z;
            row++;

            // r3 = X·X - 1
            J[row, p + 0] = wRot * 2.0 * aX.x;
            J[row, p + 3] = wRot * 2.0 * aX.y;
            J[row, p + 6] = wRot * 2.0 * aX.z;
            row++;

            // r4 = Y·Y - 1
            J[row, p + 1] = wRot * 2.0 * aY.x;
            J[row, p + 4] = wRot * 2.0 * aY.y;
            J[row, p + 7] = wRot * 2.0 * aY.z;
            row++;

            // r5 = Z·Z - 1
            J[row, p + 2] = wRot * 2.0 * aZ.x;
            J[row, p + 5] = wRot * 2.0 * aZ.y;
            J[row, p + 8] = wRot * 2.0 * aZ.z;
            row++;

            return row;
        }

        private int FillRegularizationJacobianBlock(Matrix<double> J, int row, int nodeJ, int nodeK, double wReg)
        {
            int pj = ParamBase(nodeJ);
            int pk = ParamBase(nodeK);

            Vector3 gj = nodes[nodeJ].restPosition;
            Vector3 gk = nodes[nodeK].restPosition;
            Vector3 d = gk - gj;

            double dx = d.x;
            double dy = d.y;
            double dz = d.z;

            // r.x
            J[row, pj + 0] = wReg * dx;
            J[row, pj + 1] = wReg * dy;
            J[row, pj + 2] = wReg * dz;
            J[row, pj + 9] = wReg * 1.0;
            J[row, pk + 9] = wReg * -1.0;
            row++;

            // r.y
            J[row, pj + 3] = wReg * dx;
            J[row, pj + 4] = wReg * dy;
            J[row, pj + 5] = wReg * dz;
            J[row, pj + 10] = wReg * 1.0;
            J[row, pk + 10] = wReg * -1.0;
            row++;

            // r.z
            J[row, pj + 6] = wReg * dx;
            J[row, pj + 7] = wReg * dy;
            J[row, pj + 8] = wReg * dz;
            J[row, pj + 11] = wReg * 1.0;
            J[row, pk + 11] = wReg * -1.0;
            row++;

            return row;
        }

        private int FillConstraintJacobianBlock(Matrix<double> J, int row, int vertexIndex, double wCon)
        {
            Vector3 v = restVertices[vertexIndex];
            EDVertexBinding binding = bindings[vertexIndex];

            for (int b = 0; b < binding.nodeIndices.Length; b++)
            {
                int nodeIndex = binding.nodeIndices[b];
                if (nodeIndex < 0) continue;

                double wb = (binding.weights != null && b < binding.weights.Length) ? binding.weights[b] : 1.0 / binding.nodeIndices.Length;

                int p = ParamBase(nodeIndex);

                Vector3 g = nodes[nodeIndex].restPosition;
                Vector3 u = v - g;

                double ux = u.x;
                double uy = u.y;
                double uz = u.z;

                double s = wCon * wb;

                // residual x
                J[row + 0, p + 0] += s * ux;
                J[row + 0, p + 1] += s * uy;
                J[row + 0, p + 2] += s * uz;
                J[row + 0, p + 9] += s;

                // residual y
                J[row + 1, p + 3] += s * ux;
                J[row + 1, p + 4] += s * uy;
                J[row + 1, p + 5] += s * uz;
                J[row + 1, p + 10] += s;

                // residual z
                J[row + 2, p + 6] += s * ux;
                J[row + 2, p + 7] += s * uy;
                J[row + 2, p + 8] += s * uz;
                J[row + 2, p + 11] += s;
            }

            return row + 3;
        }

        public Matrix<double> BuildAnalyticalJacobian(double rotationWeight = 1.0, double regularizationWeight = 10.0, double constraintWeight = 100.0)
        {
            int nodeCount = nodes.Count;

            int directedEdgeCount = 0;
            for (int i = 0; i < nodeCount; i++)
                directedEdgeCount += nodes[i].neighbors.Count;

            int constraintCount = vertexConstraints.Count;

            int rowCount = 6 * nodeCount + 3 * directedEdgeCount + 3 * constraintCount;
            int colCount = 12 * nodeCount;

            var J = DenseMatrix.Create(rowCount, colCount, 0.0);

            double wRot = Math.Sqrt(rotationWeight);
            double wReg = Math.Sqrt(regularizationWeight);
            double wCon = Math.Sqrt(constraintWeight);

            int row = 0;

            // Rotation
            for (int i = 0; i < nodeCount; i++)
                row = FillRotationJacobianBlock(J, row, i, wRot);

            // Regularization (directed)
            for (int j = 0; j < nodeCount; j++)
            {
                foreach (int k in nodes[j].neighbors)
                    row = FillRegularizationJacobianBlock(J, row, j, k, wReg);
            }

            // Constraints
            for (int c = 0; c < constraintCount; c++)
                row = FillConstraintJacobianBlock(J, row, vertexConstraints[c].vertexIndex, wCon);

            return J;
        }
#endif

        public void SolveED(int maxIterations = 10,
                    double rotationWeight = 1.0,
                    double regularizationWeight = 10.0,
                    double constraintWeight = 100.0,
                    double damping = 1.0,
                    double residualTolerance = 1e-5,
                    double stepTolerance = 1e-6,
                    bool resetBeforeSolve = true)
        {
            if (resetBeforeSolve) ResetDeformation();

#if MATH_NET_AVAILABLE
            var x = PackNodeParameters();

            for (int iter = 0; iter < maxIterations; iter++)
            {
                ApplyNodeParameters(x);

                var f = EvaluateResidualVector(rotationWeight, regularizationWeight, constraintWeight);
                double error = f.L2Norm();

                //Debug.Log($"[ED] Iter {iter} - Residual: {error}");

                // Already solved / close enough
                if (!double.IsFinite(error) || error < residualTolerance)
                {
                    //Debug.Log($"[ED] Converged before solve at iteration {iter}");
                    break;
                }

                //var J = BuildNumericalJacobian(x, rotationWeight, regularizationWeight, constraintWeight);
                var J = BuildAnalyticalJacobian(rotationWeight, regularizationWeight, constraintWeight);

                //double diff = (J - J2).FrobeniusNorm();
                //double rel = diff / Math.Max(1e-12, J2.FrobeniusNorm());

                //Debug.Log($"Jacobian diff = {diff}, relative = {rel}");

                double jNorm = J.FrobeniusNorm();
                if (!double.IsFinite(jNorm) || jNorm < 1e-12)
                {
                    //Debug.Log("[ED] Jacobian is near zero; stopping.");
                    break;
                }

                Vector<double> delta;

                try
                {
                    var qr = J.QR();
                    delta = qr.Solve(-f);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"SolveED failed: {ex.Message}");
                    return;
                }

                double stepNorm = delta.L2Norm();
                if (!double.IsFinite(stepNorm))
                {
                    Debug.LogError("[ED] SolveED produced non-finite delta.");
                    return;
                }

                if (stepNorm < stepTolerance)
                {
                    //Debug.Log($"[ED] Converged by step size at iteration {iter}");
                    break;
                }

                x = x + damping * delta;
            }

            ApplyNodeParameters(x);
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
