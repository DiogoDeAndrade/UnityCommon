using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    [Serializable]
    public partial class EmbededDeformation
    {
        public DeformationGraphSource deformationGraphSource;
        public DVector3[] restVertices;
        public int[] triangles;

        public List<EDNode> nodes = new();
        public EDVertexBinding[] bindings;
        public List<EDHandleConstraint> handleConstraints = new();

        // Which navmesh edges span an opening, and so must not be measured against for clearance.
        // Supplied by the caller rather than derived from handleConstraints - see EDClearanceOpening.
        [SerializeField, HideInInspector]
        private List<EDClearanceOpening> clearanceOpenings = new();
        public List<EDVertexConstraint> vertexConstraints = new();
        public List<EDTerminalConstraint> terminalConstraints = new();
        public List<EDLinkAngleConstraint> linkAngleConstraints = new();
        public TopologyStatic navMeshTopology;

        /// <summary>
        /// Whether the navigation data the nav-aware energies measure through has been built - the
        /// per-segment bindings and clearance probes. Only a navigation run builds them, so the
        /// navigation-aware features are simply unavailable in the TranslationOnly and plain ED
        /// modes and callers must not assume otherwise.
        ///
        /// The navmesh topology is necessary but no longer sufficient to say so, which is why
        /// navigationDataBuilt is tested as well: the topology is handed to every run now, so that
        /// a builder can sample it, and a run whose graph came off the navmesh is not thereby a
        /// navigation run.
        ///
        /// Tests the edge data rather than the reference: TopologyStatic is [Serializable], so a
        /// topology that was null when the scene was written comes back as a live object with a
        /// null edge list, and a reference test would wrongly report it as configured.
        /// </summary>
        public bool isNavConfigured => (navigationDataBuilt) && (navMeshTopology != null) && (navMeshTopology.edgeCount > 0);
        public List<NavEDSegments> structure;
        public float maxSlope = 45.0f;
        public float slopeSoftBand = 5.0f;
        public Vector3 upVector
        {
            get => _upVector;
            set
            {
                _upVector = value.normalized;
                _upVectorD = value.ToDVector3().normalized;
            }
        }
        private Vector3 _upVector = Vector3.up;
        private DVector3 _upVectorD = DVector3.up;
        public double clearanceMinRatio = 0.85;
        public double segmentMinRatio = 0.85;

        [SerializeField, HideInInspector]
        private EDState currentState;
        [SerializeField, HideInInspector]
        // Widened as terms adopt it - the clearance term measures against the rest clearances.
        internal EDState restState;
        // Deliberately not serialized. The per-cell distance/nodeId/weight arrays are the bulk of the
        // whole scene when they are - 14.4 MB of an 18.7 MB SampleScene, re-uploaded to LFS on every
        // commit that touches the scene. It is derived data: BuildDeformationField repopulates it
        // inside Build(), so the only thing serializing it bought was skipping a rebuild on reload.
        //
        // It also repairs usesDeformationField. As a [SerializeField] this came back from a domain
        // reload live but empty, so the "!= null" half of that guard was a no-op in structure mode
        // and the field path ran against an empty grid. Now the reference is genuinely null until
        // Build() runs and the guard means what it says.
        [NonSerialized]
        private FullDeformationField    deformationField;
        [SerializeField, HideInInspector]
        private List<Mesh>  sourceGeometry;


#if UC_PROFILER_ENABLE
        // The per-term breakdown lives on the terms themselves rather than as a fixed list here.
        // A hardcoded list only described the blocks that existed when it was written - it never
        // gained orientation, segment length, link angle or the terminal terms - and it could not
        // describe a configuration that does not use one of them. Whatever the model carries gets
        // measured; nothing else appears.
        internal DebugProfiler timeIteration;
        internal DebugProfiler timeResidualEvaluate;
        internal DebugProfiler timeJacobianBuild;
        internal DebugProfiler timeSolve;
        internal DebugProfiler timeUpdateClearance;

        // Marked by the caller that generates output geometry - the subdivision and simplification
        // are mesh operations it owns, not the deformation's. Reported here so a run is described by
        // one report rather than by a report plus a scattering of separate log lines.
        /// <summary>
        /// The field rebuild, which happens before the solve rather than inside it. Owned here so it
        /// lands in the same report as everything else it competes with for a press of the button.
        /// </summary>
        public DebugProfiler timeFieldRebuild;
        public DebugProfiler timeOutputSubdivide;
        public DebugProfiler timeOutputDeform;
        public DebugProfiler timeOutputSimplify;

        // Solver iterations actually run, so the report can divide by it. Comparing solvers on
        // totals alone is misleading when they run different numbers of iterations.
        private int solveIterations;
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

        public void SetSourceGeometry(List<Mesh> meshes, List<Matrix4x4> matrices)
        {
            sourceGeometry = new();
            for (int i = 0; i < meshes.Count; i++)
            {
                sourceGeometry.Add(meshes[i].BakeTransform(matrices[i]));
            }
        }

        /// <summary>
        /// Undirected edge count of the built graph. Surfaced so a builder can report what it
        /// produced without walking the neighbour lists itself.
        /// </summary>
        public int graphEdgeCount => deformGraphEdgeCount;

        private DVector3[] BuildNodeSurfaceNormals()
        {
            DVector3 referenceUp = (_upVector.sqrMagnitude > 1e-8f) ? (_upVector.ToDVector3().normalized) : (DVector3.up);

            DVector3[] sums = new DVector3[nodes.Count];

            double[] weights = new double[nodes.Count];

            for (int i = 0; i < structure.Count; i++)
            {
                NavEDSegments segment = structure[i];

                DVector3 normal = segment.normal;

                if (normal.sqrMagnitude < 1e-8) continue;

                normal.Normalize();

                if (DVector3.Dot(normal, referenceUp) < 0.0)
                    normal = -normal;

                double length = (segment.p2 - segment.p1).magnitude;

                double weight = Math.Max(length, 1e-6);

                if (segment.node1 >= 0)
                {
                    sums[segment.node1] += weight * normal;
                    weights[segment.node1] += weight;
                }

                if (segment.node2 >= 0)
                {
                    sums[segment.node2] += weight * normal;
                    weights[segment.node2] += weight;
                }
            }

            DVector3[] result = new DVector3[nodes.Count];

            for (int i = 0; i < nodes.Count; i++)
            {
                result[i] = ((weights[i] > 0.0) && (sums[i].sqrMagnitude > 1e-8)) ? (sums[i].normalized) : (referenceUp);
            }

            return result;
        }

        private DVector3 ComputeNodeForward(int nodeIndex)
        {
            const double epsilon = 1e-8;

            if ((nodes == null) || (nodeIndex < 0) || (nodeIndex >= nodes.Count))
            {
                return DVector3.forward;
            }

            EDNode node = nodes[nodeIndex];
            DVector3 p = node.restPosition;

            if ((node.neighbors == null) || (node.neighbors.Count == 0))
            {
                return DVector3.forward;
            }

            if (node.neighbors.Count == 1)
            {
                // Leaf: point outward from its only neighbour.
                int neighbourIndex = node.neighbors[0];

                DVector3 neighbourPosition = nodes[neighbourIndex].restPosition;

                DVector3 forward = p - neighbourPosition;

                return (forward.sqrMagnitude > epsilon) ? (forward.normalized) : (DVector3.forward);
            }

            if (node.neighbors.Count == 2)
            {
                // Chain node: tangent running through both adjacent nodes.
                DVector3 a = nodes[node.neighbors[0]].restPosition;
                DVector3 b = nodes[node.neighbors[1]].restPosition;

                DVector3 forward = b - a;

                if (forward.sqrMagnitude > epsilon) return forward.normalized;

                // Degenerate fallback: use either incident edge.
                forward = a - p;

                if (forward.sqrMagnitude > epsilon) return forward.normalized;

                forward = b - p;

                return (forward.sqrMagnitude > epsilon) ? (forward.normalized) : (DVector3.forward);
            }

            // Junction: average the normalized outgoing edge directions.
            DVector3 sum = DVector3.zero;

            for (int i = 0; i < node.neighbors.Count; i++)
            {
                int neighbourIndex = node.neighbors[i];

                DVector3 neighbourPosition = nodes[neighbourIndex].restPosition;

                DVector3 direction = neighbourPosition - p;

                if (direction.sqrMagnitude > epsilon) sum += direction.normalized;
            }

            if (sum.sqrMagnitude > epsilon)
                return sum.normalized;

            // Directions cancelled out. Use the longest incident edge as a
            // more stable fallback than depending on neighbour-list order.
            DVector3 longestDirection = DVector3.zero;
            double longestLengthSq = 0.0;

            for (int i = 0; i < node.neighbors.Count; i++)
            {
                int neighbourIndex = node.neighbors[i];

                DVector3 direction = nodes[neighbourIndex].restPosition - p;

                double lengthSq = direction.sqrMagnitude;

                if (lengthSq > longestLengthSq)
                {
                    longestLengthSq = lengthSq;
                    longestDirection = direction;
                }
            }

            return (longestDirection.sqrMagnitude > epsilon) ? (longestDirection.normalized) : (DVector3.forward);
        }

        /// <summary>
        /// Orients every node from the skeleton running through it. Public because it is the
        /// structure builder that knows its graph has meaningful frames - a sampled graph does not.
        /// </summary>
        public void BuildNodeRestFrames()
        {
            if ((nodes == null) || (nodes.Count == 0))
                return;

            DVector3[] surfaceNormals = BuildNodeSurfaceNormals();

            for (int i = 0; i < nodes.Count; i++)
            {
                EDNode node = nodes[i];

                DVector3 forward = ComputeNodeForward(i);

                node.BuildSurfaceFrame(forward, surfaceNormals[i]);
            }
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
                    Debug.LogWarning($"GetBinding: unsupported link mode {bindMode}.");
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

        /// <summary>
        /// Declares which vertex groups span openings, for clearance measurement. Independent of the
        /// handle constraints on purpose: what a piece is pinned by and where a piece opens are
        /// different questions that happen to have the same answer today.
        /// </summary>
        public void SetClearanceOpenings(List<EDClearanceOpening> openings)
        {
            clearanceOpenings = (openings != null) ? (new List<EDClearanceOpening>(openings)) : (new List<EDClearanceOpening>());
        }

        public void UpdateConstraints(List<EDHandleConstraint> handleData)
        {
            handleConstraints = new(handleData);

            vertexConstraints.Clear();
            terminalConstraints.Clear();

            foreach (var hc in handleData)
            {
                Matrix4x4 delta = hc.currentHandleMatrix * hc.restHandleMatrix.inverse;

                if (hc.vertexIndices != null)
                {
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

                // Orientation and scale are only defined for terminal
                // structure nodes.
                if ((!hc.isTerminal) || (deformationGraphSource != DeformationGraphSource.StructureOnly)) continue;

                Vector3 restHandlePosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero);

                int nodeIndex = GetClosestLeafNodeIndex(restHandlePosition);

                if ((nodeIndex < 0) || (nodeIndex >= nodes.Count)) continue;

                EDNode node =nodes[nodeIndex];

                Quaternion restRotation = hc.restHandleMatrix.ExtractMatrixRotation();

                Quaternion currentRotation = hc.currentHandleMatrix.ExtractMatrixRotation();

                Quaternion rotationDelta = currentRotation * Quaternion.Inverse(restRotation);

                DVector3 targetRight = (rotationDelta * node.restRight.ToVector3()).ToDVector3().normalized;

                DVector3 targetUp = (rotationDelta * node.restUp.ToVector3()).ToDVector3().normalized;

                DVector3 targetForward = (rotationDelta * node.restForward.ToVector3()).ToDVector3().normalized;

                // Local X/right is the connector-width axis.
                double restScale = hc.restHandleMatrix.GetMatrixAxisLength(0);

                double currentScale = hc.currentHandleMatrix.GetMatrixAxisLength(0);

                double targetScale = currentScale / Math.Max(restScale, 1e-8);

                DVector3 targetPosition = hc.currentHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();

                terminalConstraints.Add(new EDTerminalConstraint {
                    nodeIndex = nodeIndex,
                    targetPosition = targetPosition,

                    targetRight = targetRight,
                    targetUp = targetUp,
                    targetForward = targetForward,

                    targetScale = targetScale
                });
            }

            /*foreach (EDTerminalConstraint terminal in terminalConstraints)
            {
                Debug.Log(
                    $"Terminal node {terminal.nodeIndex}: " +
                    $"scale={terminal.targetScale:F3}, " +
                    $"right={terminal.targetRight}, " +
                    $"up={terminal.targetUp}, " +
                    $"forward={terminal.targetForward}"
                );
            }*/
        }

        /// <summary>
        /// Records the rest angle of every pair of links meeting at a node. Public for the same
        /// reason as BuildNodeRestFrames: only the structure builder produces a graph whose link
        /// angles mean anything.
        /// </summary>
        public void BuildLinkAngleConstraints()
        {
            linkAngleConstraints.Clear();

            if ((nodes == null) || (nodes.Count == 0))
                return;

            const double epsilon = 1e-12;

            for (int centerIndex = 0; centerIndex < nodes.Count; centerIndex++)
            {
                EDNode centerNode = nodes[centerIndex];

                if ((centerNode.neighbors == null) || (centerNode.neighbors.Count < 2))
                    continue;

                DVector3 center = centerNode.restPosition;
                DVector3 restUp = centerNode.restUp;

                if (restUp.sqrMagnitude < epsilon)
                    restUp = DVector3.up;
                else
                    restUp.Normalize();

                for (int a = 0; a < centerNode.neighbors.Count - 1; a++)
                {
                    int neighborA = centerNode.neighbors[a];
                    DVector3 directionA = nodes[neighborA].restPosition - center;

                    if (directionA.sqrMagnitude < epsilon)
                        continue;

                    directionA.Normalize();

                    for (int b = a + 1; b < centerNode.neighbors.Count;b++) 
                    {
                        int neighborB = centerNode.neighbors[b];

                        DVector3 directionB = nodes[neighborB].restPosition - center;

                        if (directionB.sqrMagnitude < epsilon)
                            continue;

                        directionB.Normalize();

                        double restCos = Math.Clamp(DVector3.Dot(directionA, directionB), -1.0, 1.0);

                        double restSin = Math.Clamp(DVector3.Dot(restUp, DVector3.Cross(directionA, directionB)), -1.0, 1.0);

                    linkAngleConstraints.Add(new EDLinkAngleConstraint {
                            centerNode = centerIndex,
                            neighborA = neighborA,
                            neighborB = neighborB,
                            restCos = restCos,
                            restSin = restSin
                        });
                    }
                }
            }

            Debug.Log($"Built {linkAngleConstraints.Count} structure link-angle constraints.");
        }

        public void ResetDeformation()
        {
            currentState = new EDState(nodes.Count);
            ComputeClearance(currentState);
        }

        /// <summary>
        /// Deforms the navmesh vertices the way this graph actually deforms things: through the
        /// volumetric field when there is one, and through the node bindings otherwise.
        ///
        /// There used to be two of these, drawn side by side to compare the field against the
        /// binding blend while the field was being developed. Now that the field is the deformation
        /// in structure mode, showing the binding blend alongside it only invited reading a
        /// discrepancy as an error.
        ///
        /// The binding path keeps using the precomputed per-vertex bindings rather than going
        /// through EDBindingDeformer, which would rebind every vertex from scratch for an identical
        /// answer.
        /// </summary>
        public Vector3[] DeformVertices()
        {
            Vector3[] deformed = new Vector3[restVertices.Length];

            if (usesDeformationField)
            {
                // One blender for the whole sweep, not one per vertex - the frames do not change
                // while it runs, which is the condition the freeze is built around.
                FullDeformationField.TransformBlender blender = CreateDebugFieldBlender();

                for (int vId = 0; vId < restVertices.Length; vId++)
                    deformed[vId] = blender.DeformPosition(restVertices[vId].ToVector3(), trilinear: true);

                return deformed;
            }

            WarnIfFieldMissing(nameof(DeformVertices));

            var state = new EDStateView(currentState);

            for (int vId = 0; vId < restVertices.Length; vId++)
                deformed[vId] = DeformVertex(restVertices[vId], bindings[vId], state).ToVector3();

            return deformed;
        }

        // Internal because the navmesh constraint term deforms its constrained vertices exactly the
        // way the legacy constraint block does.
        internal DVector3 DeformVertex(DVector3 v, EDVertexBinding binding, EDStateView state)
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

        /// <summary>
        /// A translation-only baseline: no rotations, no term machinery, one dense least-squares
        /// system of constrained vertices against a smoothness prior.
        ///
        /// It takes the energy model anyway, for the constraint weight alone. That weight has to be
        /// the same number the full problem uses or the baseline is not comparable, and the model is
        /// where that number now lives - so it is read rather than duplicated onto this solver.
        /// </summary>
        public bool SolveTranslationsOnly(EDEnergyModel.Instance energy, double smoothnessWeight, bool resetBeforeSolve = true)
        {
            if (resetBeforeSolve) ResetDeformation();

            InitMathNet();

            double constraintWeight = (energy != null) ? (energy.GetConceptualWeight("constraint")) : (0.0);

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
                DebugProfiler.DebugMark(timeSolve);

                tx = A.Solve(bx);
                ty = A.Solve(by);
                tz = A.Solve(bz);

                DebugProfiler.DebugMark(timeSolve);
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

        /// <summary>
        /// The up vector this deformation was configured with, already normalized by the setter.
        /// Deliberately not re-normalized: it used to be handed in raw and normalized here, and
        /// normalizing a unit vector again can move its last bit.
        /// </summary>
        private Vector3 GetSafeStructureUp()
        {
            return (_upVector.sqrMagnitude > 1e-8f) ? (_upVector) : (Vector3.up);
        }

        private Vector3 GetStructureSegmentNormal(Vector3 p1,
                                                  Vector3 p2,
                                                  int fallbackSegmentIndex,
                                                  TryGetSurfaceNormal tryGetSurfaceNormal)
        {
            Vector3 mid = (p1 + p2) * 0.5f;

            if ((tryGetSurfaceNormal != null) &&
                tryGetSurfaceNormal(mid, out Vector3 normal) &&
                normal.sqrMagnitude > 1e-8f)
            {
                return normal.normalized;
            }

            // Note that this cannot actually contribute during construction: every segment in
            // `structure` right now was added by the loop below, and its probe bindings are not
            // attached until BuildNavigationData runs at the end of the build. GetSegmentSlopeNormal
            // therefore returns zero here and we fall through to the up vector. Kept because the
            // branch is meaningful if this is ever called on an already-bound structure.
            if ((structure != null) &&
                (fallbackSegmentIndex >= 0) &&
                (fallbackSegmentIndex < structure.Count) &&
                (currentState != null))
            {
                normal = GetSegmentSlopeNormal(fallbackSegmentIndex);

                if (normal.sqrMagnitude > 1e-8f)
                    return normal.normalized;
            }

            return GetSafeStructureUp();
        }

        /// <summary>
        /// Snapshots the skeleton, subdividing anything longer than the given limit.
        ///
        /// The up vector is no longer a parameter: it is set once through SetAgentParameters and
        /// read from there, so a build cannot be given one that disagrees with the one every
        /// nav-aware energy measures against.
        /// </summary>
        public void BuildStructure(IEDStructureSource structureSource, float structureMaxSegmentLength, TryGetSurfaceNormal tryGetSurfaceNormal)
        {
            ClearStructure();

            if (structureSource == null)
                return;

            int sourceSegmentCount = structureSource.segmentCount;

            for (int i = 0; i < sourceSegmentCount; i++)
            {
                structureSource.GetSegment(i, out Vector3 p1, out Vector3 p2);

                var distance = Vector3.Distance(p1, p2);
                if (distance > 1e-3)
                {
                    if (structureMaxSegmentLength <= 1e-3)
                    {
                        Vector3 normal = GetStructureSegmentNormal(p1, p2, i, tryGetSurfaceNormal);

                        AddStructureSegment(p1, p2, normal);
                    }
                    else
                    {
                        int subdivision = Mathf.Max(1, Mathf.CeilToInt(distance / structureMaxSegmentLength));
                        float tInc = 1.0f / subdivision;

                        for (int k = 0; k < subdivision; k++)
                        {
                            Vector3 sp1 = Vector3.Lerp(p1, p2, k * tInc);
                            Vector3 sp2 = Vector3.Lerp(p1, p2, (k + 1) * tInc);

                            Vector3 normal = GetStructureSegmentNormal(sp1, sp2, i, tryGetSurfaceNormal);

                            AddStructureSegment(sp1, sp2, normal);
                        }
                    }
                }
            }
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

        /// <summary>
        /// Whether the volumetric field is what deforms things for this graph. Only the structure
        /// builder produces one.
        ///
        /// Both halves are still tested even though deformationField is [NonSerialized] and so is
        /// honestly null in navmesh mode. The graph source says what this deformation is meant to
        /// do; the reference says whether Build() has actually run. They answer different
        /// questions, and fieldMissing below is the case where they disagree.
        /// </summary>
        private bool usesDeformationField => (deformationGraphSource == DeformationGraphSource.StructureOnly) && (deformationField != null);

        internal bool UseDeformationFieldForClearance => usesDeformationField;

        /// <summary>
        /// A structure graph that should have a field and has not got one - the state after a domain
        /// reload or a scene load, before Build() has run.
        /// </summary>
        private bool fieldMissing => (deformationGraphSource == DeformationGraphSource.StructureOnly) && (deformationField == null);

        // Deliberately [NonSerialized], and that is the whole mechanism: it comes back false from
        // every domain reload, which is exactly when the warning needs to fire again.
        [NonSerialized]
        private bool warnedFieldMissing;

        /// <summary>
        /// Reports the fall back from field deformation to binding deformation, once per episode and
        /// re-armed whenever a field is built.
        ///
        /// This has to be loud. The fallback produces a plausible deformation rather than an
        /// obviously broken one, so a golden captured in this state is wrong in a way that reads as
        /// correct - configs 6 and 7 are the ones that can hit it.
        ///
        /// Called from the main-thread, once-per-pass entry points only - never from inside the
        /// parallel row work. This is pass-level reporting, not a per-point check.
        /// </summary>
        private void WarnIfFieldMissing(string context)
        {
            if (!fieldMissing) return;
            if (warnedFieldMissing) return;

            warnedFieldMissing = true;

            Debug.LogWarning($"{context}: structure graph has no deformation field, falling back to binding deformation. Build() has not run since the last scene load or domain reload - rebuild before trusting this result or capturing a golden.");
        }

        internal List<FullDeformationField.Frame> BuildNodeFrames(EDStateView state)
        {
            var frames = new List<FullDeformationField.Frame>(nodes.Count);

            for (int i = 0; i < nodes.Count; i++)
            {
                frames.Add(GetNodeFrame(i, state));
            }

            return frames;
        }

        /// <summary>
        /// The blender the field path deforms through, over a snapshot of the node frames.
        ///
        /// One per pass. It freezes the frames and precomputes each node's transform, so it is
        /// read-only afterwards and can be shared across the workers of a parallel clearance pass -
        /// which is exactly what the list of frames it replaces was being used for.
        ///
        /// Null when the field is not what deforms here, which is the same "field mode or not" flag
        /// the frame list was doing double duty as.
        /// </summary>
        internal FullDeformationField.TransformBlender CreateFieldBlender(EDStateView state)
        {
            if (deformationField == null) return null;

            return deformationField.CreateBlender(BuildNodeFrames(state));
        }

        private DVector3 DeformClearancePoint(DVector3 restPosition, EDVertexBinding standardBinding, EDStateView state, FullDeformationField.TransformBlender blender)
        {
            if (blender != null)
            {
                Vector3 deformed = blender.DeformPosition(restPosition.ToVector3(), trilinear: true);

                return deformed.ToDVector3();
            }

            return DeformVertex(restPosition, standardBinding, state);
        }

        private bool TryComputeSegmentClearance(EDStateView state, int segmentIndex, FullDeformationField.TransformBlender blender, out double clearance)
        {
            // Single gate for "can this segment's clearance be measured at all". GetClearance walks
            // navMeshTopology.edges and deforms through the per-segment bindings, and those only
            // exist once BuildNavigationData has run. Both callers - the cache builder and the
            // clearance residual - already treat false as "no clearance available", so this needs
            // no special handling downstream.
            if (!isNavConfigured)
            {
                clearance = double.MaxValue;
                return false;
            }

            (Vector3 p1, Vector3 p2) = GetTransformedSegment(state, segmentIndex, blender);

            return GetClearance(state, p1.ToDVector3(), p2.ToDVector3(), blender, out clearance);
        }

        EDClearanceCache ComputeClearance(EDState state)
        {
            return state.clearances = ComputeClearance(new EDStateView(state));
        }

        EDClearanceCache ComputeClearance(EDStateView state)
        {
            DebugProfiler.DebugMark(timeUpdateClearance);

            var ret = new EDClearanceCache((structure != null) ? (structure.Count) : (0));

            // Clearance is a navigation-aware measurement, and the per-segment bindings it deforms
            // through are supplied by BuildNavigationData, which only NavED mode calls. In the
            // other modes there is nothing to measure, so every segment reports the existing "no
            // clearance" marker instead of doing the work and dereferencing a null binding.
            if (!isNavConfigured)
            {
                for (int i = 0; i < ret.count; i++)
                    ret.Set(i, double.MaxValue);
                DebugProfiler.DebugMark(timeUpdateClearance);

                return ret;
            }

            // The solve reaches clearance without necessarily going through CreateDeformer, so this
            // path needs its own report. Here rather than in EDClearanceTerm.CreateRowScratch: this
            // runs once on the main thread before the parallel loop, that one runs per worker.
            WarnIfFieldMissing(nameof(ComputeClearance));

            if (UseDeformationFieldForClearance)
            {
                // Built once and read-only during the parallel loop, which is the whole reason a
                // blender freezes its transforms rather than tracking them.
                //
                // There used to be a per-worker scratch object here, carrying a private copy of the
                // node frames. It was constructed through the overload that leaves that copy null, so
                // every worker shared the one list anyway and the scratch carried nothing - it went
                // with the frames it was meant to hold.
                FullDeformationField.TransformBlender blender = CreateFieldBlender(state);

                Parallel.For(0, structure.Count, EDDiagnostics.parallelOptions, index =>
                {
                    bool valid = TryComputeSegmentClearance(state, index, blender, out double clearance);

                    ret.Set(index, (valid) ? (clearance) : (double.MaxValue));
                });
            }
            else
            {
                Parallel.For(0, structure.Count, EDDiagnostics.parallelOptions, index =>
                {
                    bool valid = TryComputeSegmentClearance(state, index, null, out double clearance);

                    ret.Set(index, (valid) ? (clearance) : (double.MaxValue));
                });
            }
            DebugProfiler.DebugMark(timeUpdateClearance);

            return ret;
        }

        private double EvaluateSingleClearanceResidual(EDStateView state, int segmentIndex, double wClearance, FullDeformationField.TransformBlender blender = null)
        {
            double original = restState.GetClearance(segmentIndex);

            // Fallback for serial callers. The optimized Jacobian path supplies
            // the blender explicitly, so it does not reach this allocation.
            if ((UseDeformationFieldForClearance) && (blender == null))
            {
                blender = CreateFieldBlender(state);
            }

            if (!TryComputeSegmentClearance(state, segmentIndex, blender, out double current))
            {
                return 0.0;
            }

            return wClearance * ComputeClearanceLoss(original, current);
        }

        internal double EvaluateSingleSlopeResidual(EDStateView state, int segmentIndex, double wSlope)
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

            Vector3 upNorm = _upVector.normalized;
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

        internal double EvaluateSingleNodeSlopeResidualStructure(EDStateView state, int nodeIndex, double wSlope)
        {
            double hardAngle = maxSlope * Math.PI / 180.0;

            double softAngle = Math.Max(0.0, maxSlope - slopeSoftBand) * Math.PI / 180.0;

            double hardDot = Math.Cos(hardAngle);
            double softDot = Math.Cos(softAngle);

            double denom = Math.Max(softDot - hardDot, 1e-12);

            DVector3 currentUp = GetTransformedNodeUp(state, nodeIndex);

            if (currentUp.sqrMagnitude < 1e-12) return wSlope;

            double dp = Math.Abs(DVector3.Dot(currentUp, _upVectorD));

            dp = Math.Clamp(dp, 0.0, 1.0);

            double penalty = Math.Max(0.0, (softDot - dp) / denom);

            return wSlope * penalty;
        }

        internal DVector3 EvaluateSingleOrientationResidual(EDStateView state, int segmentIndex, double wOrientation)
        {
            Vector3 current = GetTransformedSegmentSlopeNormal(state, segmentIndex);

            if (current.sqrMagnitude < 1e-12f)
            {
                // Degenerate local frame: strongly invalid.
                Vector3 fallback = structure[segmentIndex].normal.ToVector3();
                if (fallback.sqrMagnitude < 1e-12f)
                    fallback = _upVector.normalized;
                else
                    fallback.Normalize();

                return new DVector3(-wOrientation * fallback.x, -wOrientation * fallback.y, -wOrientation * fallback.z);
            }

            current.Normalize();

            Vector3 target = structure[segmentIndex].normal.ToVector3().SafeNormalized();

            return new DVector3(wOrientation * (current.x - target.x), wOrientation * (current.y - target.y), wOrientation * (current.z - target.z));
        }

        internal DVector3 EvaluateSingleNodeOrientationResidualStructure(EDStateView state, int nodeIndex, double wOrientation)
        {
            DVector3 currentUp = GetTransformedNodeUp(state, nodeIndex);

            DVector3 restUp = nodes[nodeIndex].restUp.normalized;

            if (currentUp.sqrMagnitude < 1e-12f)
                return -wOrientation * restUp;

            return wOrientation * (currentUp - restUp);
        }

        internal void EvaluateSingleLinkAngleResidual(EDStateView state, int constraintIndex, double wLinkAngle, out double cosineResidual, out double sineResidual)
        {
            const double epsilon = 1e-12;

            EDLinkAngleConstraint constraint = linkAngleConstraints[constraintIndex];
            DVector3 center = DeformStructureNodePosition(constraint.centerNode,state);
            DVector3 positionA = DeformStructureNodePosition(constraint.neighborA, state);
            DVector3 positionB = DeformStructureNodePosition(constraint.neighborB, state);
            DVector3 directionA = positionA - center;
            DVector3 directionB = positionB - center;

            if ((directionA.sqrMagnitude < epsilon) || (directionB.sqrMagnitude < epsilon))
            {
                // A collapsed link is strongly invalid.
                cosineResidual = wLinkAngle * (0.0 - constraint.restCos);
                sineResidual = wLinkAngle * (0.0 - constraint.restSin);

                return;
            }

            directionA.Normalize();
            directionB.Normalize();

            DVector3 currentUp = GetTransformedNodeUp(state, constraint.centerNode);

            if (currentUp.sqrMagnitude < epsilon)
                currentUp = nodes[constraint.centerNode].restUp.normalized;

            double currentCos = Math.Clamp(DVector3.Dot(directionA, directionB), -1.0, 1.0);
            double currentSin = Math.Clamp(DVector3.Dot(currentUp, DVector3.Cross(directionA, directionB)), -1.0, 1.0);

            cosineResidual = wLinkAngle * (currentCos - constraint.restCos);
            sineResidual = wLinkAngle * (currentSin - constraint.restSin);
        }

        private DVector3 GetTransformedNodeUp(EDStateView state, int nodeIndex)
        {
            DVector3 transformed = state.TransformVector(nodeIndex, nodes[nodeIndex].restUp);

            if (transformed.sqrMagnitude < 1e-12) return DVector3.zero;

            return transformed.normalized;
        }

        /// <summary>
        /// How far a segment has been crushed below a fraction of its rest length, weighted. Zero
        /// when it is longer than the floor - stretching is not a problem.
        ///
        /// Both graph sources use this. The endpoints come from the deformed nodes when the graph
        /// came from the structure and the segment knows them, since there the nodes *are* the
        /// structure, and from the vertex bindings otherwise.
        /// </summary>
        internal double EvaluateSingleSegmentLengthResidual(EDStateView state, int segmentIndex, double wSegmentLength)
        {
            NavEDSegments seg = structure[segmentIndex];

            DVector3 p1;
            DVector3 p2;

            if ((deformationGraphSource == DeformationGraphSource.StructureOnly) &&
                (seg.node1 >= 0) &&
                (seg.node2 >= 0))
            {
                p1 = DeformStructureNodePosition(seg.node1, state);
                p2 = DeformStructureNodePosition(seg.node2, state);
            }
            else
            {
                p1 = DeformVertex(seg.p1, seg.bind1, state);
                p2 = DeformVertex(seg.p2, seg.bind2, state);
            }

            double originalLength = (seg.p2 - seg.p1).magnitude;

            if (originalLength < 1e-8)
                return 0.0;

            double currentLength = (p2 - p1).magnitude;

            double minRatio = Math.Clamp(segmentMinRatio, 0.0, 1.0);

            double minAllowedLength = minRatio * originalLength;

            double shrinkage = Math.Max(0.0, minAllowedLength - currentLength);

            return wSegmentLength * shrinkage / originalLength;
        }

        internal DVector3 EvaluateSingleTerminalOrientationResidual(EDStateView state, int terminalIndex, double wTerminalOrientation)
        {
            EDTerminalConstraint terminal = terminalConstraints[terminalIndex];

            if (!TryGetNodeRotation(state, terminal.nodeIndex, out Quaternion currentRotation))
            {
                return new DVector3(wTerminalOrientation * Math.PI, 0.0, 0.0);
            }

            Vector3 targetForward = terminal.targetForward.ToVector3().normalized;
            Vector3 targetUp = terminal.targetUp.ToVector3().normalized;

            Quaternion targetRotation = Quaternion.LookRotation(targetForward, targetUp);

            Quaternion rotationError = Quaternion.Inverse(targetRotation) * currentRotation;

            return wTerminalOrientation * QuaternionRotationVector(rotationError);
        }

        internal double EvaluateSingleTerminalScaleResidual(EDStateView state, int terminalIndex, double wTerminalScale)
        {
            EDTerminalConstraint terminal = terminalConstraints[terminalIndex];

            EDNode node = nodes[terminal.nodeIndex];

            DVector3 transformedRight = state.TransformVector(terminal.nodeIndex, node.restRight);

            double currentScale = transformedRight.magnitude;
            double targetScale = Math.Max(terminal.targetScale, 1e-8);

            return wTerminalScale * (currentScale - targetScale);
        }

        internal double ComputeClearanceLoss(double original, double current)
        {
            if ((original == double.MaxValue) || (current == double.MaxValue))
                return 0.0;

            const double epsilon = 1e-3;

            // Optional world-space slack, useful when original clearance is large.
            const double absoluteSlack = 0.05;

            double allowedByRatio = original * clearanceMinRatio;
            double allowedBySlack = Math.Max(0.0, original - absoluteSlack);

            // More permissive of the two.
            double allowed = Math.Min(allowedByRatio, allowedBySlack);

            double loss = (allowed - current) / (original + epsilon);

            return Math.Max(0.0, loss);
        }

        bool GetClearance(EDStateView state, DVector3 p1, DVector3 p2, FullDeformationField.TransformBlender blender, out double minClearance)
        {
            minClearance = double.MaxValue;

            DVector3 dir = p2 - p1;

            if (dir.sqrMagnitude < 1e-3) return false;

            double maxDist = dir.magnitude;
            dir /= maxDist;

            foreach (var edge in navMeshTopology.edges)
            {
                if (!edge.isBoundary) continue;
                if (IsOpeningEdge(edge)) continue;

                DVector3 e1 = DeformClearancePoint(restVertices[edge.vertices.i1], bindings[edge.vertices.i1], state, blender);
                DVector3 e2 = DeformClearancePoint(restVertices[edge.vertices.i2], bindings[edge.vertices.i2], state, blender);

                double t1 = DVector3.Dot(e1 - p1, dir);
                double t2 = DVector3.Dot(e2 - p1, dir);

                double minT = Math.Min(t1, t2);
                double maxT = Math.Max(t1, t2);

                // Skip edges too far along the segment's own axis to beat the best distance found so
                // far. The margin is what makes this sound: the measurement below is segment to
                // segment, so a wall just off the end of this segment is a real candidate, and
                // culling on overlap alone threw it away. That cost nothing at rest, where the
                // walls run alongside the structure and always overlap it, and produced a clearance
                // several times too large once a segment had rotated near a corner.
                //
                // minClearance starts at double.MaxValue, so the first pass culls nothing and the
                // bound tightens as better candidates appear.
                if ((maxT < -minClearance) || (minT > maxDist + minClearance)) continue;

                double distance = LineHelpers.Distance(p1, p2, e1, e2, out _, out _);

                if (distance < minClearance) minClearance = distance;
            }

            return minClearance != double.MaxValue;
        }

        /// <summary>
        /// Whether this edge spans an opening, and so should be ignored when measuring clearance.
        /// </summary>
        bool IsOpeningEdge(TopologyStatic.TEdge edge)
        {
            if (clearanceOpenings == null) return false;

            foreach (var opening in clearanceOpenings)
            {
                if (opening?.vertexIndices == null) continue;

                // Both endpoints in the same opening. Two endpoints in different openings are not
                // an edge across either of them.
                if ((opening.vertexIndices.Contains(edge.vertices.i1)) &&
                    (opening.vertexIndices.Contains(edge.vertices.i2))) return true;
            }

            return false;
        }

        // Internal because the structure rotation term needs the same answer the legacy rotation
        // block does about which nodes have their right-axis scale dictated by a terminal.
        internal bool HasTerminalScaleConstraint(int nodeIndex)
        {
            if (terminalConstraints == null)
                return false;

            for (int i = 0; i < terminalConstraints.Count; i++)
            {
                if (terminalConstraints[i].nodeIndex == nodeIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetNodeRotation(EDStateView state, int nodeIndex, out Quaternion rotation)
        {
            const float epsilon = 1e-8f;

            rotation = Quaternion.identity;

            if ((nodeIndex < 0) || (nodeIndex >= nodes.Count))
            {
                return false;
            }

            EDNode node = nodes[nodeIndex];

            DVector3 right = state.TransformVector(nodeIndex, node.restRight);
            DVector3 up = state.TransformVector(nodeIndex, node.restUp);
            DVector3 forward = state.TransformVector(nodeIndex, node.restForward);

            if (forward.sqrMagnitude < epsilon)
            {
                if ((right.sqrMagnitude > epsilon) && (up.sqrMagnitude > epsilon))
                {
                    forward = DVector3.Cross(right, up);
                }
            }

            if (forward.sqrMagnitude < epsilon)
                return false;

            forward.Normalize();

            // Remove scale and shear from the orientation measurement.
            up = DVector3.ProjectOnPlane(up, forward);

            if ((up.sqrMagnitude < epsilon) && (right.sqrMagnitude > epsilon))
            {
                up = DVector3.Cross(forward, right);
            }

            if (up.sqrMagnitude < epsilon)
            {
                DVector3 fallback = (Math.Abs(DVector3.Dot(forward, DVector3.up)) < 0.95f) ? (DVector3.up) : (DVector3.right);

                up = DVector3.ProjectOnPlane(fallback, forward);
            }

            if (up.sqrMagnitude < epsilon) return false;

            up.Normalize();

            rotation = Quaternion.LookRotation(forward.ToVector3(), up.ToVector3());

            return true;
        }

        private static DVector3 QuaternionRotationVector(Quaternion rotation)
        {
            Quaternion q = rotation.normalized;

            // Select the shortest quaternion representation.
            if (q.w < 0.0f)
            {
                q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            }

            Vector3 vectorPart = new Vector3(q.x, q.y, q.z);

            double sinHalfAngle = vectorPart.magnitude;

            if (sinHalfAngle < 1e-8)
            {
                // log(q) ~= 2v close to identity.
                return new DVector3(2.0 * q.x, 2.0 * q.y, 2.0 * q.z);
            }

            double angle = 2.0 * Math.Atan2(sinHalfAngle, Math.Clamp(q.w, -1.0f, 1.0f));

            Vector3 axis = vectorPart / (float)sinHalfAngle;

            return angle * axis.ToDVector3();
        }

        public int GetSegmentCount() => structure.Count;

        public (Vector3, Vector3) GetSegment(int segmentIndex)
        {
            EDStateView state = new EDStateView(currentState);

            FullDeformationField.TransformBlender blender = (UseDeformationFieldForClearance) ? (CreateFieldBlender(state)) : (null);

            // Its own blender, because this is called serially by the debug drawing rather than from
            // inside a pass that already has one. Nothing overrides on it, so there is no ownership
            // question to answer here.
            return GetTransformedSegment(state, segmentIndex, blender);
        }

        private (Vector3, Vector3) GetTransformedSegment(EDStateView state, int segmentIndex, FullDeformationField.TransformBlender blender)
        {
            NavEDSegments segment = structure[segmentIndex];

            // Without BuildNavigationData the endpoint bindings are null, so there is nothing to
            // deform the segment through and it is still at its rest position. The deformation
            // field path ignores the bindings entirely, so it is left alone.
            if ((blender == null) && (!IsSegmentBound(segment)))
                return (segment.p1.ToVector3(), segment.p2.ToVector3());

            DVector3 p1 = DeformClearancePoint(segment.p1, segment.bind1, state, blender);

            DVector3 p2 = DeformClearancePoint(segment.p2, segment.bind2, state, blender);

            return (p1.ToVector3(), p2.ToVector3());
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

        /// <summary>
        /// True once BuildNavigationData has attached the endpoint, centre and probe bindings a
        /// segment needs before anything can be evaluated on it. All five are attached together,
        /// so this is all-or-nothing per structure.
        ///
        /// BuildStructure produces segments with none of them, and only NavED mode ever
        /// runs BuildNavigationData, so unbound segments are the normal case in the other modes.
        /// </summary>
        private static bool IsSegmentBound(NavEDSegments seg)
        {
            return (seg != null) &&
                   (seg.bind1.nodeIndices != null) &&
                   (seg.bind2.nodeIndices != null) &&
                   (seg.cBind.nodeIndices != null) &&
                   (seg.tBind.nodeIndices != null) &&
                   (seg.bBind.nodeIndices != null);
        }

        Vector3 GetTransformedSegmentSlopeNormal(EDStateView state, int segIndex)
        {
            var seg = structure[segIndex];

            // The probe bindings are only built by BuildNavigationData, so they are still null
            // while BuildStructure is running, and in every mode that never calls it.
            // An unbound segment simply has no slope normal to report.
            if (!IsSegmentBound(seg))
                return Vector3.zero;

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
            timeIteration = new();
            timeResidualEvaluate = new();
            timeJacobianBuild = new();
            timeSolve = new();
            timeUpdateClearance = new();

            timeFieldRebuild = new();

            timeOutputSubdivide = new();
            timeOutputDeform = new();
            timeOutputSimplify = new();

            solveIterations = 0;
        }

        /// <summary>
        /// Counted by each solver as it completes an iteration, so the report can express costs per
        /// iteration as well as in total.
        /// </summary>
        internal void CountSolveIteration()
        {
            solveIterations++;
        }

        /// <summary>
        /// One report describing a whole run: the solve, its breakdown per energy, and the output
        /// geometry.
        ///
        /// Totals and per-iteration averages both, because the two answer different questions and
        /// only one of them is comparable across solvers. A solver that converges in three
        /// iterations and one that grinds through ten are not usefully compared on total time; what
        /// costs what *per iteration* is the part that transfers.
        ///
        /// The per-term timers are passed in rather than read from an energy model, so the breakdown
        /// lists exactly the terms this run used, in the order they were evaluated, and so this
        /// signature names no Math.NET type - the model's Instance only exists when Math.NET does.
        /// A solver that uses no terms at all - the translation-only baseline - reports no
        /// breakdown.
        /// </summary>
        public void LogTimerReport(string label, IReadOnlyList<(string name, DebugProfiler timer)> termTimers = null)
        {
            var sb = new StringBuilder();

            int iterations = Math.Max(1, solveIterations);

            void Row(string name, DebugProfiler timer, int indent, bool perIteration = true)
            {
                if (timer == null) return;

                double total = timer.accumulatedTimeMS;

                string line = new string(' ', indent) + name.PadRight(30 - indent) + $"{total,10:F3} ms";

                // Per-iteration only where it means something. The output stages run once per solve,
                // not once per iteration, so dividing them by the iteration count would invite
                // exactly the comparison the average exists to prevent.
                if ((perIteration) && (solveIterations > 0))
                    line += $"   ({total / iterations,8:F3} ms/iter)";

                sb.AppendLine(line);
            }

            double fieldTotal = ((timeFieldRebuild != null) ? (timeFieldRebuild.accumulatedTimeMS) : (0.0));
            double solveTotal = ((timeIteration != null) ? (timeIteration.accumulatedTimeMS) : (0.0));
            double outputTotal = ((timeOutputSubdivide != null) ? (timeOutputSubdivide.accumulatedTimeMS) : (0.0))
                               + ((timeOutputDeform != null) ? (timeOutputDeform.accumulatedTimeMS) : (0.0))
                               + ((timeOutputSimplify != null) ? (timeOutputSimplify.accumulatedTimeMS) : (0.0));

            sb.AppendLine($"Time report - {label}, {solveIterations} iteration(s)");

            // Named in the report rather than left to a console line printed only when it changes,
            // so three runs measuring three provider configurations cannot be told apart by which
            // order they were read in. A timing number without the configuration it was taken under
            // is not a measurement.
            sb.AppendLine($"  under {EDDiagnostics.DescribeProviders()}");

            // First because it happens first, and never per-iteration: it runs once for the whole
            // press, like the output stages do.
            if (fieldTotal > 0.0) Row("Field rebuild", timeFieldRebuild, 2, false);

            Row("Solve", timeIteration, 2);
            Row("Residual evaluation", timeResidualEvaluate, 4);
            Row("Build Jacobian", timeJacobianBuild, 4);

            if (termTimers != null)
            {
                foreach (var t in termTimers)
                    Row(t.name, t.timer, 6);
            }

            Row("Linear solve", timeSolve, 4);
            Row("Clearance update", timeUpdateClearance, 4);

            if (outputTotal > 0.0)
            {
                sb.AppendLine($"  {"Output".PadRight(28)}{outputTotal,10:F3} ms");
                Row("Subdivision", timeOutputSubdivide, 4, false);
                Row("Mesh deformation", timeOutputDeform, 4, false);
                Row("Simplification", timeOutputSimplify, 4, false);
            }

            sb.AppendLine($"  {"Total".PadRight(28)}{fieldTotal + solveTotal + outputTotal,10:F3} ms");

            Debug.Log(sb.ToString());
        }

        public Vector3 GetDebugNodePosition(int nodeIndex)
        {
            var node = nodes[nodeIndex];

            return (currentState.TransformOffset(nodeIndex, DVector3.zero) + node.restPosition).ToVector3();
        }

        public FullDeformationField.Frame GetDebugNodeFrame(int nodeIndex)
        {
            EDStateView state = new EDStateView(currentState);
            return GetNodeFrame(nodeIndex, state);
        }

        /// <summary>
        /// The blender the field is currently deforming through, for the tools that report where it
        /// carries a point.
        ///
        /// Handed out rather than left to the caller to construct, so a readout cannot end up on a
        /// different blend from the geometry it is drawn beside - the same reason the binding path
        /// reports through TryGetDebugBindingMatrix instead of rebinding on its own terms.
        ///
        /// Null when the field is not what deforms here, which is the caller's cue to ask the binding
        /// path instead.
        /// </summary>
        internal FullDeformationField.TransformBlender CreateDebugFieldBlender()
        {
            if (!usesDeformationField) return null;

            return deformationField.CreateBlender(GetDebugNodeFrame);
        }

        public bool TryGetDebugNodeFrame(int nodeIndex, out FullDeformationField.Frame frame)
        {
            frame = default;

            if ((nodes == null) || (currentState == null) || (nodeIndex < 0) || (nodeIndex >= nodes.Count))
            {
                return false;
            }

            frame = GetDebugNodeFrame(nodeIndex);

            return true;
        }

        public bool TryGetTerminalTargetFrame(int nodeIndex, out FullDeformationField.Frame frame, out float targetScale)
        {
            frame = default;
            targetScale = 1.0f;

            if ((terminalConstraints == null) || (nodes == null) || (nodeIndex < 0) || (nodeIndex >= nodes.Count))
            {
                return false;
            }

            for (int i = 0; i < terminalConstraints.Count; i++)
            {
                EDTerminalConstraint terminal = terminalConstraints[i];

                if (terminal.nodeIndex != nodeIndex)
                    continue;

                frame = new FullDeformationField.Frame(terminal.targetPosition.ToVector3(), terminal.targetRight.ToVector3(), terminal.targetUp.ToVector3(), terminal.targetForward.ToVector3());

                targetScale = (float)terminal.targetScale;

                return true;
            }

            return false;
        }

        private FullDeformationField.Frame GetNodeFrame(int nodeIndex, EDStateView state)
        {
            EDNode node = nodes[nodeIndex];

            Vector3 position = (node.restPosition + state.TransformOffset(nodeIndex, DVector3.zero)).ToVector3();
            Vector3 right = state.TransformVector(nodeIndex, node.restRight).ToVector3();
            Vector3 up = state.TransformVector(nodeIndex, node.restUp).ToVector3();
            Vector3 forward = state.TransformVector(nodeIndex, node.restForward).ToVector3();

            return new FullDeformationField.Frame(position, right, up, forward);
        }

        public int GetClosestDebugNodeIndex(Vector3 restPosition)
        {
            if ((nodes == null) || (nodes.Count == 0))
                return -1;

            return GetClosestNodeIndex(restPosition.ToDVector3());
        }

        public int GetClosestLeafNodeIndex(Vector3 restPosition)
        {
            if ((nodes == null) || (nodes.Count == 0))
                return -1;

            int bestIndex = -1;
            double bestDistSq = double.MaxValue;
            DVector3 p = restPosition.ToDVector3();

            for (int i = 0; i < nodes.Count; i++)
            {
                // In StructureOnly, terminal structure endpoints should have one neighbour.
                if ((nodes[i].neighbors == null) || (nodes[i].neighbors.Count != 1))
                    continue;

                double dSq = (nodes[i].restPosition - p).sqrMagnitude;

                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestIndex = i;
                }
            }

            // Fallback for degenerate/debug cases.
            if (bestIndex < 0)
                bestIndex = GetClosestNodeIndex(p);

            return bestIndex;
        }

        private struct EDResidualEnergy
        {
            public string name;
            public int rows;
            public double energy;
            public double rms;
            public double maxAbs;
        }

        private EDResidualEnergy MeasureResidualBlock(Vector<double> f, ref int row, int count, string name)
        {
            double energy = 0.0;
            double maxAbs = 0.0;

            for (int i = 0; i < count; i++)
            {
                double v = f[row + i];
                energy += v * v;
                maxAbs = Math.Max(maxAbs, Math.Abs(v));
            }

            row += count;

            return new EDResidualEnergy
            {
                name = name,
                rows = count,
                energy = energy,
                rms = (count > 0) ? Math.Sqrt(energy / count) : 0.0,
                maxAbs = maxAbs
            };
        }

        protected void LogResidualEnergies(Vector<double> f, EDEnergyModel.Instance energy, int iteration)
        {
            if (energy == null) return;

            // Reads the layout off the term list rather than a parallel list of block names that had
            // to be kept in step by hand - which is what let three energies go missing from the
            // navmesh layout unnoticed. A term that is not in the model cannot be missing from here.
            var layout = energy.DescribeLayout();

            int row = 0;

            List<EDResidualEnergy> blocks = new List<EDResidualEnergy>();

            for (int i = 0; i < layout.Count; i++)
                blocks.Add(MeasureResidualBlock(f, ref row, layout[i].rows, layout[i].name));

            double totalEnergy = 0.0;
            for (int i = 0; i < blocks.Count; i++)
                totalEnergy += blocks[i].energy;

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"[ED] Residual energies, iteration {iteration}");
            sb.AppendLine($"[ED] Normalized groups = {energy.model.normalizesWeights}");
            sb.AppendLine($"[ED] Total weighted energy = {totalEnergy:E6}, L2 = {Math.Sqrt(totalEnergy):E6}");
            sb.AppendLine("[ED] Block breakdown:");

            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];

                if (b.rows <= 0)
                    continue;

                double percent = (totalEnergy > 0.0) ? (100.0 * b.energy / totalEnergy) : 0.0;

                sb.AppendLine(
                    $"  {b.name,-16} rows={b.rows,6} " +
                    $"energy={b.energy:E6} rms={b.rms:E6} max={b.maxAbs:E6} " +
                    $"share={percent,6:F2}%");
            }

            if (row != f.Count)
            {
                sb.AppendLine($"[ED] WARNING: residual row accounting mismatch. Used={row}, f.Count={f.Count}");
            }

            Debug.Log(sb.ToString());
        }

        public FullDeformationField GetDeformationField() => deformationField;

        /// <summary>
        /// Deforms a mesh through whichever mechanism the graph implies, from srcMatrix's space into
        /// destMatrix's.
        ///
        /// restPositionUVChannel, when it is a real channel, writes each vertex's *rest* world
        /// position into that UV set. Everything the deformation is indexed by - field cells,
        /// bindings, weights - is keyed on where a vertex was, and the output mesh only records where
        /// it ended up. Without this the two cannot be related again, and a tool that samples the
        /// field at an output vertex is reading the weights of wherever that vertex was carried to.
        /// Stored as a Vector4 with w = 1 rather than a Vector3 so that a piece lying flat in a plane
        /// still keeps three components: the mesh simplifier sizes a UV channel by how many
        /// components are actually used, and an all-zero z would silently drop to a 2D channel.
        /// </summary>
        public Mesh DeformMesh(Mesh srcMesh, Matrix4x4 srcMatrix, Matrix4x4 destMatrix, bool rebuildNormals, bool rebuildTangents, int restPositionUVChannel = -1)
        {
            if (srcMesh == null)
            {
                Debug.LogError("DeformMesh failed: source mesh is null.");

                return null;
            }

            if (!srcMesh.isReadable)
            {
                Debug.LogError($"DeformMesh failed: mesh '{srcMesh.name}' is not readable. Enable Read/Write in its import settings.");

                return null;
            }

            if (currentState == null)
            {
                Debug.LogError($"DeformMesh failed for '{srcMesh.name}': there is no current deformation state.");

                return null;
            }

            Matrix4x4 destInverse = destMatrix.inverse;
            Matrix4x4 sourceNormalMatrix = srcMatrix.inverse.transpose;
            Matrix4x4 destinationNormalMatrix = destMatrix.transpose;

            Mesh outputMesh = UnityEngine.Object.Instantiate(srcMesh);

            outputMesh.name = $"{srcMesh.name} (Deformed)";

            Vector3[] sourceVertices = srcMesh.vertices;

            Vector3[] outputVertices = new Vector3[sourceVertices.Length];
            Vector3[] outputNormals = rebuildNormals ? null : srcMesh.normals;
            Vector4[] outputTangents = rebuildTangents ? null : srcMesh.tangents;

            if (restPositionUVChannel >= 8)
            {
                Debug.LogWarning($"Rest positions cannot be stored in UV channel {restPositionUVChannel}: a mesh has 8. They will not be written, and anything reading them back will fall back to the deformed positions.");

                restPositionUVChannel = -1;
            }

            List<Vector4> restPositions = (restPositionUVChannel >= 0) ? (new List<Vector4>(sourceVertices.Length)) : (null);

            bool transformNormals = (!rebuildNormals) && (outputNormals != null) && (outputNormals.Length == sourceVertices.Length);
            bool transformTangents = (!rebuildTangents) && (outputTangents != null) && (outputTangents.Length == sourceVertices.Length);

            if ((!rebuildNormals) && (!transformNormals))
            {
                Debug.LogWarning($"Mesh '{srcMesh.name}' does not contain a valid normal for every vertex. Existing normals cannot be transformed.");
            }

            if ((!rebuildTangents) && (!transformTangents))
            {
                Debug.LogWarning($"Mesh '{srcMesh.name}' does not contain a valid tangent for every vertex. Existing tangents cannot be transformed.");
            }

            // Whichever deformation the graph implies - the volumetric field for a structure graph,
            // a blend of the bound node transforms for a navmesh one. Previously this went straight
            // to the field, so output geometry could not be generated at all without one.
            EDDeformer deformer = CreateDeformer();

            if (deformer == null)
            {
                Debug.LogError($"DeformMesh failed for '{srcMesh.name}': the deformation graph has not been built.");

                return null;
            }

            float sourceToDestinationDeterminant = srcMatrix.determinant * destInverse.determinant;

            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 sourcePosition = srcMatrix.MultiplyPoint3x4(sourceVertices[i]);

                restPositions?.Add(new Vector4(sourcePosition.x, sourcePosition.y, sourcePosition.z, 1.0f));

                bool hasDeformation = deformer.TryGetDeformationMatrix(sourcePosition, out Matrix4x4 deformationMatrix);

                Vector3 deformedPosition = (hasDeformation) ? (deformationMatrix.MultiplyPoint3x4(sourcePosition)) : (sourcePosition);

                outputVertices[i] = destInverse.MultiplyPoint3x4(deformedPosition);

                if (transformNormals)
                {
                    Vector3 sourceNormal = sourceNormalMatrix.MultiplyVector(outputNormals[i]);

                    Vector3 deformedNormal = (hasDeformation) ? (deformationMatrix.TransformNormal(sourceNormal)) : (sourceNormal.normalized);

                    Vector3 destinationNormal = destinationNormalMatrix.MultiplyVector(deformedNormal);

                    outputNormals[i] = (destinationNormal.sqrMagnitude > 1e-12f) ? (destinationNormal.normalized) : (Vector3.up);
                }

                if (transformTangents)
                {
                    Vector4 sourceTangent4 = outputTangents[i];

                    Vector3 sourceTangent = sourceTangent4.xyz();

                    sourceTangent = srcMatrix.MultiplyVector(sourceTangent);

                    Vector3 deformedTangent = (hasDeformation) ? (deformationMatrix.MultiplyVector(sourceTangent)) : (sourceTangent);

                    Vector3 destinationTangent = (destInverse.MultiplyVector(deformedTangent));

                    if (destinationTangent.sqrMagnitude > 1e-12f) destinationTangent.Normalize();

                    // Tangent.w describes tangent-space handedness. Flip it if the
                    // complete transformation reverses orientation.
                    float determinant = sourceToDestinationDeterminant * ((hasDeformation) ? (deformationMatrix.determinant) : (1.0f));

                    float handedness = (determinant < 0.0f) ? (-sourceTangent4.w) : (sourceTangent4.w);

                    outputTangents[i] = destinationTangent.xyzw(handedness);
                }
            }

            outputMesh.vertices = outputVertices;

            if (restPositions != null)
            {
                outputMesh.SetUVs(restPositionUVChannel, restPositions);
            }

            if (rebuildNormals)
            {
                outputMesh.RecalculateNormals();
            }
            else if (transformNormals)
            {
                outputMesh.normals = outputNormals;
            }

            if (rebuildTangents)
            {
                if (outputMesh.normals.Length != outputMesh.vertexCount)
                {
                    Debug.LogWarning($"Mesh '{srcMesh.name}' has no valid normals. Normals will be rebuilt before rebuilding tangents.");

                    outputMesh.RecalculateNormals();
                }

                if (outputMesh.uv.Length == outputMesh.vertexCount)
                {
                    outputMesh.RecalculateTangents();
                }
                else
                {
                    // The source mesh may not use tangent-space shading.
                    // Do not retain stale tangents from the cloned mesh.
                    outputMesh.tangents = Array.Empty<Vector4>();
                }
            }
            else if (transformTangents)
            {
                // Re-orthogonalize the transformed tangent against the final normal.
                // This is especially useful when normals were rebuilt from geometry.
                Vector3[] finalNormals = outputMesh.normals;

                if ((finalNormals != null) && (finalNormals.Length == outputTangents.Length))
                {
                    for (int i = 0; i < outputTangents.Length; i++)
                    {
                        Vector3 tangent = outputTangents[i].xyz();

                        tangent = Vector3.ProjectOnPlane(tangent, finalNormals[i]);

                        if (tangent.sqrMagnitude > 1e-12f) tangent.Normalize();

                        outputTangents[i] = tangent.xyzw(outputTangents[i].w);
                    }
                }

                outputMesh.tangents = outputTangents;
            }

            outputMesh.RecalculateBounds();

            return outputMesh;
        }
    }
}
#endif
