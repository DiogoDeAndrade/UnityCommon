using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;
using System.Text;
using System.Threading.Tasks;
using System.Linq;


#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{  
    public enum BindingSelectionMode { ClosestOne, NearestK };
    public enum BindingWeightMode { Uniform, InversePower, Gaussian, OriginalED };
    public enum GraphLinkMode { PartitionAdjacency, SharedBindings, DirectionAware };

    [Serializable]
    public class EDNode
    {
        public DVector3         restPosition;
        public List<int>        neighbors = new();
    }

    [Serializable]
    class EDClearanceCache
    {
        [SerializeField]
        private double[] values;

        public EDClearanceCache(int count)
        {
            values = new double[count];
        }

        public EDClearanceCache Clone()
        {
            var clone = new EDClearanceCache(values.Length);
            Array.Copy(values, clone.values, values.Length);
            return clone;
        }

        public double Get(int index) => values[index];

        public void Set(int index, double value)
        {
            values[index] = value;
        }
    }

    [Serializable]
    class EDState
    {
        [SerializeField]
        private double[]        parameters; // 12 * nodeCount
        [SerializeField]
        public EDClearanceCache clearances;

        public EDState(int nodeCount)
        {
            parameters = new double[12 * nodeCount];
            // Set all matrices to identity
            for (int i = 0; i < nodeCount; i++)
            {
                int baseIndex = i * 12;
                parameters[baseIndex + 0] = 
                parameters[baseIndex + 5] = 
                parameters[baseIndex + 10] = 1.0; 
            }
        }   

        public double Get(int index) => parameters[index];
        public void Set(int index, double value)
        {
            parameters[index] = value;
        }

        public int Count => parameters.Length;

        public void SetTranslation(int nodeIndex, double x, double y, double z)
        {
            int o = nodeIndex * 12;

            parameters[o + 3] = x;
            parameters[o + 7] = y;
            parameters[o + 11] = z;
        }

        public void ResetRotation(int nodeIndex)
        {
            int o = nodeIndex * 12;

            parameters[o + 0] = 1.0;
            parameters[o + 1] = 0.0;
            parameters[o + 2] = 0.0;

            parameters[o + 4] = 0.0;
            parameters[o + 5] = 1.0;
            parameters[o + 6] = 0.0;

            parameters[o + 8] = 0.0;
            parameters[o + 9] = 0.0;
            parameters[o + 10] = 1.0;
        }

        public void Apply(Vector<double> delta, double damping)
        {
            if (delta == null)
                throw new ArgumentNullException(nameof(delta));

            if (delta.Count != parameters.Length)
            {
                throw new ArgumentException($"Delta size mismatch. Delta={delta.Count}, State={parameters.Length}");
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                double v = parameters[i] + damping * delta[i];

                if (!double.IsFinite(v))
                {
                    throw new InvalidOperationException($"Non-finite state parameter at index {i}.");
                }

                parameters[i] = v;
            }
        }

        public EDState Clone()
        {
            EDState clone = new EDState(parameters.Length / 12);
            Array.Copy(parameters, clone.parameters, parameters.Length);
            clone.clearances = clearances.Clone();
            return clone;
        }

        public EDState CloneAndApply(Vector<double> delta, double damping = 1.0)
        {
            EDState clone = Clone();
            clone.Apply(delta, damping);
            return clone;
        }

        public double GetClearance(int index) => clearances.Get(index);

        public DVector3 TransformOffset(int nodeIndex, DVector3 localOffset)
        {
            int o = nodeIndex * 12;

            double m00 = Get(o + 0);
            double m01 = Get(o + 1);
            double m02 = Get(o + 2);
            double m03 = Get(o + 3);

            double m10 = Get(o + 4);
            double m11 = Get(o + 5);
            double m12 = Get(o + 6);
            double m13 = Get(o + 7);

            double m20 = Get(o + 8);
            double m21 = Get(o + 9);
            double m22 = Get(o + 10);
            double m23 = Get(o + 11);

            return new DVector3(
                m00 * localOffset.x + m01 * localOffset.y + m02 * localOffset.z + m03,
                m10 * localOffset.x + m11 * localOffset.y + m12 * localOffset.z + m13,
                m20 * localOffset.x + m21 * localOffset.y + m22 * localOffset.z + m23
            );
        }
    }

    [Serializable]
    readonly struct EDStateView
    {
        [SerializeField]
        private readonly EDState            parentState;
        [SerializeField]
        private readonly int                perturbedIndex;
        [SerializeField]
        private readonly double             perturbation;
        [SerializeField]
        private readonly EDClearanceCache   _clearances;

        public EDStateView(EDState parameters, EDClearanceCache clearances = null)
        {
            this.parentState = parameters;
            this.perturbedIndex = -1;
            this.perturbation = 0.0;
            this._clearances = clearances;
        }

        public EDStateView(EDState parameters, int perturbedIndex, double perturbation, EDClearanceCache clearances = null)
        {
            this.parentState = parameters;
            this.perturbedIndex = perturbedIndex;
            this.perturbation = perturbation;
            this._clearances = clearances;
        }

        public EDClearanceCache clearances => (_clearances == null) ? (parentState.clearances) : (_clearances);
        public double Get(int index) => (index == (perturbedIndex)) ? (parentState.Get(index) + perturbation) : (parentState.Get(index));

        public DVector3 DeformVertex(int nodeIndex, DVector3 p, DVector3 restPos)
        {
            return TransformOffset(nodeIndex, p - restPos) + restPos;
        }

        public DVector3 TransformOffset(int nodeIndex, DVector3 localOffset)
        {
            int o = nodeIndex * 12;

            double m00 = Get(o + 0);
            double m01 = Get(o + 1);
            double m02 = Get(o + 2);
            double m03 = Get(o + 3);

            double m10 = Get(o + 4);
            double m11 = Get(o + 5);
            double m12 = Get(o + 6);
            double m13 = Get(o + 7);

            double m20 = Get(o + 8);
            double m21 = Get(o + 9);
            double m22 = Get(o + 10);
            double m23 = Get(o + 11);

            return new DVector3(
                m00 * localOffset.x + m01 * localOffset.y + m02 * localOffset.z + m03,
                m10 * localOffset.x + m11 * localOffset.y + m12 * localOffset.z + m13,
                m20 * localOffset.x + m21 * localOffset.y + m22 * localOffset.z + m23
            );
        }

        public DVector3 TransformVector(int nodeIndex, DVector3 localVector)
        {
            int o = nodeIndex * 12;

            double m00 = Get(o + 0);
            double m01 = Get(o + 1);
            double m02 = Get(o + 2);
            double m03 = Get(o + 3);

            double m10 = Get(o + 4);
            double m11 = Get(o + 5);
            double m12 = Get(o + 6);
            double m13 = Get(o + 7);

            double m20 = Get(o + 8);
            double m21 = Get(o + 9);
            double m22 = Get(o + 10);
            double m23 = Get(o + 11);

            return new DVector3(
                m00 * localVector.x + m01 * localVector.y + m02 * localVector.z,
                m10 * localVector.x + m11 * localVector.y + m12 * localVector.z,
                m20 * localVector.x + m21 * localVector.y + m22 * localVector.z
            );
        }


        public DVector3 GetAxisX(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 0),  // m00
                Get(o + 4),  // m10
                Get(o + 8)   // m20
            );
        }
        public DVector3 GetAxisY(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 1),  // m01
                Get(o + 5),  // m11
                Get(o + 9)   // m21
            );
        }
        public DVector3 GetAxisZ(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 2),   // m02
                Get(o + 6),   // m12
                Get(o + 10)   // m22
            );
        }

        public DVector3 GetTranslation(int nodeIndex)
        {
            int o = nodeIndex * 12;

            return new DVector3(
                Get(o + 3),   // m03
                Get(o + 7),   // m13
                Get(o + 11)   // m23
            );
        }
    }

    [Serializable]
    public struct EDVertexBinding
    {
        public int[]    nodeIndices;   // k nearest
        public double[] weights;     // normalized
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
        public DVector3 targetPosition;
    }

    [Serializable]
    public class NavEDSegments
    {
        public DVector3         p1, p2;
        public EDVertexBinding  bind1, bind2;
        public DVector3         center;
        public DVector3         normal, probeT, probeB;
        public EDVertexBinding  cBind, tBind, bBind;
    }

    public delegate bool HasLOS(Vector3 p1, Vector3 p2);

    [Serializable]
    public class EmbededDeformation
    {
        public DVector3[] restVertices;
        public int[] triangles;

        public List<EDNode> nodes = new();
        public EDVertexBinding[] bindings;
        public List<EDHandleConstraint> handleConstraints = new();
        public List<EDVertexConstraint> vertexConstraints = new();
        public TopologyStatic navMeshTopology;
        public List<NavEDSegments> structure;
        public float maxSlope = 45.0f;
        public float slopeSoftBand = 5.0f;
        public Vector3 upVector = Vector3.up;

        [SerializeField]
        private EDState currentState;
        [SerializeField]
        private EDState restState;

           
#if UC_PROFILER_ENABLE
        DebugProfiler timePack;
        DebugProfiler timeIteration;
        DebugProfiler timeResidualEvaluate;
        DebugProfiler timeJacobianBuild;
        DebugProfiler timeSolve;
        DebugProfiler timeApplyParameters;
        DebugProfiler timeUpdateClearance;
        DebugProfiler timeJacobianBuildConstraint;
        DebugProfiler timeJacobianBuildRotation;
        DebugProfiler timeJacobianBuildRegularization;
        DebugProfiler timeJacobianBuildSlope;
        DebugProfiler timeJacobianBuildClearance;
#endif


        int deformGraphEdgeCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < nodes.Count; i++)
                    count += nodes[i].neighbors.Count;
                return count / 2; // Undirected graph, each edge is counted twice.
            }
        }

        public void BuildDeformationGraph(TopologyStatic topology, float minDistance, List<int> forcedVertices, bool forceStructureNodes,
                                          BindingSelectionMode bindMode, BindingWeightMode weightMode, GraphLinkMode graphLinkMode,
                                          int k = 4, // When BindingSelectionMode = closest-K
                                          float maxBindDistance = 2.0f, // When GraphLinKMode = DirectionAware
                                          float minBindAngle = 20.0f, // When GraphLinKMode = DirectionAware
                                          HasLOS hasLOSFunction = null, // When GraphLinKMode = DirectionAware
                                          float power = 2.0f,
                                          float sigma = 1.0f) // When BindingSelectionMode = closest-K and BindingWeightMode = InversePower
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
            var v = topology.GetVertexPositions();
            restVertices = new DVector3[v.Count];
            for (int i = 0; i < v.Count; i++) restVertices[i] = v[i].ToDVector3();
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

            HashSet<int> forcedSet = (forcedVertices != null) ? (new HashSet<int>(forcedVertices)) : (new HashSet<int>());

            // Forced vertices first - min distance is set to 0.0f so that they're always added regardless of distance to each other
            // There are no duplicates for sure, so this code could probabably be optimized a bit, but it's not a big deal since the number of forced vertices is expected to be low.
            foreach (int vId in forcedSet)
            {
                if ((vId < 0) || (vId >= topology.vertexCount))
                    continue;

                TryAddSampleVertex(vId, topology, 0.0f);
            }

            // Add structure nodes
            if ((structure != null) && (forceStructureNodes))
                for (int i = 0; i < structure.Count; i++)
                {
                    var seg = structure[i];
                    int idx1 = TryAddSampleVertex(seg.p1, minDistanceSq);
                    int idx2 = TryAddSampleVertex(seg.p2, minDistanceSq);
                    //AddUndirectedNeighbor(idx1, idx2);
                }

            // Fill remaining graph with radius-pruned vertex samples
            for (int vId = 0; vId < topology.vertexCount; vId++)
            {
                if (forcedSet.Contains(vId))
                    continue;

                TryAddSampleVertex(vId, topology, minDistanceSq);
            }

            // Fallback safety
            if ((nodes.Count == 0) && (topology.vertexCount > 0))
            {
                Debug.LogError("Failed to generate ED deformation graph: no nodes were sampled.");
                return;
            }

            // -----------------------------------------------------------------
            // 3) Build bindings: each navmesh vertex gets k nearest nodes
            // -----------------------------------------------------------------
            BuildBindings(topology, bindMode, weightMode, k, power, sigma);

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

                case GraphLinkMode.DirectionAware:
                    BuildGraphDirectionAware(maxBindDistance, minBindAngle, hasLOSFunction);
                    break;
            }

            currentState = new EDState(nodes.Count);
            restState = new EDState(nodes.Count);

            Debug.Log($"ED graph built. Vertices={topology.vertexCount}, Triangles={topology.triangleCount}, Nodes={nodes.Count}, Edges={deformGraphEdgeCount}");
        }

        private struct DirectionAwareCandidate
        {
            public int nodeIndex;
            public double distanceSq;
            public Vector3 direction;
        }

        void BuildGraphDirectionAware(float maxBindDistance, float minBindAngle, HasLOS hasLOSFunction)
        {

            // Clamp to valid cosine range.
            float sameDirectionCosTolerance = Mathf.Cos(minBindAngle * Mathf.Deg2Rad);
            sameDirectionCosTolerance = Math.Max(-1.0f, Math.Min(1.0f, sameDirectionCosTolerance));

            bool IsDirectionAlreadyChosen(Vector3 candidateDirection, List<Vector3> chosenDirections)
            {
                for (int i = 0; i < chosenDirections.Count; i++)
                {
                    double d = Vector3.Dot(candidateDirection, chosenDirections[i]);

                    // Same direction only. If you want opposite directions to collapse too,
                    // change this to Math.Abs(d) >= sameDirectionCosTolerance.
                    if (d >= sameDirectionCosTolerance)
                        return true;
                }

                return false;
            }

            if (nodes == null || nodes.Count == 0)
                return;

            if (maxBindDistance <= 0.0)
            {
                Debug.LogWarning("BuildGraphDirectionAware: maxBindDistance must be > 0.");
                return;
            }

            // Clear previous graph
            for (int i = 0; i < nodes.Count; i++) nodes[i].neighbors.Clear();

            float maxBindDistanceSq = maxBindDistance * maxBindDistance;
            const double eps = 1e-12;

            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 pi = nodes[i].restPosition.ToVector3();

                List<DirectionAwareCandidate> candidates = new();

                // ---------------------------------------------------------
                // 1) Gather valid candidates inside radius (+ optional LOS)
                // ---------------------------------------------------------
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j)
                        continue;

                    Vector3 pj = nodes[j].restPosition.ToVector3();
                    Vector3 delta = pj - pi;

                    float distSq = delta.sqrMagnitude;
                    if ((distSq <= eps) || (distSq > maxBindDistanceSq))
                        continue;

                    if ((hasLOSFunction != null) && (!hasLOSFunction(pi, pj)))
                        continue;

                    float dist = Mathf.Sqrt(distSq);
                    Vector3 dir = delta / dist;

                    candidates.Add(new DirectionAwareCandidate
                    {
                        nodeIndex = j,
                        distanceSq = distSq,
                        direction = dir
                    });
                }

                // ---------------------------------------------------------
                // 2) Closest first
                // ---------------------------------------------------------
                candidates.Sort((a, b) => a.distanceSq.CompareTo(b.distanceSq));

                // ---------------------------------------------------------
                // 3) Greedily keep only one candidate per direction bucket
                // ---------------------------------------------------------
                List<Vector3> chosenDirections = new();

                for (int c = 0; c < candidates.Count; c++)
                {
                    var cand = candidates[c];

                    if (IsDirectionAlreadyChosen(cand.direction, chosenDirections))
                        continue;

                    AddUndirectedNeighbor(i, cand.nodeIndex);
                    chosenDirections.Add(cand.direction);
                }
            }
        }

        private int TryAddSampleVertex(int vertexId, TopologyStatic topology, float minDistanceSq)
        {
            return TryAddSampleVertex(topology.GetVertexPosition(vertexId).ToDVector3(), minDistanceSq);
        }

        private int TryAddSampleVertex(DVector3 pos, float minDistanceSq)
        {
            if (minDistanceSq > 0.0f)
            {
                int index = GetSampledVertexIndex(pos, minDistanceSq);
                if (index != -1) return index;
            }

            nodes.Add(new EDNode
            {
                restPosition = pos,
                neighbors = new List<int>()
            });

            return nodes.Count - 1;
        }

        private int GetSampledVertexIndex(DVector3 pos, double tolerance)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                DVector3 q = nodes[i].restPosition;
                if ((pos - q).sqrMagnitude < tolerance)
                    return i;
            }

            return -1;
        }

        private int GetClosestNodeIndex(DVector3 p)
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
            if ((a < 0) || (b < 0) || (a == b))
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
                DVector3 p = nodes[i].restPosition;

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

        private void BuildBindings(TopologyStatic topology, BindingSelectionMode bindMode, BindingWeightMode weightMode, int k = 4, float power = 2.0f, float sigma = 1.0f)
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

            int vertexCount = topology.vertexCount;
            int nodeCount = nodes.Count;
            bindings = new EDVertexBinding[vertexCount];

            for (int vId = 0; vId < vertexCount; vId++)
            {
                DVector3 p = topology.GetVertexPosition(vId).ToDVector3();

                bindings[vId] = GetBinding(p, bindMode, weightMode, k, power, sigma);
            }
        }

        EDVertexBinding GetBinding(DVector3 p, BindingSelectionMode bindMode, BindingWeightMode weightMode, int k = 4, float power = 2.0f, float sigma = 1.0f)
        {
            EDVertexBinding ret = new();

            double epsilon = 1e-8;
            double sigmaSq = sigma * sigma;

            switch (bindMode)
            {
                case BindingSelectionMode.ClosestOne:
                    {
                        int closestNode = GetClosestNodeIndex(p);

                        ret = new EDVertexBinding
                        {
                            nodeIndices = new int[] { closestNode },
                            weights = new double[] { 1.0f }
                        };
                    }
                    break;
                case BindingSelectionMode.NearestK:
                    switch (weightMode)
                    {
                        case BindingWeightMode.Uniform:
                            ret = GetNearestK_Generic(p, k, (dSq, dMaxSq) => 1.0);
                            break;
                        case BindingWeightMode.InversePower:
                            ret = GetNearestK_Generic(p, k,
                                                      (dSq, _) =>
                                                      {
                                                          double d = Math.Sqrt(dSq);
                                                          return 1.0 / Math.Pow(d + epsilon, power);
                                                      });
                            break;
                        case BindingWeightMode.Gaussian:
                            ret = GetNearestK_Generic(p, k,
                                                      (dSq, _) =>
                                                      {
                                                          return Math.Exp(-dSq / (2.0 * sigmaSq));
                                                      });
                            break;
                        case BindingWeightMode.OriginalED:
                            epsilon = 1e-12;
                            ret = GetNearestK_Generic(p, k,
                                                      (dSq, dMaxSq) =>
                                                      {
                                                          double d = Math.Sqrt(dSq);
                                                          double dMax = Math.Sqrt(Math.Max(dMaxSq, epsilon));

                                                          double w = 1.0 - (d / dMax);
                                                          if (w < 0.0) w = 0.0;

                                                          return w * w;
                                                      });
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    Debug.LogWarning($"BuildBindings: unsupported link mode {bindMode}.");
                    break;
            }

            return ret;
        }

        EDVertexBinding GetNearestK_Generic(DVector3 p, int k, Func<double, double, double> weightFunc)
        {
            int nodeCount = nodes.Count;
            int actualK = Mathf.Clamp(k, 1, nodeCount);

            const double epsilon = 1e-12;
            const double snapDistanceSq = 1e-12;

            int[] bestIndices = new int[actualK];
            double[] bestDistSq = new double[actualK];

            for (int i = 0; i < actualK; i++)
            {
                bestIndices[i] = -1;
                bestDistSq[i] = double.MaxValue;
            }

            // Find K nearest
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

            double[] weights = new double[actualK];

            // Snap
            bool snapped = false;
            for (int i = 0; i < actualK; i++)
            {
                if (bestIndices[i] >= 0 && bestDistSq[i] <= snapDistanceSq)
                {
                    for (int j = 0; j < actualK; j++)
                        weights[j] = 0.0;

                    weights[i] = 1.0;
                    snapped = true;
                    break;
                }
            }

            if (!snapped)
            {
                double weightSum = 0.0;

                double dMaxSq = bestDistSq[actualK - 1];

                for (int i = 0; i < actualK; i++)
                {
                    if (bestIndices[i] < 0)
                    {
                        weights[i] = 0.0;
                        continue;
                    }

                    double w = weightFunc(bestDistSq[i], dMaxSq);

                    weights[i] = w;
                    weightSum += w;
                }

                if (weightSum > epsilon)
                {
                    for (int i = 0; i < actualK; i++)
                        weights[i] /= weightSum;
                }
                else
                {
                    int validCount = 0;
                    for (int i = 0; i < actualK; i++)
                        if (bestIndices[i] >= 0) validCount++;

                    double fallback = validCount > 0 ? 1.0 / validCount : 0.0;

                    for (int i = 0; i < actualK; i++)
                        weights[i] = (bestIndices[i] >= 0) ? fallback : 0.0;
                }
            }

            return new EDVertexBinding
            {
                nodeIndices = bestIndices,
                weights = weights
            };
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
                    DVector3 restPos = restVertices[vId];
                    DVector3 targetPos = delta.MultiplyPoint3x4(restPos.ToVector3()).ToDVector3();

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
            currentState = new EDState(nodes.Count);
            ComputeClearance(currentState);
        }

        public Vector3[] DeformVerticesFromCurrentNodeTransforms()
        {
            Vector3[] deformed = new Vector3[restVertices.Length];

            for (int vId = 0; vId < restVertices.Length; vId++)
            {
                var vertex = restVertices[vId];
                var binding = bindings[vId];
                deformed[vId] = DeformVertex(vertex, binding, new EDStateView(currentState)).ToVector3();
            }

            return deformed;
        }

        private DVector3 DeformVertex(DVector3 v, EDVertexBinding binding, EDStateView state)
        {
            DVector3 result = DVector3.zero;

            for (int i = 0; i < binding.nodeIndices.Length; i++)
            {
                int nodeIndex = binding.nodeIndices[i];
                double w = binding.weights[i];

                var node = nodes[nodeIndex];
                DVector3 g = node.restPosition;

                DVector3 transformed = state.DeformVertex(nodeIndex, v, g);

                result += w * transformed;
            }

            return result;
        }

        public DVector3 DeformVertexFromCurrentNodeTransforms(int vertexId)
        {
            if ((vertexId < 0) || (vertexId >= restVertices.Length) || (bindings == null) || (vertexId >= bindings.Length))
            {
                return DVector3.zero;
            }
            DVector3 v = restVertices[vertexId];
            var binding = bindings[vertexId];
                
            return DeformVertex(v, binding, new EDStateView(currentState));
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
                ResetDeformation();
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

                DVector3 rest = restVertices[vc.vertexIndex];
                DVector3 delta = vc.targetPosition - rest;

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
                currentState.SetTranslation(i, tx[i], ty[i], tz[i]);
                currentState.ResetRotation(i);
            }

            return true;
#else
            throw new NotImplementedException();
#endif
        }

