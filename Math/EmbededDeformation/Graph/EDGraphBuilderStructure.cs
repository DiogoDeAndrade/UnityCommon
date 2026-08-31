using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Takes the deformation graph straight from the skeleton: every structure segment endpoint
    /// becomes a node and every segment becomes an edge.
    ///
    /// Neither sampling nor a linking strategy applies here, which is why this builder exposes
    /// neither - the skeleton already answers both questions. What it adds instead is a volumetric
    /// deformation field, which is how geometry is deformed in this mode.
    ///
    /// It also exposes no binding settings. Bindings are still built, because the node transforms
    /// have to reach the navmesh vertices somehow, but nothing that produces a result reads them in
    /// this mode: geometry goes through the field, clearance measures through the field, and the
    /// slope, orientation and segment-length terms all work from node frames and node indices. A
    /// fixed nearest-node binding is therefore enough, and exposing a knob that cannot change a
    /// result would only invite tuning it.
    /// </summary>
    [CreateAssetMenu(fileName = "EDGraphBuilderStructure", menuName = "Unity Common/ED/Graph Builder/Structure Skeleton")]
    public class EDGraphBuilderStructure : EDGraphBuilder
    {
        [SerializeField, Min(0.0f), Tooltip("Deformation field voxel size, as a fraction of the bounding box.")]
        private float fieldVoxelDensity = 0.05f;
        [SerializeField, Min(1), Tooltip("How many nodes may influence a single field cell.")]
        private int fieldMaxWeights = 4;
        [SerializeField, Tooltip("How many nodes each cell records, as opposed to how many it weights. Recording more never adds influences - only the nearest Field Max Weights are ever weighted - it stops eviction severing a node's shortest path partway, which otherwise inflates the distance recorded for nodes that ARE weighted. AllNodes removes that artifact by construction, at the cost of time.")]
        private EDFieldDistanceStorage fieldDistanceStorage = EDFieldDistanceStorage.AllNodes;
        [SerializeField, Min(1), ShowIf(nameof(usesExplicitStorage)), Tooltip("Only for measuring where the approximation stops mattering. Clamped to at least Field Max Weights and at most the node count.")]
        private int fieldStorageSlots = 4;
        [SerializeField, Tooltip("Which neighbours the distance wavefront may step to. Faces6 makes every step the same length, so the metric is L1 and the isolines are diamonds. The richer stencils add diagonals at their true cost and round that out, at the price of a corner-cutting check per diagonal step and more work per cell.")]
        private EDFieldConnectivity fieldConnectivity = EDFieldConnectivity.FacesEdgesCorners26;
        [SerializeField, Tooltip("Seed each terminal node along its connector's whole cross-section rather than from the single voxel at the frame origin. Off gives every terminal a point source, which is what the field did before.")]
        private bool useTerminalLength = true;
        [SerializeField, Tooltip("Seed every node along a bar as wide as the navigable corridor is at that node, measured across the node's right axis. This is what makes one node's distance the same kind of quantity as another's: a point source and a bar source are not comparable, so a piece where only the terminals are bars has terminals staying competitive further out than they should.")]
        private bool useCorridorLength = false;

        [SerializeField, Min(0.0f), ShowIf(nameof(useCorridorLength)), Tooltip("How fast the corridor probe's height band opens with distance, in degrees. A filter on which navmesh boundary crossings count as walls rather than as floor or ceiling - NOT a navigability limit, and no reason for it to match the slope any energy penalises. Too low rejects the far wall of a ramp; too high accepts a wall that is really the floor further along. 45 is what the probe measured with before it had a setting of its own.")]
        private float corridorConeAngleDegrees = 45.0f;

        [SerializeField, Tooltip("How a cell turns the distances it stored into blend weights. InverseDistance is what every structure golden up to now was captured against. InversePower at p = 1 does NOT reproduce it - it differs by the even-split-at-zero branch, which is the point: that comparison isolates what the branch was doing.")]
        private EDFieldWeightMode fieldWeightMode = EDFieldWeightMode.InverseDistance;
        [SerializeField, Min(0.01f), ShowIf(nameof(usesWeightPower)), Tooltip("The exponent. For InversePower, 1/max(d, floor)^p - above 1 sharpens, below 1 flattens. For Gaussian, exp(-(d/sigma)^p) - 2 is the true Gaussian, higher flattens the centre and steepens the shoulder.")]
        private float fieldWeightPower = 2.0f;
        [SerializeField, Min(1e-4f), ShowIf(nameof(usesWeightFloor)), Tooltip("Distances below this are clamped to it, so there is no singularity at zero. Clamped rather than added, so that its effect does not grow with the power being swept. World units - set it below the smallest real distance in the field and it never touches anything else.")]
        private float fieldWeightDistanceFloor = 0.01f;
        [SerializeField, Min(1e-4f), ShowIf(nameof(usesWeightSigma)), Tooltip("Gaussian falloff width. A fraction of the furthest kept distance when Normalize Distances is on, and a world-space length when it is off.")]
        private float fieldWeightSigma = 0.4f;
        [SerializeField, Range(1.01f, 2.0f), ShowIf(nameof(usesWeightAlpha)), Tooltip("Entmax alpha. Towards 1 is softmax-like and dense; 2 is sparsemax; 1.5 is the usual choice and the one with the cheapest exact solution elsewhere in the literature.")]
        private float fieldWeightAlpha = 1.5f;
        [SerializeField, Min(1e-4f), ShowIf(nameof(usesWeightTemperature)), Tooltip("Divides the distances before entmax sees them. Smaller concentrates the weights and produces more exact zeros; larger spreads them out.")]
        private float fieldWeightTemperature = 0.25f;
        [SerializeField, ShowIf(nameof(usesNormalizedDistances)), Tooltip("Divide each cell's distances by the furthest one it keeps before mapping them. This is what makes sigma and the entmax temperature dimensionless - without it they are world-space lengths, and as brittle against voxel density and piece scale as a fixed softmax temperature. Off exists to demonstrate that rather than assert it.")]
        private bool fieldWeightNormalizeDistances = true;

        [SerializeField, Tooltip("How the transforms of the influencing nodes are combined once their weights are known. The weights decide how much each node says; this decides what averaging what they say means. LinearAffine is the component-wise mean of the affine matrices - the original formulation, and what every golden is captured against. Polar splits each transform into translation, rotation and stretch and combines each where it lives.")]
        private EDFieldBlendMode fieldBlendMode = EDFieldBlendMode.LinearAffine;
        [SerializeField, ShowIf(nameof(usesDecomposedBlend)), Tooltip("How the rotations are averaged. Chordal is the exact minimiser of the chordal distance - blend the matrices as the linear mode does, then project back onto SO(3) - and is the smallest departure from the baseline. Nlerp is the cheap quaternion blend. Karcher is the intrinsic geodesic mean, the principled and slowest answer, worth having as the reference the other two are measured against.")]
        private EDFieldRotationBlend fieldRotationBlend = EDFieldRotationBlend.Chordal;
        [SerializeField, ShowIf(nameof(usesDecomposedBlend)), Tooltip("What happens to the non-rotational part. Full blends the whole symmetric stretch factor and so keeps shear. Diagonal blends only the principal stretches and reattaches them to the blended rotation's frame, which is 'translation, rotation and scale' in the usual sense and is strictly weaker - the loss is the orientation of each node's stretch.")]
        private EDFieldScaleBlend fieldScaleBlend = EDFieldScaleBlend.Full;

        private bool usesDecomposedBlend => (fieldBlendMode != EDFieldBlendMode.LinearAffine);

        private bool usesExplicitStorage => (fieldDistanceStorage == EDFieldDistanceStorage.Explicit);

        private bool usesWeightPower => (fieldWeightMode == EDFieldWeightMode.InversePower) || (fieldWeightMode == EDFieldWeightMode.Gaussian);
        private bool usesWeightFloor => (fieldWeightMode == EDFieldWeightMode.InversePower);
        private bool usesWeightSigma => (fieldWeightMode == EDFieldWeightMode.Gaussian);
        private bool usesWeightAlpha => (fieldWeightMode == EDFieldWeightMode.Entmax);
        private bool usesWeightTemperature => (fieldWeightMode == EDFieldWeightMode.Entmax);
        private bool usesNormalizedDistances => (fieldWeightMode == EDFieldWeightMode.Gaussian) || (fieldWeightMode == EDFieldWeightMode.Entmax);

        private static readonly EDBindingConfig fixedBinding = new EDBindingConfig();

        public override DeformationGraphSource graphSource => DeformationGraphSource.StructureOnly;
        public override EDBindingConfig binding => fixedBinding;
        public override float deformationFieldVoxelDensity => fieldVoxelDensity;
        public override int deformationFieldMaxWeights => fieldMaxWeights;
        public override EDFieldDistanceStorage deformationFieldDistanceStorage => fieldDistanceStorage;
        public override int deformationFieldStorageSlots => fieldStorageSlots;
        public override EDFieldConnectivity deformationFieldConnectivity => fieldConnectivity;
        public override bool deformationFieldSeedTerminals => useTerminalLength;
        public override bool deformationFieldSeedCorridors => useCorridorLength;
        public override float corridorConeAngle => corridorConeAngleDegrees;
        public override EDFieldBlendMode deformationFieldBlendMode => fieldBlendMode;
        public override EDFieldRotationBlend deformationFieldRotationBlend => fieldRotationBlend;
        public override EDFieldScaleBlend deformationFieldScaleBlend => fieldScaleBlend;

        public override FullDeformationField.WeightResolver CreateFieldWeightResolver()
        {
            switch (fieldWeightMode)
            {
                case EDFieldWeightMode.InversePower:
                    return new FullDeformationField.InversePowerWeights(fieldWeightPower, fieldWeightDistanceFloor);

                case EDFieldWeightMode.Gaussian:
                    return new FullDeformationField.GaussianWeights(fieldWeightSigma, fieldWeightPower, fieldWeightNormalizeDistances);

                case EDFieldWeightMode.Entmax:
                    return new FullDeformationField.EntmaxWeights(fieldWeightAlpha, fieldWeightTemperature, fieldWeightNormalizeDistances);

                default:
                    return new FullDeformationField.InverseDistanceWeights();
            }
        }

        public override Instance NewInstance(EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav) => new StructureInstance(this, deformation, structureSource, nav);

        public class StructureInstance : Instance
        {
            public StructureInstance(EDGraphBuilderStructure builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
                : base(builder, deformation, structureSource, nav)
            {
            }

            /// <summary>
            /// Two structure endpoints closer together than this are the same node. Not a sampling
            /// distance - the skeleton decides where nodes go - only a guard against a shared
            /// junction arriving as two coincident points from two segments.
            /// </summary>
            private const float structureNodeMergeDistanceSq = 1e-8f;

            public override void Build(List<int> forcedVertices)
            {
                var def = (EDGraphBuilderStructure)builder;

                TopologyStatic topology = ResolveTopology();
                if (topology == null) return;

                deformation.BeginGraphBuild(DeformationGraphSource.StructureOnly);

                deformation.BuildStructure(structureSource, def.maxSegmentLength, nav.tryGetSurfaceNormal);

                var structure = deformation.structure;

                if ((structure == null) || (structure.Count == 0))
                {
                    Debug.LogError("Structure graph build failed: structure is null or empty.");
                    return;
                }

                // -----------------------------------------------------------------
                // 1) Copy source navmesh into ED rest data.
                //    Even in StructureOnly mode, the mesh still needs to deform.
                // -----------------------------------------------------------------
                deformation.SetRestGeometry(topology);

                // -----------------------------------------------------------------
                // 2) Build ED graph directly from structure segment endpoints.
                // -----------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    var seg = structure[i];

                    int idx1 = deformation.AddGraphNode(seg.p1, structureNodeMergeDistanceSq);
                    int idx2 = deformation.AddGraphNode(seg.p2, structureNodeMergeDistanceSq);

                    seg.node1 = idx1;
                    seg.node2 = idx2;

                    deformation.LinkGraphNodes(idx1, idx2);
                }

                if (deformation.nodes.Count == 0)
                {
                    Debug.LogError("Structure graph build failed: no nodes were created.");
                    return;
                }

                // -----------------------------------------------------------------
                // 3) Bind navmesh vertices to the structure graph.
                // -----------------------------------------------------------------
                deformation.SetGraphBindings(topology, def.binding, def.sampleMinDistance);

                deformation.EndGraphBuild();

                Debug.Log($"ED structure-only graph built. " +
                          $"Vertices={topology.vertexCount}, " +
                          $"Triangles={topology.triangleCount}, " +
                          $"StructureSegments={structure.Count}, " +
                          $"Nodes={deformation.nodes.Count}, " +
                          $"Edges={deformation.graphEdgeCount}");

                // -----------------------------------------------------------------
                // 4) Everything that only means something on a skeleton graph.
                // -----------------------------------------------------------------
                deformation.BuildNodeRestFrames();

                // After the node loop above, which is what makes it possible: the tree is recovered
                // from seg.node1/seg.node2, and those are written there.
                deformation.BuildStructureTree();

                BuildDeformationField(def);
            }

            /// <summary>
            /// The bar length to seed each node along, indexed by node. Zero means a point source.
            ///
            /// Two independent contributions, and they are kept independent on purpose - the four
            /// combinations of the two toggles are the measurement. Corridor widths are laid down
            /// first and connector widths overwrite them, because the connector width is the stated
            /// width of the piece's mouth while the corridor probe is only an estimate of it; where
            /// both exist the stated one wins.
            ///
            /// That ordering also makes the free consistency check available: with terminal seeding
            /// off and corridor seeding on, the probe measures the terminals too, and what it
            /// measures there should come out close to the connector width it is not being given.
            /// MeasureCorridorWidths logs the comparison so a wrong measurement is caught before it
            /// reaches a golden.
            /// </summary>
            private float[] ResolveSeedLengths(EDGraphBuilderStructure def, float runawayLimit)
            {
                int nodeCount = deformation.nodes.Count;

                float[] connectorWidths = ResolveConnectorWidths();
                float[] lengths = new float[nodeCount];

                if (def.useCorridorLength)
                {
                    MeasureCorridorWidths(lengths, connectorWidths, runawayLimit, def.corridorConeAngleDegrees);
                }

                if (def.useTerminalLength)
                {
                    for (int i = 0; i < nodeCount; i++)
                    {
                        if (connectorWidths[i] > 0.0f) lengths[i] = connectorWidths[i];
                    }
                }

                return lengths;
            }

            /// <summary>
            /// Fills <paramref name="lengths"/> with the navigable corridor width at each node,
            /// measured across the node's rest right axis.
            ///
            /// The rest frames this reads are built by BuildNodeRestFrames, which Build calls
            /// immediately before BuildDeformationField. That ordering is load-bearing: run this any
            /// earlier and every node's right axis is the identity default, which measures a real
            /// width along an arbitrary direction and looks entirely plausible.
            ///
            /// Deliberately not capped by the node spacing. The spacing runs *along* the corridor and
            /// the bar runs across it, so one does not bound the other - and the width is wanted in
            /// full, since it is what the influences are then computed from. The probe terminates on
            /// its own: a bounded surface means a probe from an interior point crosses a boundary
            /// eventually, and a side that crosses nothing is unbounded rather than failed, which the
            /// symmetric width already handles by losing the Min.
            /// </summary>
            private void MeasureCorridorWidths(float[] lengths, float[] connectorWidths, float runawayLimit, float coneAngle)
            {
                var nodes = deformation.nodes;

                int measured = 0;
                int unmeasured = 0;
                int openEnded = 0;
                int runaway = 0;
                int junctions = 0;

                float minWidth = float.MaxValue;
                float maxWidth = 0.0f;
                double totalWidth = 0.0;

                for (int i = 0; i < nodes.Count; i++)
                {
                    EDNode node = nodes[i];

                    // A junction is left as a point source, and that is a statement about what the
                    // measurement means rather than a shortcut. The bar is laid across the corridor,
                    // along restRight - and at a node with three or more incident links there is no
                    // "the corridor" to be across. restForward there is the average of the outgoing
                    // edge directions, which points along none of them, so a bar laid on it is a real
                    // width measured in a direction with no meaning. A point source is the honest
                    // degenerate case: it seeds one voxel and the wavefront leaves it equally in
                    // every direction, which is what a junction actually looks like.
                    //
                    // Note what this costs, because the notes argue the other way and the argument
                    // still holds: a point source is *less* competitive than a bar, since a bar wins
                    // the nearest-source test anywhere along its length. So a junction node now loses
                    // ground near itself to the barred corridor nodes around it. That is a smaller
                    // wrong than a bar pointing somewhere arbitrary, and the fuller answer - a disc
                    // of the minimised width, genuinely omnidirectional and comparable with the bars -
                    // needs AddDeformationNode to seed something other than a segment.
                    if ((node.neighbors != null) && (node.neighbors.Count > 2))
                    {
                        junctions++;
                        continue;
                    }

                    // Rest geometry: this runs inside Build, before BuildNavigationData, so there are
                    // no bindings to deform through and nothing has moved anyway.
                    if (!deformation.TryMeasureCorridor(node.restPosition, node.restRight, node.restUp, null, null, coneAngle, out EDCorridorExtent extent))
                    {
                        unmeasured++;

                        Debug.LogWarning($"Corridor width could not be measured at node {i} ({node.restPosition.ToVector3()}): the probe crossed no navmesh boundary on either side. Seeding it as a point source. A node outside the navmesh, or one whose rest frame is degenerate, would both look like this.");
                        continue;
                    }

                    if ((!extent.hasPositive) || (!extent.hasNegative)) openEnded++;

                    float width = (float)extent.symmetricWidth;

                    // Not a clamp on a measurement that might be right - a bar longer than the piece
                    // itself is a symptom, and one that would otherwise seed silently across the whole
                    // volume. Left at zero so it reads as "not measured" rather than as a number.
                    if (width > runawayLimit)
                    {
                        runaway++;

                        Debug.LogWarning($"Corridor width at node {i} measured {width:F3}, which exceeds the piece's own extent ({runawayLimit:F3}). Discarding it and seeding a point source - something is wrong with the probe or with this node's rest frame.");
                        continue;
                    }

                    lengths[i] = width;

                    measured++;
                    totalWidth += width;
                    minWidth = Mathf.Min(minWidth, width);
                    maxWidth = Mathf.Max(maxWidth, width);

                    // The check that does not need a golden: at a terminal the probe is measuring the
                    // connector mouth, whose width is stated independently. They should agree.
                    if (connectorWidths[i] > 0.0f)
                    {
                        Debug.Log($"Corridor width at terminal node {i}: measured {width:F3} against a stated connector width of {connectorWidths[i]:F3} (+{DescribeExtent(extent.positive)} / -{DescribeExtent(extent.negative)}).");
                    }
                }

                if (measured == 0)
                {
                    Debug.LogWarning("Corridor seeding is on but not one node could be measured. Every node is a point source, which is the same field corridor seeding off would have produced - so this is on in name only.");
                    return;
                }

                Debug.Log($"Corridor widths: measured {measured} of {nodes.Count} nodes, " +
                          $"min {minWidth:F3}, max {maxWidth:F3}, mean {(totalWidth / measured):F3}. " +
                          $"Open-ended on one side {openEnded}, unmeasured {unmeasured}, discarded as runaway {runaway}, " +
                          $"junctions left as point sources {junctions}.");
            }

            /// <summary>
            /// An extent as text, with the unbounded case named rather than printed - MaxValue under
            /// a numeric format is a 300-digit number that reads as a measurement.
            /// </summary>
            private static string DescribeExtent(double extent)
            {
                return (extent == double.MaxValue) ? ("open") : (extent.ToString("F3"));
            }

            /// <summary>
            /// The connector cross-section width at each terminal node, zero elsewhere.
            ///
            /// A skeleton graph puts exactly one node at a connector, so a vertex on the connector
            /// cross-section is otherwise reached by geodesic distance from that single point - and
            /// a vertex out at the edge of the bar is measurably further from it than one at the
            /// centre, so it gets a blend where it should be controlled almost entirely by its
            /// terminal. Seeding the whole bar at distance zero removes the asymmetry without
            /// adding a variable or a per-vertex assignment.
            ///
            /// The sampled builders need none of this: every vertex the connector holds is already
            /// forced to be a node of its own and constrained directly, and they build no field.
            ///
            /// Terminals are matched by the same GetClosestLeafNodeIndex the terminal constraints
            /// use, so the node seeded along a bar is the node that bar goes on to drive.
            /// </summary>
            private float[] ResolveConnectorWidths()
            {
                float[] widths = new float[deformation.nodes.Count];

                var seeds = deformation.GetConnectorSeeds();

                if (seeds == null) return widths;

                for (int i = 0; i < seeds.Count; i++)
                {
                    EDConnectorSeed seed = seeds[i];

                    if (seed.width <= 0.0f) continue;

                    int nodeIndex = deformation.GetClosestLeafNodeIndex(seed.restPosition);

                    if ((nodeIndex < 0) || (nodeIndex >= widths.Length)) continue;

                    // Two connectors landing on one node means the skeleton has no terminal to tell them apart, and the terminal constraints are in the same trouble. Keep the
                    // wider bar rather than whichever came last, and say so - a silent overwrite here would show up only as a field that is subtly wrong at one connector.
                    if (widths[nodeIndex] > 0.0f)
                    {
                        Debug.LogWarning($"Two connectors both resolve to leaf node {nodeIndex}. Seeding it with the wider bar; the terminal constraints on it will also be in conflict.");

                        widths[nodeIndex] = Mathf.Max(widths[nodeIndex], seed.width);
                        continue;
                    }

                    widths[nodeIndex] = seed.width;
                }

                return widths;
            }

            /// <summary>
            /// Rebuilds only the volumetric field, for when it is missing rather than wrong.
            ///
            /// The field is [NonSerialized] on the deformation, so a domain reload or a scene load
            /// discards it - while everything it is computed from (source geometry, nodes, connector
            /// seeds) is serialized and survives. Recovering it therefore needs none of the rest of
            /// Build(), which would also rebuild the navmesh, the topology and the deform points and
            /// so move the starting point of the next solve.
            ///
            /// Same call the tail of Build() makes, deliberately: a field rebuilt this way has to be
            /// the one Build() would have produced, or the two routes to it diverge.
            /// </summary>
            public void RebuildDeformationField()
            {
                BuildDeformationField((EDGraphBuilderStructure)builder);
            }

            /// <summary>
            /// The shortest structure segment's rest length, or -1 with no structure to measure -
            /// the node spacing along the skeleton, which is what the distance field has to
            /// resolve for the weights to tell neighbouring nodes apart.
            /// </summary>
            private static double ShortestStructureSegment(List<NavEDSegments> structure)
            {
                if ((structure == null) || (structure.Count == 0)) return -1.0;

                double min = double.MaxValue;

                foreach (NavEDSegments segment in structure)
                    min = System.Math.Min(min, (segment.p2 - segment.p1).magnitude);

                return min;
            }

            /// <summary>
            /// Voxelizes the source geometry and seeds it with the graph nodes, so that a point
            /// anywhere in the solid is carried by geodesic distance through the volume rather than
            /// by straight-line distance through whatever wall happens to be between it and a node.
            /// </summary>
            private void BuildDeformationField(EDGraphBuilderStructure def)
            {
                DebugProfiler timer = new();
                timer.Mark();

                List<Mesh> sourceGeometry = deformation.GetSourceGeometry();

                if ((sourceGeometry == null) || (sourceGeometry.Count == 0))
                {
                    Debug.LogWarning("BuildDeformationField failed: no source geometry was provided.");
                    deformation.SetDeformationField(null);
                    return;
                }

                var nodes = deformation.nodes;

                if ((nodes == null) || (nodes.Count == 0))
                {
                    Debug.LogWarning("BuildDeformationField failed: no deformation graph nodes exist.");
                    deformation.SetDeformationField(null);
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
                    deformation.SetDeformationField(null);
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
                    deformation.SetDeformationField(null);
                    return;
                }

                float safeDensity = Mathf.Max(def.fieldVoxelDensity, 1e-5f);
                float voxelSize = maxSize * safeDensity;

                // The denser-graph experiments push structureMaxSegmentLength down while the
                // voxel is stated only indirectly (voxel = density x largest bound), so nothing
                // said when the subdivision outran the field's resolution. Two nodes closer than
                // one voxel sit in the same cell of the distance field - their distances are
                // identical everywhere and no weighting can separate them - and spacing under a
                // couple of voxels quantizes every node-to-node distance to a step or two.
                // Checked here because this is the first place both numbers exist.
                double minSegmentLength = ShortestStructureSegment(deformation.structure);

                if (minSegmentLength > 0.0)
                {
                    if (minSegmentLength < voxelSize)
                        Debug.LogError($"Structure subdivision finer than the deformation field: the shortest segment is {minSegmentLength:F4} against a voxel of {voxelSize:F4}, so adjacent nodes share a cell and the field cannot tell them apart. Raise structureMaxSegmentLength, or lower the field voxel density (voxel = density x largest bound).");
                    else if (minSegmentLength < 2.0 * voxelSize)
                        Debug.LogWarning($"Structure subdivision close to the deformation field's resolution: the shortest segment is {minSegmentLength:F4} against a voxel of {voxelSize:F4} ({minSegmentLength / voxelSize:F2} voxels), so neighbouring nodes' distances quantize to a step or two and the weights separate them poorly. Raise structureMaxSegmentLength, or lower the field voxel density (voxel = density x largest bound).");
                }

                // Storage is clamped from below by the weight count, since storing fewer than are
                // weighted is meaningless, and from above by the node count, past which there is
                // nothing left to record. Zero means "match", which keeps the default identical.
                //
                // This is not only a debug setting. Eviction is what terminates a node's wavefront,
                // so a node dropped from a cell partway along its shortest path arrives at later
                // cells by a detour - and it can still be among the nearest few there, being weighted,
                // with a distance that is too large. Extra slots buy back the accuracy of the
                // distances actually in use, not just visibility of the ones that were dropped.
                //
                // Resolved through the shared helper because the golden harness applies the same
                // clamps to decide whether an existing field still matches these settings.
                EDGraphBuilder.ResolveFieldSlots(def.fieldMaxWeights, def.fieldDistanceStorage, def.fieldStorageSlots, nodes.Count,
                                                 out int safeMaxWeights, out int storageSlots);

                // -------------------------------------------------------------
                // 2) Create deformation field.
                // -------------------------------------------------------------
                // Slot-per-node only when storage covers every node, which is exactly when no cell can
                // evict. Derived from the resolved count rather than from the mode, so it stays true
                // to what the field was actually given.
                bool slotPerNode = (storageSlots >= nodes.Count);

                FullDeformationField field = new FullDeformationField(voxelSize, safeDensity, safeMaxWeights, storageSlots, slotPerNode, def.fieldConnectivity,
                                                                      def.useTerminalLength, def.useCorridorLength, def.fieldBlendMode,
                                                                      def.fieldRotationBlend, def.fieldScaleBlend);

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

                field.FillWithMesh(sourceGeometry, identityMatrices);

                // -------------------------------------------------------------
                // 4) Add deformation graph nodes as volumetric/geodesic seeds.
                //
                //    Add them in ED node order so the deformation field node id matches the ED node index.
                // -------------------------------------------------------------
                //    The runaway limit is the piece's own diagonal. It is a symptom detector rather
                //    than a cap - see MeasureCorridorWidths for why the measurement itself is not
                //    bounded - and it is derived here because this is where the bounds exist.
                float[] seedLengths = ResolveSeedLengths(def, bounds.size.magnitude);

                for (int i = 0; i < nodes.Count; i++)
                {
                    EDNode node = nodes[i];

                    field.AddDeformationNode(node.restPosition.ToVector3(), node.restRight.ToVector3(), node.restUp.ToVector3(), node.restForward.ToVector3(), seedLengths[i]);
                }

                // -------------------------------------------------------------
                // 5) Extend the influence field outside occupied cells.
                //
                //    The occupied volume gets geodesic distances from AddDeformationNode(). GrowInfluence() lets nearby empty cells also query valid weights.
                // -------------------------------------------------------------
                field.GrowInfluence();

                // -------------------------------------------------------------
                // 6) Convert distances into normalized weights.
                // -------------------------------------------------------------
                field.ComputeWeights(safeMaxWeights, def.CreateFieldWeightResolver());
                field.BuildTrilinearRegions();

                deformation.SetDeformationField(field);

                timer.Mark();

                Debug.Log(
                    $"Deformation field built:\n" +
                    $"  Meshes={sourceGeometry.Count}\n " +
                    $"  Nodes={nodes.Count}\n" +
                    $"  VoxelSize={voxelSize:F4}\n" +
                    ((minSegmentLength > 0.0) ? ($"  MinSegment={minSegmentLength:F4} ({minSegmentLength / voxelSize:F2} voxels)\n") : ("")) +
                    $"  MaxWeights={safeMaxWeights}\n" +
                    $"  Bounds={bounds.size}\n" +
                    $"  Grid Size={field.gridSize}\n" +
                    $"  Time={timer.accumulatedTimeMS:F6} ms"
                );
            }
        }
    }
}
#endif
