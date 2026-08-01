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
        /// Whether SetNavEDParameters has run, which is what supplies the navmesh topology, the
        /// per-segment bindings and probes, and the slope/clearance limits. GenerateED only calls
        /// it in NavED mode, so the navigation-aware features are simply unavailable in the
        /// TranslationOnly and plain ED modes and callers must not assume otherwise.
        ///
        /// Tests the edge data rather than the reference: TopologyStatic is [Serializable], so a
        /// topology that was null when the scene was written comes back as a live object with a
        /// null edge list, and a reference test would wrongly report it as configured.
        /// </summary>
        public bool isNavConfigured => (navMeshTopology != null) && (navMeshTopology.edgeCount > 0);
        public List<NavEDSegments> structure;
        public float maxSlope = 45.0f;
        public float slopeSoftBand = 5.0f;
        public Vector3 upVector
        {
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
        [SerializeField, HideInInspector]
        private FullDeformationField    deformationField;
        [SerializeField, HideInInspector]
        private List<Mesh>  sourceGeometry;


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
        DebugProfiler timeDeformationFieldGeneration;
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

        public void BuildDeformationGraph(DeformationGraphSource deformationGraphSource,
                                          TopologyStatic topology, float minDistance, List<int> forcedVertices, bool forceStructureNodes,
                                          BindingSelectionMode bindMode, BindingWeightMode weightMode, GraphLinkMode graphLinkMode,
                                          IEDStructureSource structureSource, float structureMaxSegmentLength = 0.0f, Vector3 structureFallbackUp = default, TryGetSurfaceNormal tryGetSurfaceNormal = null,
                                          int k = 4, // When BindingSelectionMode = closest-K
                                          float maxBindDistance = 2.0f, // When GraphLinKMode = DirectionAware
                                          float minBindAngle = 20.0f, // When GraphLinKMode = DirectionAware
                                          HasLOS hasLOSFunction = null, // When GraphLinKMode = DirectionAware
                                          float power = 2.0f,
                                          float sigma = 1.0f, // When BindingSelectionMode = closest-K and BindingWeightMode = InversePower
                                          float deformationFieldVoxelSize = 0.05f,  // Voxel resolution: default = 5% of maximum size of encapsulating objects
                                          int deformationFieldMaxWeights = 4) // How many weights influence the deformation at each cell
        {
            if (topology == null)
            {
                Debug.LogError("BuildDeformationGraph failed: topology is null.");
                return;
            }

            if (structureFallbackUp.sqrMagnitude > 1e-8f)
                this.upVector = structureFallbackUp;
            else
                this.upVector = Vector3.up;

            this.deformationGraphSource = deformationGraphSource;

            // Discard any field from a previous build; only the structure branch produces one.
            // Note this is not sufficient on its own - see usesDeformationField.
            deformationField = null;

            switch (deformationGraphSource)
            {
                case DeformationGraphSource.NavMeshAndStructure:
                    BuildDeformationGraphFromNavMesh(topology, minDistance, forcedVertices, forceStructureNodes, bindMode, weightMode, graphLinkMode, structureSource, structureMaxSegmentLength, structureFallbackUp, tryGetSurfaceNormal, k, maxBindDistance, minBindAngle, hasLOSFunction, power, sigma);
                    break;
                case DeformationGraphSource.StructureOnly:
                    BuildDeformationGraphFromStructure(topology,
                                                       bindMode, weightMode,
                                                       structureSource, structureMaxSegmentLength, structureFallbackUp, tryGetSurfaceNormal,
                                                       k, power,sigma);
                    BuildNodeRestFrames();
                    BuildLinkAngleConstraints();
                    BuildDeformationField(deformationFieldVoxelSize, deformationFieldMaxWeights);
                    break;
                default:
                    break;
            }
        }

        public void BuildDeformationGraphFromNavMesh(TopologyStatic topology, float minDistance, List<int> forcedVertices, bool forceStructureNodes,
                                                     BindingSelectionMode bindMode, BindingWeightMode weightMode, GraphLinkMode graphLinkMode,
                                                     IEDStructureSource structureSource, float structureMaxSegmentLength = 0.0f, Vector3 structureFallbackUp = default, TryGetSurfaceNormal tryGetSurfaceNormal = null,
                                                     int k = 4, float maxBindDistance = 2.0f, float minBindAngle = 20.0f,
                                                     HasLOS hasLOSFunction = null,
                                                     float power = 2.0f, float sigma = 1.0f)
        {
            BuildStructure(structureSource, structureMaxSegmentLength, structureFallbackUp, tryGetSurfaceNormal);

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
            clearanceOpenings.Clear();
            terminalConstraints.Clear();
            linkAngleConstraints.Clear();

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

        public void BuildDeformationGraphFromStructure(TopologyStatic topology,
                                               BindingSelectionMode bindMode,
                                               BindingWeightMode weightMode,
                                               IEDStructureSource structureSource,
                                               float structureMaxSegmentLength = 0.0f,
                                               Vector3 structureFallbackUp = default,
                                               TryGetSurfaceNormal tryGetSurfaceNormal = null,
                                               int k = 4,
                                               float power = 2.0f,
                                               float sigma = 1.0f)
        {
            BuildStructure(structureSource, structureMaxSegmentLength, structureFallbackUp, tryGetSurfaceNormal);

            if (topology == null)
            {
                Debug.LogError("BuildDeformationGraphFromStructure failed: topology is null.");
                return;
            }

            if ((structure == null) || (structure.Count == 0))
            {
                Debug.LogError("BuildDeformationGraphFromStructure failed: structure is null or empty.");
                return;
            }

            // -----------------------------------------------------------------
            // 1) Copy source navmesh into ED rest data.
            //    Even in StructureOnly mode, the mesh still needs to deform.
            // -----------------------------------------------------------------
            var v = topology.GetVertexPositions();

            restVertices = new DVector3[v.Count];
            for (int i = 0; i < v.Count; i++)
                restVertices[i] = v[i].ToDVector3();

            triangles = topology.GetTriangleIndices().ToArray();

            nodes.Clear();
            bindings = null;
            handleConstraints.Clear();
            clearanceOpenings.Clear();
            terminalConstraints.Clear();
            linkAngleConstraints.Clear();

            // -----------------------------------------------------------------
            // 2) Build ED graph directly from structure segment endpoints.
            // -----------------------------------------------------------------
            const float structureNodeMergeDistanceSq = 1e-8f;

            for (int i = 0; i < structure.Count; i++)
            {
                var seg = structure[i];

                int idx1 = TryAddSampleVertex(seg.p1, structureNodeMergeDistanceSq);
                int idx2 = TryAddSampleVertex(seg.p2, structureNodeMergeDistanceSq);

                seg.node1 = idx1;
                seg.node2 = idx2;

                AddUndirectedNeighbor(idx1, idx2);
            }

            if (nodes.Count == 0)
            {
                Debug.LogError("BuildDeformationGraphFromStructure failed: no nodes were created.");
                return;
            }

            // -----------------------------------------------------------------
            // 3) Bind navmesh vertices to the structure graph.
            // -----------------------------------------------------------------
            BuildBindings(topology, bindMode, weightMode, k, power, sigma);

            currentState = new EDState(nodes.Count);
            restState = new EDState(nodes.Count);

            Debug.Log($"ED structure-only graph built. " +
                      $"Vertices={topology.vertexCount}, " +
                      $"Triangles={topology.triangleCount}, " +
                      $"StructureSegments={structure.Count}, " +
                      $"Nodes={nodes.Count}, " +
                      $"Edges={deformGraphEdgeCount}");
        }

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

        private void BuildNodeRestFrames()
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

            // Remembered so that points which are not navmesh vertices - the source geometry passed
            // to DeformMesh, for instance - can be bound the same way later on.
            RememberBindingSettings(bindMode, weightMode, k, power, sigma);

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

        private void BuildLinkAngleConstraints()
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
                for (int vId = 0; vId < restVertices.Length; vId++)
                    deformed[vId] = deformationField.DeformPositionFromNodeFramesTrilinear(restVertices[vId].ToVector3(), GetDebugNodeFrame);

                return deformed;
            }

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

        private Vector3 GetSafeStructureUp(Vector3 fallbackUp)
        {
            if (fallbackUp.sqrMagnitude > 1e-8f)
                return fallbackUp.normalized;

            if (_upVector.sqrMagnitude > 1e-8f)
                return _upVector.normalized;

            return Vector3.up;
        }

        private Vector3 GetStructureSegmentNormal(Vector3 p1,
                                                  Vector3 p2,
                                                  int fallbackSegmentIndex,
                                                  Vector3 fallbackUp,
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
            // attached until SetNavEDParameters runs much later. GetSegmentSlopeNormal therefore
            // returns zero here and we fall through to the up vector. Kept because the branch is
            // meaningful if this is ever called on an already-bound structure.
            if ((structure != null) &&
                (fallbackSegmentIndex >= 0) &&
                (fallbackSegmentIndex < structure.Count) &&
                (currentState != null))
            {
                normal = GetSegmentSlopeNormal(fallbackSegmentIndex);

                if (normal.sqrMagnitude > 1e-8f)
                    return normal.normalized;
            }

            return GetSafeStructureUp(fallbackUp);
        }

        public void BuildStructure(IEDStructureSource structureSource, float structureMaxSegmentLength, Vector3 fallbackUp, TryGetSurfaceNormal tryGetSurfaceNormal)
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
                        Vector3 normal = GetStructureSegmentNormal(p1, p2, i, fallbackUp, tryGetSurfaceNormal);

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

                            Vector3 normal = GetStructureSegmentNormal(sp1, sp2, i, fallbackUp, tryGetSurfaceNormal);

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

        /// <summary>
        /// Supplies the navigation data the nav-aware energies measure against.
        ///
        /// The limits those energies enforce no longer come through here - each term pushes its own
        /// in ApplyRuntimeParameters, so a limit travels with the energy that reads it. What remains
        /// is data rather than configuration: the topology, the agent radius, the up vector and the
        /// per-segment bindings, none of which belongs to any single energy.
        /// </summary>
        public void SetNavEDParameters(TopologyStatic navMeshTopology,
                                       float agentRadius, Vector3 upVector,
                                       BindingSelectionMode bindMode, BindingWeightMode weightMode,
                                       int k = 4, // When BindingSelectionMode = closest-K
                                       float power = 2.0f,
                                       float sigma = 1.0f)
        {
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

                float probeDistance = agentRadius * 0.5f;
                seg.probeT = seg.center + probeDistance * t;
                seg.probeB = seg.center + probeDistance * b;

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

        /// <summary>
        /// Whether the volumetric field is what deforms things for this graph. Only the structure
        /// builder produces one.
        ///
        /// The graph source is tested as well as the reference, and the graph source is the part
        /// that matters: FullDeformationField is [Serializable] and lives on a [SerializeField]
        /// member, so a field that was null when Unity serialized the component comes back as a
        /// live but empty object. Clearing it on rebuild is not enough, because serialization
        /// happens again afterwards. A null check alone silently routes navmesh graphs through an
        /// empty field.
        /// </summary>
        private bool usesDeformationField => (deformationGraphSource == DeformationGraphSource.StructureOnly) && (deformationField != null);

        internal bool UseDeformationFieldForClearance => usesDeformationField;

        internal List<FullDeformationField.Frame> BuildNodeFrames(EDStateView state)
        {
            var frames = new List<FullDeformationField.Frame>(nodes.Count);

            for (int i = 0; i < nodes.Count; i++)
            {
                frames.Add(GetNodeFrame(i, state));
            }

            return frames;
        }

        private DVector3 DeformClearancePoint(DVector3 restPosition, EDVertexBinding standardBinding, EDStateView state, List<FullDeformationField.Frame> nodeFrames)
        {
            if (nodeFrames != null)
            {
                Vector3 deformed = deformationField.DeformPositionFromNodeFramesTrilinear(restPosition.ToVector3(), nodeFrames);

                return deformed.ToDVector3();
            }

            return DeformVertex(restPosition, standardBinding, state);
        }

        private bool TryComputeSegmentClearance(EDStateView state, int segmentIndex, List<FullDeformationField.Frame> nodeFrames, out double clearance)
        {
            // Single gate for "can this segment's clearance be measured at all". GetClearance
            // walks navMeshTopology.edges, and that topology only exists once SetNavEDParameters
            // has run. Both callers - the cache builder and the clearance residual - already treat
            // false as "no clearance available", so this needs no special handling downstream.
            if (!isNavConfigured)
            {
                clearance = double.MaxValue;
                return false;
            }

            (Vector3 p1, Vector3 p2) = GetTransformedSegment(state, segmentIndex, nodeFrames);

            return GetClearance(state, p1.ToDVector3(), p2.ToDVector3(),nodeFrames, out clearance);
        }

        EDClearanceCache ComputeClearance(EDState state)
        {
            return state.clearances = ComputeClearance(new EDStateView(state));
        }

        private sealed class ClearanceThreadScratch
        {
            public readonly List<FullDeformationField.Frame> nodeFrames;

            public ClearanceThreadScratch(int capacity) : this(capacity, null)
            {
            }

            public ClearanceThreadScratch(int capacity, List<FullDeformationField.Frame> baseNodeFrames)
            {
                nodeFrames = (baseNodeFrames != null) ? (new List<FullDeformationField.Frame>(baseNodeFrames)) : (null);
            }
        }

        EDClearanceCache ComputeClearance(EDStateView state)
        {
            DebugProfiler.DebugMark(timeUpdateClearance);

            var ret = new EDClearanceCache((structure != null) ? (structure.Count) : (0));

            // Clearance is a navigation-aware measurement, and everything it needs - the navmesh
            // topology to measure against and the per-segment bindings to deform through - is
            // supplied by SetNavEDParameters, which only NavED mode calls. In the other modes
            // there is nothing to measure, so every segment reports the existing "no clearance"
            // marker instead of doing the work and dereferencing a null topology.
            if (!isNavConfigured)
            {
                for (int i = 0; i < ret.count; i++)
                    ret.Set(i, double.MaxValue);

                DebugProfiler.DebugMark(timeUpdateClearance);

                return ret;
            }

            if (UseDeformationFieldForClearance)
            {
                // Read-only during the parallel loop.
                List<FullDeformationField.Frame> nodeFrames = BuildNodeFrames(state);

                int scratchCapacity = Mathf.Min(nodes.Count, 8 * deformationField.maxInfluencesPerCell);

                Parallel.For(
                    0,
                    structure.Count,
                    EDDiagnostics.parallelOptions,
                    // One scratch object per worker, not per segment.
                    () => new ClearanceThreadScratch(scratchCapacity),

                    (index, loopState, scratch) =>
                    {
                        bool valid = TryComputeSegmentClearance(state, index, nodeFrames, out double clearance);

                        ret.Set(index, (valid) ? (clearance) : (double.MaxValue));

                        return scratch;
                    },

                    scratch =>
                    {
                        // Nothing to merge or dispose.
                    }
                );
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

        private double EvaluateSingleClearanceResidual(EDStateView state, int segmentIndex, double wClearance, List<FullDeformationField.Frame> nodeFrames = null)
        {
            double original = restState.GetClearance(segmentIndex);

            // Fallback for serial callers. The optimized Jacobian path supplies
            // nodeFrames explicitly, so it does not reach this allocation.
            if ((UseDeformationFieldForClearance) && (nodeFrames == null))
            {
                nodeFrames = BuildNodeFrames(state);
            }

            if (!TryComputeSegmentClearance(state, segmentIndex, nodeFrames, out double current))
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

        internal double EvaluateSingleSegmentLengthResidual(EDStateView state, int segmentIndex, double wSegmentLength)
        {
            var seg = structure[segmentIndex];

            DVector3 p1 = DeformVertex(seg.p1, seg.bind1, state);
            DVector3 p2 = DeformVertex(seg.p2, seg.bind2, state);

            double originalLength = (seg.p2 - seg.p1).magnitude;
            if (originalLength < 1e-8)
                return 0.0;

            double currentLength = (p2 - p1).magnitude;

            double mslr = Math.Clamp(segmentMinRatio, 0.0, 1.0);
            double minAllowedLength = mslr * originalLength;

            double loss = Math.Max(0.0, minAllowedLength - currentLength) / originalLength;
            return wSegmentLength * loss;
        }

        internal double EvaluateSingleSegmentLengthResidualStructure(EDStateView state, int segmentIndex, double wSegmentLength)
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

        bool GetClearance(EDStateView state, DVector3 p1, DVector3 p2, List<FullDeformationField.Frame> nodeFrames, out double minClearance)
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

                DVector3 e1 = DeformClearancePoint(restVertices[edge.vertices.i1], bindings[edge.vertices.i1], state, nodeFrames);
                DVector3 e2 = DeformClearancePoint(restVertices[edge.vertices.i2], bindings[edge.vertices.i2], state, nodeFrames);

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

            List<FullDeformationField.Frame> nodeFrames = (UseDeformationFieldForClearance) ? (BuildNodeFrames(state)) : (null);

            // GetSegment is currently called serially by the debug drawing, so null
            // scratch buffers may safely use FullDeformationField's shared cache.
            return GetTransformedSegment(state, segmentIndex, nodeFrames);
        }

        private (Vector3, Vector3) GetTransformedSegment(EDStateView state, int segmentIndex, List<FullDeformationField.Frame> nodeFrames)
        {
            NavEDSegments segment = structure[segmentIndex];

            // Without SetNavEDParameters the endpoint bindings are null, so there is nothing to
            // deform the segment through and it is still at its rest position. The deformation
            // field path ignores the bindings entirely, so it is left alone.
            if ((nodeFrames == null) && (!IsSegmentBound(segment)))
                return (segment.p1.ToVector3(), segment.p2.ToVector3());

            DVector3 p1 = DeformClearancePoint(segment.p1, segment.bind1, state, nodeFrames);

            DVector3 p2 = DeformClearancePoint(segment.p2, segment.bind2, state, nodeFrames);

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
        /// True once SetNavEDParameters has attached the endpoint, centre and probe bindings a
        /// segment needs before anything can be evaluated on it. All five are attached together,
        /// so this is all-or-nothing per structure.
        ///
        /// BuildStructure produces segments with none of them, and only NavED mode ever
        /// runs SetNavEDParameters, so unbound segments are the normal case in the other modes.
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

            // The probe bindings are only built by SetNavEDParameters, so they are still null
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

        public FullDeformationField.Frame GetDebugNodeFrame(int nodeIndex)
        {
            EDStateView state = new EDStateView(currentState);
            return GetNodeFrame(nodeIndex, state);
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

        void BuildDeformationField(float density = 0.05f, int maxWeights = 4)
        {
            timeDeformationFieldGeneration = new();

            timeDeformationFieldGeneration.Mark();

            if ((sourceGeometry == null) || (sourceGeometry.Count == 0))
            {
                Debug.LogWarning("BuildDeformationField failed: no source geometry was provided.");
                deformationField = null;

                timeDeformationFieldGeneration.Mark();
                return;
            }

            if ((nodes == null) || (nodes.Count == 0))
            {
                Debug.LogWarning("BuildDeformationField failed: no deformation graph nodes exist.");
                deformationField = null;

                timeDeformationFieldGeneration.Mark();
                return;
            }

            // -------------------------------------------------------------
            // 1) Compute bounds from baked source geometry.
            //    Important: Bounds is a struct, so do not use bounds.Value.Encapsulate().
            // -------------------------------------------------------------
            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < sourceGeometry.Count; i++)
            {
                Mesh mesh = sourceGeometry[i];
                if (mesh == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = mesh.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(mesh.bounds);
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning("BuildDeformationField failed: source geometry has no valid meshes.");
                deformationField = null;

                timeDeformationFieldGeneration.Mark();
                return;
            }

            // Make sure deformation graph nodes are inside the field bounds.
            for (int i = 0; i < nodes.Count; i++)
            {
                bounds.Encapsulate(nodes[i].restPosition.ToVector3());
            }

            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

            if (maxSize <= 1e-6f)
            {
                Debug.LogWarning("BuildDeformationField failed: source geometry bounds are degenerate.");
                deformationField = null;

                timeDeformationFieldGeneration.Mark();
                return;
            }

            float safeDensity = Mathf.Max(density, 1e-5f);
            float voxelSize = maxSize * safeDensity;

            int safeMaxWeights = Mathf.Clamp(maxWeights, 1, nodes.Count);

            // -------------------------------------------------------------
            // 2) Create deformation field.
            // -------------------------------------------------------------
            deformationField = new FullDeformationField(voxelSize, safeMaxWeights);

            // -------------------------------------------------------------
            // 3) Fill the field using source geometry.
            //
            //    sourceGeometry is already baked to world space in SetSourceGeometry(),
            //    so the voxelizer should receive identity matrices here.
            // -------------------------------------------------------------
            List<Matrix4x4> identityMatrices = new();

            for (int i = 0; i < sourceGeometry.Count; i++)
            {
                identityMatrices.Add(Matrix4x4.identity);
            }

            deformationField.FillWithMesh(sourceGeometry, identityMatrices);

            // -------------------------------------------------------------
            // 4) Add deformation graph nodes as volumetric/geodesic seeds.
            //
            //    Add them in ED node order so the deformation field node id
            //    matches the ED node index.
            // -------------------------------------------------------------
            for (int i = 0; i < nodes.Count; i++)
            {
                EDNode node = nodes[i];

                deformationField.AddDeformationNode(node.restPosition.ToVector3(), node.restRight.ToVector3(), node.restUp.ToVector3(), node.restForward.ToVector3());
            }

            // -------------------------------------------------------------
            // 5) Extend the influence field outside occupied cells.
            //
            //    The occupied volume gets geodesic distances from AddDeformationNode().
            //    GrowInfluence() lets nearby empty cells also query valid weights.
            // -------------------------------------------------------------
            deformationField.GrowInfluence();

            // -------------------------------------------------------------
            // 6) Convert distances into normalized weights.
            // -------------------------------------------------------------
            deformationField.ComputeWeights(safeMaxWeights);
            deformationField.BuildTrilinearRegions();

            timeDeformationFieldGeneration.Mark();

            Debug.Log(
                $"Deformation field built:\n" +
                $"  Meshes={sourceGeometry.Count}\n " +
                $"  Nodes={nodes.Count}\n" +
                $"  VoxelSize={voxelSize:F4}\n" +
                $"  MaxWeights={safeMaxWeights}\n" +
                $"  Bounds={bounds.size}\n" +
                $"  Grid Size={deformationField.gridSize}\n" +
                $"  Time={timeDeformationFieldGeneration.accumulatedTimeMS:F6} ms"
            );
        }

        public FullDeformationField GetDeformationField() => deformationField;

        public Mesh DeformMesh(Mesh srcMesh, Matrix4x4 srcMatrix, Matrix4x4 destMatrix, bool rebuildNormals, bool rebuildTangents)
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