#if MATH_NET_AVAILABLE
        private Vector<double> EvaluateResidualVector(EDStateView state,
                                                      double rotationWeight,
                                                      double regularizationWeight,
                                                      double constraintWeight,
                                                      double clearanceWeight,
                                                      double slopeWeight)
        {
            DebugProfiler.DebugMark(timeResidualEvaluate);

            int nodeCount = nodes.Count;
            int directedEdgeCount = 0;
            for (int i = 0; i < nodes.Count; i++)
                directedEdgeCount += nodes[i].neighbors.Count;
            int constraintCount = vertexConstraints.Count;

            int residualCount = 6 * nodeCount + 3 * directedEdgeCount + 3 * constraintCount;
            if (clearanceWeight > 0)
            {
                residualCount += 1 * structure.Count;
            }
            if (slopeWeight > 0)
            {
                residualCount += 1 * structure.Count;
            }

            Vector<double> residual = DenseVector.Create(residualCount, 0.0);

            double wRot = Math.Sqrt(rotationWeight);
            double wReg = Math.Sqrt(regularizationWeight);
            double wCon = Math.Sqrt(constraintWeight);
            double wClearance = Math.Sqrt(clearanceWeight);
            double wSlope = Math.Sqrt(slopeWeight);

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
                var axisX = state.GetAxisX(i);
                var axisY = state.GetAxisY(i);
                var axisZ = state.GetAxisZ(i);

                residual[row++] = wRot * DVector3.Dot(axisX, axisY);
                residual[row++] = wRot * DVector3.Dot(axisX, axisZ);
                residual[row++] = wRot * DVector3.Dot(axisY, axisZ);

                residual[row++] = wRot * (DVector3.Dot(axisX, axisX) - 1.0);
                residual[row++] = wRot * (DVector3.Dot(axisY, axisY) - 1.0);
                residual[row++] = wRot * (DVector3.Dot(axisZ, axisZ) - 1.0);
            }

            // -------------------------------------------------------------
            // 2) Regularization residuals: 3 per directed edge
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
                DVector3 gj = nodeJ.restPosition;
                DVector3 tj = state.GetTranslation(j);

                foreach (int k in nodeJ.neighbors)
                {
                    EDNode nodeK = nodes[k];
                    DVector3 gk = nodeK.restPosition;
                    DVector3 tk = state.GetTranslation(k);

                    DVector3 diff = gk - gj;
                    DVector3 rotatedDiff = state.TransformVector(j, diff);

                    DVector3 r = rotatedDiff + gj + tj - (gk + tk);

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

                DVector3 deformed = DeformVertex(restVertices[vc.vertexIndex], bindings[vc.vertexIndex], state);
                DVector3 r = deformed - vc.targetPosition;

                residual[row++] = wCon * r.x;
                residual[row++] = wCon * r.y;
                residual[row++] = wCon * r.z;
            }

            if (clearanceWeight > 0)
            {
                // -------------------------------------------------------------
                // 4) Clearance constraints - allow for clearance to grow, but constrained it going smaller
                //
                // -------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    var originalClearance = restState.clearances.Get(i);
                    var currentClearance = state.clearances.Get(i);

                    residual[row++] = wClearance * ComputeClearanceLoss(originalClearance, currentClearance);
                }
            }

            if (slopeWeight > 0)
            {
                // -------------------------------------------------------------
                // 5) Slope constraints
                // -------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    residual[row++] = EvaluateSingleSlopeResidual(state, i, wSlope);
                }
            }

            DebugProfiler.DebugMark(timeResidualEvaluate);

            return residual;
        }

        private int ParamBase(int nodeIndex) => nodeIndex * 12;

        private int FillRotationJacobianBlock(EDStateView state, Matrix<double> J, int row, int nodeIndex, double wRot, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildRotation);

            int p = ParamBase(nodeIndex);
            var aX = state.GetAxisX(nodeIndex);
            var aY = state.GetAxisY(nodeIndex);
            var aZ = state.GetAxisZ(nodeIndex);

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

            double ax2 = aX.x * aX.x + aX.y * aX.y + aX.z * aX.z;
            double ay2 = aY.x * aY.x + aY.y * aY.y + aY.z * aY.z;
            double az2 = aZ.x * aZ.x + aZ.y * aZ.y + aZ.z * aZ.z;

            jNormRunningTotalSq += 6.0 * wRot * wRot * (ax2 + ay2 + az2);

            DebugProfiler.DebugMark(timeJacobianBuildRotation);

            return row;
        }

        private int FillRegularizationJacobianBlock(EDStateView state, Matrix<double> J, int row, int nodeJ, int nodeK, double wReg, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildRegularization);

            int pj = ParamBase(nodeJ);
            int pk = ParamBase(nodeK);

            DVector3 gj = nodes[nodeJ].restPosition;
            DVector3 gk = nodes[nodeK].restPosition;
            DVector3 d = gk - gj;

            double dx = d.x;
            double dy = d.y;
            double dz = d.z;

            // r.x
            J[row, pj + 0] = wReg * dx;   // m00
            J[row, pj + 1] = wReg * dy;   // m01
            J[row, pj + 2] = wReg * dz;   // m02
            J[row, pj + 3] = wReg * 1.0;  // m03 / tx_j
            J[row, pk + 3] = wReg * -1.0; // tx_k
            row++;

            // r.y
            J[row, pj + 4] = wReg * dx;   // m10
            J[row, pj + 5] = wReg * dy;   // m11
            J[row, pj + 6] = wReg * dz;   // m12
            J[row, pj + 7] = wReg * 1.0;  // m13 / ty_j
            J[row, pk + 7] = wReg * -1.0; // ty_k
            row++;

            // r.z
            J[row, pj + 8] = wReg * dx;   // m20
            J[row, pj + 9] = wReg * dy;   // m21
            J[row, pj + 10] = wReg * dz;   // m22
            J[row, pj + 11] = wReg * 1.0;  // m23 / tz_j
            J[row, pk + 11] = wReg * -1.0; // tz_k
            row++;

            double d2 = dx * dx + dy * dy + dz * dz;
            jNormRunningTotalSq += 3.0 * wReg * wReg * (d2 + 2.0);

            DebugProfiler.DebugMark(timeJacobianBuildRegularization);

            return row;
        }

        private int FillConstraintJacobianBlock(EDStateView state, Matrix<double> J, int row, int vertexIndex, double wCon, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            DVector3 v = restVertices[vertexIndex];
            EDVertexBinding binding = bindings[vertexIndex];

            for (int b = 0; b < binding.nodeIndices.Length; b++)
            {
                int nodeIndex = binding.nodeIndices[b];
                if (nodeIndex < 0)
                    continue;

                double wb = ((binding.weights != null) && (b < binding.weights.Length)) ? (binding.weights[b]) : (1.0 / binding.nodeIndices.Length);

                int p = ParamBase(nodeIndex);

                DVector3 g = nodes[nodeIndex].restPosition;
                DVector3 u = v - g;

                double ux = u.x;
                double uy = u.y;
                double uz = u.z;

                double s = wCon * wb;

                // residual x
                J[row + 0, p + 0] += s * ux; // m00
                J[row + 0, p + 1] += s * uy; // m01
                J[row + 0, p + 2] += s * uz; // m02
                J[row + 0, p + 3] += s;      // m03 / tx

                // residual y
                J[row + 1, p + 4] += s * ux; // m10
                J[row + 1, p + 5] += s * uy; // m11
                J[row + 1, p + 6] += s * uz; // m12
                J[row + 1, p + 7] += s;      // m13 / ty

                // residual z
                J[row + 2, p + 8] += s * ux; // m20
                J[row + 2, p + 9] += s * uy; // m21
                J[row + 2, p + 10] += s * uz; // m22
                J[row + 2, p + 11] += s;      // m23 / tz

                double u2 = ux * ux + uy * uy + uz * uz;
                jNormRunningTotalSq += 3.0 * s * s * (u2 + 1.0);
            }

            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            return row + 3;
        }

        Matrix<double> BuildJacobian(EDState state, out double jNorm, double rotationWeight, double regularizationWeight, double constraintWeight, double clearanceWeight, double slopeWeight)
        {
            DebugProfiler.DebugMark(timeJacobianBuild);

            // First rows are rotation constraints, then regularization, then positional constraints, then clearance constraints (if enabled)
            // We also compute an estimate of the Jacobian norm while filling it, which can be used for scaling other terms
            // Rotation, regularization, and constraint blocks are calculated analytically, while clearance and slope weight is currently left for numerical differentiation
            jNorm = 0.0;

            int nodeCount = nodes.Count;

            int directedEdgeCount = 0;
            for (int i = 0; i < nodeCount; i++)
                directedEdgeCount += nodes[i].neighbors.Count;

            int constraintCount = vertexConstraints.Count;

            int rowCount = 6 * nodeCount + 3 * directedEdgeCount + 3 * constraintCount;
            if (clearanceWeight > 0)
            {
                rowCount += 1 * structure.Count;
            }
            if (slopeWeight > 0)
            {
                rowCount += 1 * structure.Count;
            }
            int colCount = 12 * nodeCount;

            var J = DenseMatrix.Create(rowCount, colCount, 0.0);

            double wRot = Math.Sqrt(rotationWeight);
            double wReg = Math.Sqrt(regularizationWeight);
            double wCon = Math.Sqrt(constraintWeight);
            double wClearance = Math.Sqrt(clearanceWeight);
            double wSlope = Math.Sqrt(slopeWeight);

            int row = 0;

            var stateView = new EDStateView(state);

            // Rotation
            for (int i = 0; i < nodeCount; i++)
            {
                row = FillRotationJacobianBlock(stateView, J, row, i, wRot, ref jNorm);
            }

            // Regularization (directed)
            for (int j = 0; j < nodeCount; j++)
            {
                foreach (int k in nodes[j].neighbors)
                {
                    row = FillRegularizationJacobianBlock(stateView, J, row, j, k, wReg, ref jNorm);
                }
            }

            // Constraints
            for (int c = 0; c < constraintCount; c++)
            {
                row = FillConstraintJacobianBlock(stateView, J, row, vertexConstraints[c].vertexIndex, wCon, ref jNorm);
            }

            if (clearanceWeight > 0)
            {
                int clearanceStartRow = row;

                double clearanceJNorm = 0.0;
                object normLock = new object();

                DebugProfiler.DebugMark(timeJacobianBuildClearance);

                Parallel.For(
                    0,
                    structure.Count,
                    () => 0.0,
                    (i, loopState, localNorm) =>
                    {
                        int clearanceRow = clearanceStartRow + i;

                        localNorm += FillClearanceJacobianRow(state, J, clearanceRow, i, wClearance);

                        return localNorm;
                    },
                    localNorm =>
                    {
                        lock (normLock)
                        {
                            clearanceJNorm += localNorm;
                        }
                    });

                DebugProfiler.DebugMark(timeJacobianBuildClearance);

                jNorm += clearanceJNorm;
                row += structure.Count;
            }

            if (slopeWeight > 0)
            {
                for (int i = 0; i < structure.Count; i++)
                {
                    row = FillSlopeJacobianBlock(state, J, row, i, wSlope, ref jNorm);
                }
            }

            jNorm = Math.Sqrt(jNorm);

            DebugProfiler.DebugMark(timeJacobianBuild);

            return J;
        }

        private double FillClearanceJacobianRow(EDState state, DenseMatrix J, int row, int segmentIndex, double wClearance)
        {
            var baseView = new EDStateView(state);

            double r0 = EvaluateSingleClearanceResidual(baseView, segmentIndex, wClearance);

            if (Math.Abs(r0) <= 1e-12)
            {
                return 0.0;
            }

            double localJNorm = 0.0;

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);
                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                var modified = new EDStateView(state, col, eps);

                double r1 = EvaluateSingleClearanceResidual(modified, segmentIndex, wClearance);

                double v = (r1 - r0) / eps;

                J[row, col] = v;
                localJNorm += v * v;
            }

            return localJNorm;
        }

        private int FillSlopeJacobianBlock(EDState state, DenseMatrix J, int row, int segmentIndex, double wSlope, ref double jNorm)
        {
            DebugProfiler.DebugMark(timeJacobianBuildSlope);

            var baseView = new EDStateView(state);

            // Base residual value for this segment.
            double r0 = EvaluateSingleSlopeResidual(baseView, segmentIndex, wSlope);

            if (r0 <= 1e-12)
            {
                DebugProfiler.DebugMark(timeJacobianBuildSlope);
                return row + 1;
            }

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);

                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));
                var modifiedState = new EDStateView(state, col, eps);

                double r1 = EvaluateSingleSlopeResidual(modifiedState, segmentIndex, wSlope);

                J[row, col] = (r1 - r0) / eps;

                jNorm += J[row, col] * J[row, col];
            }

            DebugProfiler.DebugMark(timeJacobianBuildSlope);

            return row + 1;
        }

        public void DebugJacobianNullspace(Matrix<double> J, double singularValueTolerance = 1e-10, int topCount = 20)
        {
            var svd = J.Svd(true);

            var s = svd.S;
            double sigmaMax = s[0];
            double sigmaMin = s[s.Count - 1];
            double tol = singularValueTolerance * sigmaMax;

            int rank = 0;
            for (int i = 0; i < s.Count; i++)
            {
                if (s[i] > tol)
                    rank++;
            }

            Debug.Log($"[ED] J rows = {J.RowCount}, cols = {J.ColumnCount}");
            Debug.Log($"[ED] Rank \u2245 {rank}/{J.ColumnCount}");
            Debug.Log($"[ED] Nullity \u2245 {J.ColumnCount - rank}");
            Debug.Log($"[ED] sigmaMax = {sigmaMax}");
            Debug.Log($"[ED] sigmaMin = {sigmaMin}");
            Debug.Log($"[ED] Condition \u2245 {sigmaMax / Math.Max(sigmaMin, 1e-300)}");

            // Math.NET returns VT, so the smallest right singular vector is the last row of VT
            var vt = svd.VT;
            int lastRow = vt.RowCount - 1;
            Vector<double> nullVec = vt.Row(lastRow);

            // Normalize for easier reading
            double maxAbs = 0.0;
            for (int i = 0; i < nullVec.Count; i++)
                maxAbs = Math.Max(maxAbs, Math.Abs(nullVec[i]));

            if (maxAbs > 0.0)
                nullVec = nullVec / maxAbs;

            // Collect largest entries
            List<(int index, double value)> entries = new();
            for (int i = 0; i < nullVec.Count; i++)
                entries.Add((i, nullVec[i]));

            entries.Sort((a, b) => Math.Abs(b.value).CompareTo(Math.Abs(a.value)));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[ED] Dominant entries in smallest right singular vector:");

            int count = Math.Min(topCount, entries.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = entries[i].index;
                double val = entries[i].value;

                int nodeIndex = idx / 12;
                int localParam = idx % 12;

                sb.AppendLine(
                    $"  #{i + 1}: global={idx}, node={nodeIndex}, param={ParamName(localParam)}, value={val}");
            }

            Debug.Log(sb.ToString());

            // Optional summary per node
            StringBuilder sbNode = new StringBuilder();
            sbNode.AppendLine("[ED] Null-space magnitude per node:");

            for (int n = 0; n < nodes.Count; n++)
            {
                int p = n * 12;

                double blockNormSq = 0.0;
                double matrixNormSq = 0.0;
                double translationNormSq = 0.0;

                for (int k = 0; k < 12; k++)
                {
                    double v = nullVec[p + k];
                    blockNormSq += v * v;

                    if (k < 9) matrixNormSq += v * v;
                    else translationNormSq += v * v;
                }

                double blockNorm = Math.Sqrt(blockNormSq);
                double matrixNorm = Math.Sqrt(matrixNormSq);
                double translationNorm = Math.Sqrt(translationNormSq);

                sbNode.AppendLine(
                    $"  node {n}: total={blockNorm}, matrix={matrixNorm}, translation={translationNorm}");
            }

            Debug.Log(sbNode.ToString());
        }

        private string ParamName(int localParam)
        {
            switch (localParam)
            {
                case 0: return "m00";
                case 1: return "m01";
                case 2: return "m02";
                case 3: return "m10";
                case 4: return "m11";
                case 5: return "m12";
                case 6: return "m20";
                case 7: return "m21";
                case 8: return "m22";
                case 9: return "tx";
                case 10: return "ty";
                case 11: return "tz";
                default: return $"p{localParam}";
            }
        }
