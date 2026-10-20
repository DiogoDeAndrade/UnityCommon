using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The construction shared by every builder that *samples* its graph off a mesh: pick vertices
    /// a minimum distance apart as nodes, bind the rest of the mesh to them, then link the nodes.
    ///
    /// It is one algorithm with two callers because it was never navmesh-specific - only the mesh
    /// changes, which is what topologySource says. The navmesh builder adds skeleton seeding and
    /// the two navigation-flavoured linking strategies; the geometry builder takes neither. Both
    /// differences are expressed as parameters this base already reads off the definition, so
    /// neither subclass overrides any of it.
    ///
    /// Adds no serialized fields of its own, so the existing builder assets are unaffected by
    /// sitting under it.
    /// </summary>
    public abstract class EDGraphBuilderSampled : EDGraphBuilder
    {
        public abstract class SampledInstance : Instance
        {
            protected SampledInstance(EDGraphBuilderSampled builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
                : base(builder, deformation, structureSource, nav)
            {
            }

            public override void Build(List<int> forcedVertices)
            {
                TopologyStatic topology = ResolveTopology();
                if (topology == null) return;

                deformation.BeginGraphBuild(DeformationGraphSource.NavMeshAndStructure);

                // The skeleton is still built even when it does not define the graph: it seeds
                // nodes when asked to, and it is what the navigation energies measure along.
                using (Stage("Skeleton"))
                    deformation.BuildStructure(structureSource, builder.maxSegmentLength, nav.tryGetSurfaceNormal);

                float minDistance = builder.sampleMinDistance;

                if (minDistance <= 0.0f)
                {
                    Debug.LogWarning("Sampled graph build: minDistance <= 0, clamping to a small value.");
                    minDistance = 0.001f;
                }

                using (Stage("Rest geometry"))
                    deformation.SetRestGeometry(topology);

                bool sampled;

                using (Stage("Nodes"))
                    sampled = SampleNodes(topology, forcedVertices, minDistance);

                if (!sampled) return;

                // Sigma is resolved against the configured spacing rather than the clamped one, so
                // that a degenerate spacing does not silently rescale the falloff as well.
                using (Stage("Bindings"))
                    deformation.SetGraphBindings(topology, builder.binding, builder.sampleMinDistance);

                using (Stage("Links"))
                    LinkNodes(topology);

                deformation.EndGraphBuild();

                Debug.Log($"ED graph built. Vertices={topology.vertexCount}, Triangles={topology.triangleCount}, Nodes={deformation.nodes.Count}, Edges={deformation.graphEdgeCount}");

                // After the links, which read ED's own bindings: the graph is ED's whatever then
                // carries the geometry, and rebinding the vertices to the field comes last.
                if (builder.buildsDeformationField)
                {
                    using (Stage("Deformation field"))
                        BuildDeformationField();
                }
            }

            /// <summary>
            /// The field again from the current settings, carrier included, without the rest of
            /// Build: after a reload, which discards the field, or after a field setting changed.
            /// </summary>
            public override void RebuildDeformationField()
            {
                if (!builder.buildsDeformationField) return;

                BuildDeformationField();
            }

            /// <summary>
            /// The deformation field over the sampled graph: the geometry voxelized, every node a
            /// point source at its rest position, the geodesic distances grown through the empty
            /// cells and turned into weights. The structure builder's construction without what
            /// only a skeleton has - no seed bars, no skeleton links, no along-skeleton resolution -
            /// and kept as its own sequence rather than shared with it, so the structure path the
            /// goldens run through stays the statements it was; the calls into the field are the
            /// same calls in the same order.
            ///
            /// The bindings stay ED's under either carrier: what carries a point is decided by the
            /// deformation (usesDeformationField), and every reader - the output, the quality terms,
            /// the vertex constraint term - asks it, so nothing here re-encodes it.
            /// </summary>
            private void BuildDeformationField()
            {
                bool carrier = (builder.geometryCarrier == EDGeometryCarrier.DeformationField);

                // Before anything can fail, so a field that does not build reads as missing rather
                // than as never asked for.
                deformation.SetSampledGraphFieldCarrier(carrier);

                DebugProfiler timer = new();
                timer.Mark();

                List<Mesh> sourceGeometry = deformation.GetSourceGeometry();

                if ((sourceGeometry == null) || (sourceGeometry.Count == 0))
                {
                    Debug.LogWarning($"{builder.name}: the deformation field was asked for and no source geometry was provided, so there is none.");
                    deformation.SetDeformationField(null);
                    return;
                }

                var nodes = deformation.nodes;

                if ((nodes == null) || (nodes.Count == 0))
                {
                    Debug.LogWarning($"{builder.name}: the deformation field was asked for and the graph has no nodes.");
                    deformation.SetDeformationField(null);
                    return;
                }

                // The geometry's bounds and the nodes inside them. Bounds is a struct, so the
                // copy is encapsulated in place.
                bool hasBounds = false;
                Bounds bounds = default;

                for (int i = 0; i < sourceGeometry.Count; i++)
                {
                    Mesh mesh = sourceGeometry[i];
                    if (mesh == null) continue;

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
                    Debug.LogWarning($"{builder.name}: the source geometry has no valid meshes, so there is no deformation field.");
                    deformation.SetDeformationField(null);
                    return;
                }

                for (int i = 0; i < nodes.Count; i++)
                    bounds.Encapsulate(nodes[i].restPosition.ToVector3());

                float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

                if (maxSize <= 1e-6f)
                {
                    Debug.LogWarning($"{builder.name}: the source geometry's bounds are degenerate, so there is no deformation field.");
                    deformation.SetDeformationField(null);
                    return;
                }

                float safeDensity = Mathf.Max(builder.deformationFieldVoxelDensity, 1e-5f);
                float voxelSize = maxSize * safeDensity;

                // The structure builder checks its shortest segment; here the nodes have no
                // segments, and the question is the same one asked of their spacing: two nodes in
                // one cell have the same distance everywhere and no weighting can tell them apart.
                double minSpacing = ShortestNodeSpacing(nodes);

                if (minSpacing > 0.0)
                {
                    if (minSpacing < voxelSize)
                        Debug.LogError($"{builder.name}: the closest two nodes are {minSpacing:F4} apart against a voxel of {voxelSize:F4}, so they share a cell and the field cannot tell them apart. Raise the sample distance, or lower the field voxel density (voxel = density x largest bound).");
                    else if (minSpacing < 2.0 * voxelSize)
                        Debug.LogWarning($"{builder.name}: the closest two nodes are {minSpacing:F4} apart against a voxel of {voxelSize:F4} ({minSpacing / voxelSize:F2} voxels), so their distances quantize to a step or two and the weights separate them poorly. Raise the sample distance, or lower the field voxel density.");
                }

                EDGraphBuilder.ResolveFieldSlots(builder.deformationFieldMaxWeights, builder.deformationFieldDistanceStorage, builder.deformationFieldStorageSlots, nodes.Count,
                                                 out int safeMaxWeights, out int storageSlots);

                bool slotPerNode = (storageSlots >= nodes.Count);

                // Point sources, so neither seeding flag; the volume influence, the only one a graph
                // without a skeleton has.
                FullDeformationField field = new FullDeformationField(voxelSize, safeDensity, safeMaxWeights, storageSlots, slotPerNode, builder.deformationFieldConnectivity,
                                                                      false, false, builder.deformationFieldBlendMode,
                                                                      builder.deformationFieldRotationBlend, builder.deformationFieldScaleBlend);

                // Baked to world space by SetSourceGeometry, so the voxelizer takes identities.
                List<Matrix4x4> identityMatrices = new();

                for (int i = 0; i < sourceGeometry.Count; i++)
                    identityMatrices.Add(Matrix4x4.identity);

                using (Stage("Voxelisation"))
                    field.FillWithMesh(sourceGeometry, identityMatrices);

                // In node order, so the field's node index is the graph's.
                using (Stage("Node wavefronts"))
                {
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        EDNode node = nodes[i];

                        field.AddDeformationNode(node.restPosition.ToVector3(), node.restRight.ToVector3(), node.restUp.ToVector3(), node.restForward.ToVector3(), 0.0f);
                    }
                }

                using (Stage("Influence growth"))
                    field.GrowInfluence(EDDiagnostics.parallelOptions);

                using (Stage("Weights"))
                    field.ComputeWeights(safeMaxWeights, builder.CreateFieldWeightResolver());

                using (Stage("Trilinear regions"))
                    field.BuildTrilinearRegions();

                deformation.SetDeformationField(field);

                string carrierLine = (carrier)
                    ? ($"  Carrier=DeformationField, blend {field.builtBlendDescriptor}: the output, the pins and the quality terms' samples all through the field\n")
                    : ("  Carrier=Bindings: the output, the pins and the field quality terms' cell corners all through ED's bindings, the field giving only its cells\n");

                timer.Mark();

                Debug.Log(
                    $"Deformation field built over the sampled graph:\n" +
                    $"  Meshes={sourceGeometry.Count}\n" +
                    $"  Nodes={nodes.Count}\n" +
                    $"  VoxelSize={voxelSize:F4}\n" +
                    ((minSpacing > 0.0) ? ($"  MinNodeSpacing={minSpacing:F4} ({minSpacing / voxelSize:F2} voxels)\n") : ("")) +
                    $"  MaxWeights={safeMaxWeights}\n" +
                    $"  Bounds={bounds.size}\n" +
                    $"  Grid Size={field.gridSize}\n" +
                    $"  Growth={field.lastGrowthPath}, {field.lastGrowthOccupiedCells} occupied cells, {field.lastGrowthBoundaryCells} of them seeds\n" +
                    carrierLine +
                    $"  Time={timer.accumulatedTimeMS:F6} ms"
                );
            }

            /// <summary>The smallest rest distance between two nodes, or -1 with fewer than two.</summary>
            private static double ShortestNodeSpacing(List<EDNode> nodes)
            {
                if ((nodes == null) || (nodes.Count < 2)) return -1.0;

                double minSq = double.MaxValue;

                for (int i = 0; i < nodes.Count; i++)
                {
                    DVector3 p = nodes[i].restPosition;

                    for (int j = i + 1; j < nodes.Count; j++)
                        minSq = Math.Min(minSq, (nodes[j].restPosition - p).sqrMagnitude);
                }

                return Math.Sqrt(minSq);
            }

            /// <summary>
            /// Forced vertices first, then optionally the skeleton endpoints, then a radius-pruned
            /// fill over what is left. False when nothing was sampled from a mesh that had vertices
            /// to sample, which is a failed build rather than an empty one.
            /// </summary>
            private bool SampleNodes(TopologyStatic topology, List<int> forcedVertices, float minDistance)
            {
                float minDistanceSq = minDistance * minDistance;

                HashSet<int> forcedSet = (forcedVertices != null) ? (new HashSet<int>(forcedVertices)) : (new HashSet<int>());

                // Forced vertices first - min distance is set to 0.0f so that they're always added regardless of distance to each other
                // There are no duplicates for sure, so this code could probabably be optimized a bit, but it's not a big deal since the number of forced vertices is expected to be low.
                foreach (int vId in forcedSet)
                {
                    if ((vId < 0) || (vId >= topology.vertexCount))
                        continue;

                    deformation.AddGraphNode(vId, topology, 0.0f);
                }

                // Add structure nodes
                var structure = deformation.structure;

                if ((structure != null) && (builder.forceStructureNodes))
                    for (int i = 0; i < structure.Count; i++)
                    {
                        var seg = structure[i];
                        deformation.AddGraphNode(seg.p1, minDistanceSq);
                        deformation.AddGraphNode(seg.p2, minDistanceSq);
                    }

                // Fill remaining graph with radius-pruned vertex samples
                for (int vId = 0; vId < topology.vertexCount; vId++)
                {
                    if (forcedSet.Contains(vId))
                        continue;

                    deformation.AddGraphNode(vId, topology, minDistanceSq);
                }

                // Fallback safety
                if ((deformation.nodes.Count == 0) && (topology.vertexCount > 0))
                {
                    Debug.LogError("Failed to generate ED deformation graph: no nodes were sampled.");
                    return false;
                }

                return true;
            }

            private void LinkNodes(TopologyStatic topology)
            {
                switch (builder.linkMode)
                {
                    case GraphLinkMode.PartitionAdjacency:
                        LinkByPartitionAdjacency(topology);
                        break;

                    case GraphLinkMode.SharedBindings:
                        LinkBySharedBindings();
                        break;

                    case GraphLinkMode.DirectionAware:
                        LinkDirectionAware();
                        break;
                }
            }

            /// <summary>
            /// Two nodes are linked when the mesh has an edge crossing from one's partition into
            /// the other's - so the graph inherits the connectivity of the surface rather than of
            /// the ambient space.
            /// </summary>
            private void LinkByPartitionAdjacency(TopologyStatic topology)
            {
                var bindings = deformation.bindings;

                if ((bindings == null) || (bindings.Length == 0))
                {
                    Debug.LogWarning("LinkByPartitionAdjacency: no bindings available.");
                    return;
                }

                deformation.ClearGraphLinks();

                for (int edgeId = 0; edgeId < topology.edgeCount; edgeId++)
                {
                    var edge = topology.GetEdgeVertex(edgeId);

                    int n0 = bindings[edge.i1].nodeIndices[0];
                    int n1 = bindings[edge.i2].nodeIndices[0];

                    if (n0 != n1)
                        deformation.LinkGraphNodes(n0, n1);
                }

                EnsureNoIsolatedNodes();
            }

            /// <summary>
            /// Every pair of nodes influencing the same vertex is linked.
            /// </summary>
            private void LinkBySharedBindings()
            {
                var bindings = deformation.bindings;

                if ((bindings == null) || (bindings.Length == 0))
                {
                    Debug.LogWarning("LinkBySharedBindings: no bindings available.");
                    return;
                }

                deformation.ClearGraphLinks();

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

                            deformation.LinkGraphNodes(a, b);
                        }
                    }
                }
            }

            private struct DirectionAwareCandidate
            {
                public int nodeIndex;
                public double distanceSq;
                public Vector3 direction;
            }

            /// <summary>
            /// Closest neighbour per direction bucket, within a radius and optionally requiring line
            /// of sight - so a node links across a room rather than around every corner of it.
            /// </summary>
            private void LinkDirectionAware()
            {
                float maxBindDistance = builder.maxBindDistance;
                float minBindAngle = builder.minBindAngle;
                HasLOS hasLOSFunction = nav.hasLOS;

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

                var nodes = deformation.nodes;

                if (nodes == null || nodes.Count == 0)
                    return;

                if (maxBindDistance <= 0.0)
                {
                    Debug.LogWarning("LinkDirectionAware: maxBindDistance must be > 0.");
                    return;
                }

                deformation.ClearGraphLinks();

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

                        deformation.LinkGraphNodes(i, cand.nodeIndex);
                        chosenDirections.Add(cand.direction);
                    }
                }
            }

            /// <summary>
            /// Attaches any node the linking left alone to its nearest neighbour. A node with no
            /// edges carries no smoothness or regularization rows, so it would be free to drift.
            /// </summary>
            private void EnsureNoIsolatedNodes()
            {
                var nodes = deformation.nodes;

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
                        deformation.LinkGraphNodes(i, bestJ);
                }
            }
        }
    }
}
#endif