#endif

        public void SolveED_GN(int maxIterations = 10,
                               double rotationWeight = 1.0,
                               double regularizationWeight = 10.0,
                               double constraintWeight = 100.0,
                               double damping = 1.0,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true)
        {
            if (resetBeforeSolve)
                ResetDeformation();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                var stateView = new EDStateView(currentState);

                var f = EvaluateResidualVector(stateView, rotationWeight, regularizationWeight, constraintWeight, 0.0, 0.0);

                double error = f.L2Norm();

                // Already solved / close enough
                if (!double.IsFinite(error) || error < residualTolerance)
                {
                    break;
                }

                var J = BuildJacobian(currentState, out double jNorm, rotationWeight, regularizationWeight, constraintWeight, 0.0, 0.0);

                if (!double.IsFinite(jNorm) || jNorm < 1e-12)
                {
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
                    break;
                }

                currentState.Apply(delta, damping);
            }
#else
    throw new NotImplementedException();
#endif
        }

        public void SolveED_LM(int maxIterations = 10,
                               double rotationWeight = 1.0,
                               double regularizationWeight = 10.0,
                               double constraintWeight = 100.0,
                               double lambda = 1e-3,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true,
                               bool adaptiveLambda = true)
        {
            if (resetBeforeSolve)
                ResetDeformation();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            double currentLambda = lambda;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                var stateView = new EDStateView(currentState);

                var f = EvaluateResidualVector(stateView, rotationWeight, regularizationWeight, constraintWeight, 0.0, 0.0);

                double error = f.L2Norm();

                if (!double.IsFinite(error))
                {
                    Debug.LogError("[ED] Residual became non-finite.");
                    return;
                }

                if (error < residualTolerance)
                    break;

                var J = BuildJacobian(currentState, out double jNorm, rotationWeight, regularizationWeight, constraintWeight, 0.0, 0.0);

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                    break;

                var JT = J.Transpose();
                var H = JT * J;
                var g = JT * f;

                Vector<double> delta = null;
                EDState acceptedState = null;
                bool solved = false;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var Hlm = H.Clone();

                    for (int i = 0; i < Hlm.RowCount; i++)
                        Hlm[i, i] += currentLambda;

                    try
                    {
                        delta = Hlm.Solve(-g);
                    }
                    catch
                    {
                        delta = null;
                    }

                    if (delta == null)
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    double stepNorm = delta.L2Norm();

                    if (!double.IsFinite(stepNorm))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    EDState candidateState;

                    try
                    {
                        candidateState = currentState.CloneAndApply(delta, 1.0);
                    }
                    catch
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    var candidateView = new EDStateView(candidateState);

                    var fCandidate = EvaluateResidualVector(candidateView, rotationWeight, regularizationWeight, constraintWeight, 0.0, 0.0);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    if (candidateError <= error)
                    {
                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        if (stepNorm < stepTolerance)
                        {
                            currentState = acceptedState;
                            return;
                        }

                        break;
                    }

                    currentLambda *= 10.0;
                }

                if (!solved)
                {
                    Debug.LogWarning("[ED] LM could not find an improving step.");
                    break;
                }

                currentState = acceptedState;
            }
#else
    throw new NotImplementedException();
#endif
        }

        public void SolveED_Nav(int maxIterations = 10,
                                double rotationWeight = 1.0,
                                double regularizationWeight = 10.0,
                                double constraintWeight = 100.0,
                                double clearanceWeight = 100.0,
                                double slopeWeight = 100.0,
                                double lambda = 1e-3,
                                double residualTolerance = 1e-5,
                                double stepTolerance = 1e-6,
                                bool resetBeforeSolve = true,
                                bool adaptiveLambda = true,
                                bool choleskyFactorization = false)
        {
            if (resetBeforeSolve)
                ResetDeformation();

#if MATH_NET_AVAILABLE
            if (currentState == null)
            {
                currentState = new EDState(nodes.Count);
                ComputeClearance(currentState);
            }

            double currentLambda = lambda;

            int iter = 0;

            for (iter = 0; iter < maxIterations; iter++)
            {
                DebugProfiler.DebugMark(timeIteration);

                var stateView = new EDStateView(currentState);

                var f = EvaluateResidualVector(stateView, rotationWeight, regularizationWeight, constraintWeight, clearanceWeight, slopeWeight);

                double error = f.L2Norm();

                if (!double.IsFinite(error))
                {
                    Debug.LogError($"[ED] Residual became non-finite after {iter} iterations.");
                    DebugProfiler.DebugMark(timeIteration);
                    return;
                }

                if (error < residualTolerance)
                {
                    DebugProfiler.DebugMark(timeIteration);
                    break;
                }

                var J = BuildJacobian(currentState, out double jNorm, rotationWeight, regularizationWeight, constraintWeight, clearanceWeight, slopeWeight);

                /*int nonZero = 0;
                int total = J.RowCount * J.ColumnCount;

                for (int r = 0; r < J.RowCount; r++)
                {
                    for (int c = 0; c < J.ColumnCount; c++)
                    {
                        if (Math.Abs(J[r, c]) > 1e-12)
                            nonZero++;
                    }
                }

                Debug.Log($"Jacobian density (iteration {iter}): {(100.0 * nonZero / total):F2}% ({nonZero}/{total})");*/

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                {
                    DebugProfiler.DebugMark(timeIteration);
                    break;
                }

                var JT = J.Transpose();

                var H = JT * J; // approximate Hessian
                var g = JT * f; // gradient term

                Vector<double> delta = null;
                EDState acceptedState = null;
                bool solved = false;

                // Try current lambda, optionally increasing it if solve or step is bad.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    DebugProfiler.DebugMark(timeSolve);

                    if (choleskyFactorization)
                    {
                        if (!TrySolveCholeskyWithDamping(H, g, currentLambda, out delta, out double usedLambda))
                        {
                            currentLambda = usedLambda;
                            continue;
                        }

                        currentLambda = usedLambda;
                    }
                    else
                    {
                        var Hlm = H.Clone();

                        for (int i = 0; i < Hlm.RowCount; i++)
                            Hlm[i, i] += currentLambda;

                        try
                        {
                            delta = Hlm.Solve(-g);
                        }
                        catch
                        {
                            delta = null;
                        }
                    }

                    DebugProfiler.DebugMark(timeSolve);

                    if (delta == null)
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    double stepNorm = delta.L2Norm();

                    if (!double.IsFinite(stepNorm))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    EDState candidateState;

                    try
                    {
                        candidateState = currentState.CloneAndApply(delta, 1.0);
                        ComputeClearance(candidateState);
                    }
                    catch
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    var candidateView = new EDStateView(candidateState);

                    var fCandidate = EvaluateResidualVector(candidateView, rotationWeight, regularizationWeight, constraintWeight, clearanceWeight, slopeWeight);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    // Accept only if it improves the residual.
                    if (candidateError <= error)
                    {
                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        if (stepNorm < stepTolerance)
                        {
                            currentState = acceptedState;

                            DebugProfiler.DebugMark(timeIteration);

                            Debug.Log($"Ran {iter} iterations...");
                            LogTimerReport();

                            return;
                        }

                        break;
                    }

                    currentLambda *= 10.0;
                }

                if (!solved)
                {
                    Debug.LogWarning("[ED] LM could not find an improving step.");
                    DebugProfiler.DebugMark(timeIteration);
                    break;
                }

                currentState = acceptedState;
                ComputeClearance(currentState);

                DebugProfiler.DebugMark(timeIteration);
            }

            Debug.Log($"Ran {iter} iterations...");
            LogTimerReport();
#else
    throw new NotImplementedException();
#endif
        }

        bool TrySolveCholeskyWithDamping(Matrix<double> H, Vector<double> g, double initialLambda, out Vector<double> delta, out double usedLambda)
        {
            delta = null;
            usedLambda = initialLambda;

            const int maxAttempts = 8;
            const double lambdaMultiplier = 10.0;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var Hlm = H.Clone();

                for (int i = 0; i < Hlm.RowCount; i++)
                    Hlm[i, i] += usedLambda;

                try
                {
                    var chol = Hlm.Cholesky();
                    delta = chol.Solve(-g);
                    return delta.All(v => double.IsFinite(v));
                }
                catch
                {
                    usedLambda *= lambdaMultiplier;
                }
            }

            return false;
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

        public void ClearStructure()
        {
            structure = new();
        }

        public void AddStructureSegment(Vector3 p1, Vector3 p2, Vector3 normal)
        {
            var c = (p1 + p2) * 0.5f;
            var n0 = normal.normalized;

            structure.Add(new NavEDSegments
            {
                p1 = p1.ToDVector3(),
                p2 = p2.ToDVector3(),
                center = c.ToDVector3(),
                normal = n0.ToDVector3()
            });
        }

        public void SetNavEDParameters(TopologyStatic navMeshTopology,
                                       float agentRadius, float maxSlope, float slopeSoftBand, Vector3 upVector,
                                       BindingSelectionMode bindMode, BindingWeightMode weightMode,
                                       int k = 4, // When BindingSelectionMode = closest-K
                                       float power = 2.0f,
                                       float sigma = 1.0f)
        {
            this.maxSlope = maxSlope;
            this.slopeSoftBand = slopeSoftBand;
            this.upVector = upVector.normalized;
            this.navMeshTopology = navMeshTopology;

            for (int i = 0; i < structure.Count; i++)
            {
                var seg = structure[i];
                seg.bind1 = GetBinding(seg.p1, bindMode, weightMode, k, power, sigma);
                seg.bind2 = GetBinding(seg.p2, bindMode, weightMode, k, power, sigma);

                // Build tangent space
                var dir = (seg.p2 - seg.p1).normalized;
                var t = DVector3.ProjectOnPlane(dir, seg.normal).normalized;
                var b = DVector3.Cross(seg.normal, t).normalized;

                seg.probeT = seg.center + agentRadius * 0.1 * t;
                seg.probeB = seg.center + agentRadius * 0.1 * b;

                seg.cBind = GetBinding(seg.center, bindMode, weightMode, k, power, sigma);
                seg.tBind = GetBinding(seg.probeT, bindMode, weightMode, k, power, sigma);
                seg.bBind = GetBinding(seg.probeB, bindMode, weightMode, k, power, sigma);
            }

            ComputeClearance(currentState);
            ComputeClearance(restState);

            LogClearance("Original clearance:", restState, restState);
        }

        public void LogCurrentClearance()
        {
            LogClearance("Current clearance:", restState, currentState);
        }

        void LogClearance(string title, EDState originalState, EDState currentState)
        {
            EDClearanceCache originalClearances = originalState.clearances;
            EDClearanceCache currentClearances = currentState.clearances;
            const double epsilon = 1e-8;

            string sb = $"{title}\n";

            double shrinkageSum = 0.0;
            double shrinkageSqSum = 0.0;
            double activeShrinkageSum = 0.0;
            int activeShrinkageCount = 0;

            double maxShrinkage = double.MinValue;
            int maxShrinkageIndex = -1;
            int invalidSegments = 0;

            for (int i = 0; i < structure.Count; i++)
            {
                double original = originalClearances.Get(i);
                double current = currentClearances.Get(i);

                if ((original == double.MaxValue) || (current == double.MaxValue))
                {
                    sb += $"Segment {i} = INVALID (orig = {original}, current = {current})\n";
                    invalidSegments++;
                    continue;
                }

                double shrinkage = (original - current) / Math.Max(original, epsilon);

                shrinkageSum += shrinkage;
                shrinkageSqSum += shrinkage * shrinkage;

                if (shrinkage > 0.0)
                {
                    activeShrinkageSum += shrinkage;
                    activeShrinkageCount++;
                }

                if (shrinkage > maxShrinkage)
                {
                    maxShrinkage = shrinkage;
                    maxShrinkageIndex = i;
                }

                sb += $"Segment {i} = {current} (orig = {original}, shrinkage = {shrinkage:P2})\n";
            }

            int validSegments = structure.Count - invalidSegments;

            double shrinkageMean = (validSegments > 0) ? (shrinkageSum / validSegments) : 0.0;
            double shrinkageVariance = (validSegments > 0) ? (shrinkageSqSum / validSegments - shrinkageMean * shrinkageMean) : 0.0;
            double activeShrinkageMean = (activeShrinkageCount > 0) ? (activeShrinkageSum / activeShrinkageCount) : 0.0;

            sb += "\n";
            sb += $"Shrinkage Mean = {shrinkageMean:P2}\n";
            sb += $"Shrinkage Variance = {shrinkageVariance:F6}\n";
            sb += $"Active Shrinkage Mean = {activeShrinkageMean:P2}\n";
            sb += $"Max Shrinkage = {maxShrinkage:P2} (Segment {maxShrinkageIndex})\n";
            sb += $"Invalid segments = {invalidSegments}\n";

            Debug.Log(sb);
        }

        EDClearanceCache ComputeClearance(EDState state)
        {
            return state.clearances = ComputeClearance(new EDStateView(state));
        }

        EDClearanceCache ComputeClearance(EDStateView state)
        {
            DebugProfiler.DebugMark(timeUpdateClearance);

            var ret = new EDClearanceCache(structure.Count);

            Parallel.For(0, structure.Count, index =>
            {
                var seg = structure[index];

                var p1 = DeformVertex(seg.p1, seg.bind1, state);
                var p2 = DeformVertex(seg.p2, seg.bind2, state);

                if (GetClearance(state, p1, p2, out var clearance))
                {
                    ret.Set(index, clearance);
                }
                else
                {
                    ret.Set(index, double.MaxValue);
                }
            });

            DebugProfiler.DebugMark(timeUpdateClearance);

            return ret;
        }

        private double EvaluateSingleClearanceResidual(EDStateView state, int index, double wClearance)
        {
            double original = restState.GetClearance(index);

            // Always calculates based on current deformation
            var seg = structure[index];

            var p1 = DeformVertex(seg.p1, seg.bind1, state);
            var p2 = DeformVertex(seg.p2, seg.bind2, state);

            if (!GetClearance(state, p1, p2, out var current))
            {
                // Can't compute clearance, likely due to the segment being outside the navmesh, a degenerate segment or numerical issues - set it to double.MaxValue to be able to identify it and make a loss of zero in that case
                return 0.0;
            }

            double loss = ComputeClearanceLoss(original, current);
            return wClearance * loss;
        }

        private double EvaluateSingleSlopeResidual(EDStateView state, int segmentIndex, double wSlope)
        {
            // Normalized hinge:
            //   0 at or below maxSlope - softBand
            //   1 at maxSlope
            //   >1 beyond maxSlope
            // -------------------------------------------------------------
            double hardAngleDeg = maxSlope;
            double softAngleDeg = Math.Max(0.0, maxSlope - slopeSoftBand);

            double hardAngle = hardAngleDeg * Math.PI / 180.0;
            double softAngle = softAngleDeg * Math.PI / 180.0;

            double hardDot = Math.Cos(hardAngle);
            double softDot = Math.Cos(softAngle);

            double denom = Math.Max(softDot - hardDot, 1e-12);

            Vector3 upNorm = upVector.normalized;
            Vector3 segNormal = GetTransformedSegmentSlopeNormal(state, segmentIndex);

            double penalty;

            if (segNormal.sqrMagnitude < 1e-12f)
            {
                // Degenerate frame: strongly invalid.
                penalty = 1.0;
            }
            else
            {
                segNormal.Normalize();

                double dp = Vector3.Dot(segNormal, upNorm);
                dp = Math.Clamp(dp, -1.0, 1.0);

                penalty = Math.Max(0.0, (softDot - dp) / denom);
            }

            return wSlope * penalty;
        }

        private double ComputeClearanceLoss(double original, double current)
        {
            if ((original == double.MaxValue) || (current == double.MaxValue))
            {
                // Can't compute clearance, likely due to the segment being outside the navmesh, a degenerate segment or numerical issues - return zero loss in that case
                return 0;
            }
            // Simple hinge loss that only penalizes clearance reductions, not increases - dependent on the world scale
            //return Math.Max(0.0, original - current);

            const double epsilon = 1e-3; // Small value to prevent division by zero and very large losses when original clearance is very small
            const double power = 1.0; // Exponent to control how aggressively we penalize clearance reductions - higher values will focus more on smaller reductions - > 1 works bad, there's probably an issue somewhere
            return Math.Max(0, Math.Pow((original - current) / (original + epsilon), power));
        }

        bool GetClearance(EDStateView state, DVector3 p1, DVector3 p2, out double minClearance)
        {
            minClearance = double.MaxValue;

            // Always calculates based on current deformation
            DVector3 dir = p2 - p1;
            if (dir.sqrMagnitude < 1e-3) return false;

            double maxDist = dir.magnitude;
            dir /= maxDist;

            foreach (var edge in navMeshTopology.edges)
            {
                if (!edge.isBoundary) continue;

                if (IsConnectorEdge(edge)) continue;


                var e1 = DeformVertex(restVertices[edge.vertices.i1], bindings[edge.vertices.i1], state);
                var e2 = DeformVertex(restVertices[edge.vertices.i2], bindings[edge.vertices.i2], state);

                // Edge projection interval [minT, maxT] onto p1->p2
                double t1 = DVector3.Dot(e1 - p1, dir);
                double t2 = DVector3.Dot(e2 - p1, dir);
                double minT = Math.Min(t1, t2);
                double maxT = Math.Max(t1, t2);

                // No overlap with segment [0,maxDist]
                if ((maxT < 0.0f) || (minT > maxDist)) continue;

                double d = LineHelpers.Distance(p1, p2, e1, e2, out var closestP, out var closestE);
                if (d < minClearance)
                {
                    minClearance = d;
                }
            }

            return (minClearance != double.MaxValue);
        }

        bool IsConnectorEdge(TopologyStatic.TEdge edge)
        {
            foreach (var h in handleConstraints)
            {
                if ((h.vertexIndices.Contains(edge.vertices.i1)) &&
                    (h.vertexIndices.Contains(edge.vertices.i2))) return true;
            }

            return false;
        }

        public int GetSegmentCount() => structure.Count;

        public (Vector3, Vector3) GetSegment(int segIndex) => GetTransformedSegment(new EDStateView(currentState), segIndex);
        private (Vector3, Vector3) GetTransformedSegment(EDStateView state, int segIndex)
        {
            var seg = structure[segIndex];

            var dp1 = DeformVertex(seg.p1, seg.bind1, state);
            var dp2 = DeformVertex(seg.p2, seg.bind2, state);

            return (dp1.ToVector3(), dp2.ToVector3());
        }

        public Vector3 GetSegmentSlopeDirection(int segIndex)
        {
            (var p1, var p2) = GetSegment(segIndex);
            Vector3 dir = p2 - p1;
            float len = dir.magnitude;

            if (len < 1e-6f) return Vector3.zero;

            dir /= len;

            return dir;
        }

        public Vector3 GetSegmentSlopeNormal(int segIndex) => GetTransformedSegmentSlopeNormal(new EDStateView(currentState), segIndex);

        Vector3 GetTransformedSegmentSlopeNormal(EDStateView state, int segIndex)
        {
            var seg = structure[segIndex];

            var q0 = DeformVertex(seg.center, seg.cBind, state);
            var qT = DeformVertex(seg.probeT, seg.tBind, state);
            var qB = DeformVertex(seg.probeB, seg.bBind, state);

            DVector3 t = qT - q0;
            DVector3 b = qB - q0;

            DVector3 n = DVector3.Cross(t, b);

            if (n.sqrMagnitude < 1e-12f) return Vector3.zero;

            n.Normalize();

            return n.ToVector3();
        }

        public double GetClearance(int segIndex)
        {
            return currentState.GetClearance(segIndex);
        }

        public void ClearTimers()
        {
            timePack = new();
            timeIteration = new();
            timeResidualEvaluate = new();
            timeJacobianBuild = new();
            timeSolve = new();
            timeApplyParameters = new();
            timeUpdateClearance = new();
            timeJacobianBuildConstraint = new();
            timeJacobianBuildRotation = new();
            timeJacobianBuildRegularization = new();
            timeJacobianBuildSlope = new();
            timeJacobianBuildClearance = new();
        }

        public void LogTimerReport()
        {
            string sb = $"Time report:\n";
            sb += $"  Pack parameters: {timePack.accumulatedTimeMS:F6} ms\n";
            sb += $"  Iteration time: {timeIteration.accumulatedTimeMS:F6} ms\n";
            sb += $"    Residual evaluation: {timeResidualEvaluate.accumulatedTimeMS:F6} ms\n";
            sb += $"    Build Jacobian: {timeJacobianBuild.accumulatedTimeMS:F6} ms\n";
            sb += $"      Constraints: {timeJacobianBuildConstraint.accumulatedTimeMS:F6} ms\n";
            sb += $"      Rotation: {timeJacobianBuildRotation.accumulatedTimeMS:F6} ms\n";
            sb += $"      Regularization: {timeJacobianBuildRegularization.accumulatedTimeMS:F6} ms\n";
            sb += $"      Slope: {timeJacobianBuildSlope.accumulatedTimeMS:F6} ms\n";
            sb += $"      Clearance: {timeJacobianBuildClearance.accumulatedTimeMS:F6} ms\n";
            sb += $"    Solve time: {timeSolve.accumulatedTimeMS:F6} ms\n";
            sb += $"    Apply parameters: {timeApplyParameters.accumulatedTimeMS:F6} ms\n";
            sb += $"    Clearance calculation: {timeUpdateClearance.accumulatedTimeMS:F6} ms\n";
            Debug.Log(sb);
        }

        public Vector3 GetDebugNodePosition(int nodeIndex)
        {
            var node = nodes[nodeIndex];

            return (currentState.TransformOffset(nodeIndex, DVector3.zero) + node.restPosition).ToVector3();
        }
    }
}
#endif