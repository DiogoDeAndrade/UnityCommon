using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// How many nodes a deformation field cell records, as distinct from how many it weights.
    ///
    /// Stated as an intent rather than a count because the count is not the thing being chosen. The
    /// slot limit is applied while the wavefronts are still propagating - a node evicted from a cell
    /// stops spreading out of it - so it does not merely discard distances, it changes the ones that
    /// remain. A node whose shortest path was cut arrives later by a detour and can still be among
    /// the nearest few, weighted, with a distance that is too large.
    ///
    /// AllNodes removes that by construction: a cell can be reached by at most every node, so with
    /// that many slots eviction never fires, no path is ever cut, and each distance is a function of
    /// its own node and the geometry alone.
    /// </summary>
    public enum EDFieldDistanceStorage
    {
        /// <summary>Record only what is weighted. Cheapest, and the distances are approximate in the
        /// way described above.</summary>
        NearestWeighted,
        /// <summary>Record a stated number, for measuring where the approximation stops mattering.
        /// A fixed count silently stops meaning what it meant if the node count changes.</summary>
        Explicit,
        /// <summary>Record every node that reaches a cell. Exact, and the slowest.</summary>
        AllNodes
    }

    /// <summary>
    /// How a deformation field cell turns the distances it stored into blend weights.
    ///
    /// An enum here, resolved into a FullDeformationField.WeightResolver at build time, rather than a
    /// resolver reference on the asset. The enum and its parameters are what the golden dump records
    /// and what the harness compares; the resolver is behaviour, built per graph build and discarded
    /// with it, which keeps it out of serialization entirely.
    /// </summary>
    public enum EDFieldWeightMode
    {
        /// <summary>1/max(d, eps) over the sum, splitting evenly at zero. The mapping every structure
        /// golden up to now was captured against.</summary>
        InverseDistance,
        /// <summary>1/max(d, floor)^p. Sharper above p = 1, flatter below. The floor clamps the
        /// distance rather than being added to it, so its effect does not grow with p. At p = 1 it
        /// differs from InverseDistance by the even-split branch and nothing else.</summary>
        InversePower,
        /// <summary>exp(-(d/sigma)^p), with sigma a fraction of the furthest kept distance.</summary>
        Gaussian,
        /// <summary>Alpha-entmax over negated distances. The only mapping here that can put a weight
        /// at exactly zero through the normalization rather than through a cutoff, so the effective
        /// number of influences varies by itself.</summary>
        Entmax,
        /// <summary>The embedded deformation paper's weight, (1 - d / d_max)² over the k nearest with
        /// d_max the (k + 1)-th distance - the bindings' OriginalED, on the field's geodesic distances
        /// (2026-10-30). A node's weight and its slope both reach zero as it leaves the k set, so the
        /// handover between node sets is smooth, which none of the mappings above gives at a hard k.
        /// Appended, since assets store the index.</summary>
        OriginalED
    }

    /// <summary>
    /// What carries the geometry of a graph that has a deformation field: ED's own bindings, or
    /// the field. One choice for everything a point of the geometry goes through - the output mesh,
    /// the field's own cell corners that the field quality term measures, the mesh quality terms'
    /// samples and the vertices the constraints pin - so a term always measures the deformation
    /// the geometry suffers.
    ///
    /// Only a sampled graph has the choice. The structure graph always deforms through its field.
    /// </summary>
    public enum EDGeometryCarrier
    {
        /// <summary>The bindings, as ED deforms: each point blended linearly over its nearest
        /// nodes by the builder's binding settings. The field then contributes only its cells -
        /// where the solid is - and none of its weights.</summary>
        Bindings,
        /// <summary>The field: each point carried by the nodes the field's geodesic distances
        /// pick, trilinearly filtered, under the field's blend - the pinned vertices too, which
        /// the vertex constraint term deforms as the output does (analytic under the linear blend,
        /// finite differences under a decomposed one). The bindings stay ED's.</summary>
        DeformationField
    }

    /// <summary>
    /// Where the deformation graph comes from, and the construction that produces it.
    ///
    /// The two builders answer genuinely different questions. Sampling the navmesh has to decide
    /// which vertices become nodes and how to link them; taking the skeleton gets both for free
    /// from the structure, and instead builds a volumetric field. Because of that they also imply
    /// different ways of deforming a point, which is why the builder produces the deformer.
    ///
    /// A builder owns no state. It queries the deformation for what it needs - the topology it is
    /// built over, the agent parameters, the skeleton - and pushes what it produces back through
    /// the deformation's Add/Set calls. What it owns is its own parameters and its own algorithm,
    /// which is the point: a builder no longer has to flatten both into one call whose signature is
    /// the union of every builder's needs and whose unused arguments it has to invent values for.
    ///
    /// Definition and instance are split as elsewhere: the asset is shared and read-only during a
    /// build, the instance holds the live scene references and per-build state.
    /// </summary>
    public abstract class EDGraphBuilder : ScriptableObject
    {
        [SerializeField, Min(0.0f), Label("Structure Max Segment Length"), Tooltip("Subdivide skeleton segments longer than this. Zero leaves them intact.")]
        protected float structureMaxSegmentLength = 0.0f;

        // Absolute is what every golden's builder is: the two lengths in world units.
        [SerializeField, Label("Length Basis"), Tooltip("What this builder's lengths are in - Structure Max Segment Length, and a sampled builder's Sample Distance: world units (Absolute), or fractions of the rest geometry's size, the largest side of its bounding box, which is the field voxel's own basis - so one asset serves a unit cactus and a hundred-unit bar alike, and the ratio of node spacing to field voxel does not depend on the mesh. The dump names the basis and the resolved lengths when it is not Absolute.")]
        private EDLengthBasis lengthBasisSetting = EDLengthBasis.Absolute;

        /// <summary>What the two lengths are read in; see <see cref="ResolveLength"/>.</summary>
        public EDLengthBasis lengthBasis => lengthBasisSetting;

        public abstract DeformationGraphSource graphSource { get; }

        /// <summary>
        /// Which mesh this builder wants to be handed. The owner resolves it and passes the matching
        /// topology to Build, so the graph, the bindings and the vertex constraints cannot end up
        /// disagreeing about which geometry they are on - which they would, silently, if each
        /// resolved it separately.
        ///
        /// The navmesh by default, because that is what every builder here has always been given.
        /// </summary>
        public virtual EDTopologySource topologySource => EDTopologySource.NavMesh;

        // Surfaced for the diagnostic dump so a golden file records how its graph was built,
        // whichever builder produced it. Overridden where the builder actually has the setting.
        public abstract EDBindingConfig binding { get; }
        public virtual float sampleMinDistance => 1.0f;
        public virtual bool forceStructureNodes => false;
        public virtual GraphLinkMode linkMode => GraphLinkMode.PartitionAdjacency;
        public virtual float maxBindDistance => 2.0f;
        public virtual float minBindAngle => 20.0f;
        /// <summary>
        /// Whether this builder builds a deformation field. The structure builder always does; a
        /// sampled builder when its option is on, and then <see cref="geometryCarrier"/> says
        /// whether the field or the bindings carry the geometry.
        /// </summary>
        public virtual bool buildsDeformationField => false;
        public virtual EDGeometryCarrier geometryCarrier => EDGeometryCarrier.Bindings;
        public virtual float deformationFieldVoxelDensity => 0.05f;
        public virtual int deformationFieldMaxWeights => 4;
        public virtual EDFieldDistanceStorage deformationFieldDistanceStorage => EDFieldDistanceStorage.NearestWeighted;
        public virtual int deformationFieldStorageSlots => 0;
        public virtual EDFieldConnectivity deformationFieldConnectivity => EDFieldConnectivity.Faces6;
        public virtual bool deformationFieldSeedTerminals => false;
        public virtual bool deformationFieldSeedCorridors => false;

        /// <summary>
        /// How a rest position finds the nodes that carry it - see EDVertexInfluenceMethod. Under
        /// AlongSkeleton the seed bars are not used, and a builder reports its seeding toggles as off.
        /// </summary>
        public virtual EDVertexInfluenceMethod deformationFieldInfluenceMethod => EDVertexInfluenceMethod.ThroughVolume;

        /// <summary>
        /// What a node's cross-section is under AlongSkeleton - see EDSkeletonSection.
        /// </summary>
        public virtual EDSkeletonSection deformationFieldSkeletonSections => EDSkeletonSection.LinkPerpendicular;

        /// <summary>
        /// What AlongSkeleton does where its sections fail to tile the corridor - see EDSkeletonKinkFix -
        /// and the blend distance of the fix as a fraction of the link's rest length.
        /// </summary>
        public virtual EDSkeletonKinkFix deformationFieldSkeletonKinkFix => EDSkeletonKinkFix.None;
        public virtual float deformationFieldSkeletonKinkDistance => 0.5f;

        /// <summary>
        /// Whether a terminal node's rest frame takes its forward from its connector, the door's
        /// normal, rather than from its last link. On, the terminal frames in the dump's graph
        /// section move wherever a door is oblique to the link that reaches it.
        /// </summary>
        public virtual bool terminalFramesFromConnectors => false;

        /// <summary>
        /// How fast the corridor probe's height band opens with distance, in degrees. Only read when
        /// deformationFieldSeedCorridors is on, since that is the one thing that measures corridors.
        ///
        /// **A filter, not a navigability limit.** It decides which navmesh boundary crossings count
        /// as walls of the corridor rather than as floor or ceiling; the slope an energy penalises is
        /// a different question and the two are free to disagree. 45 is what the probe has always
        /// used in practice - it read EmbededDeformation.maxSlope, which Build resets to that default
        /// - so this is the existing behaviour given a name and an owner.
        /// </summary>
        public virtual float corridorConeAngle => 45.0f;

        /// <summary>
        /// A factor on the corridor width for the bend compression term and the width gizmos -
        /// NOT for the field's seeding, which stays the measured width. 1 is the width as measured;
        /// larger asks "what if the corridor were this much wider", which on a piece whose folds
        /// sit in geometry outside the navmesh width (a handrail beside the walkable strip) is a way
        /// of finding out whether the width is what the term is missing. A diagnostic, not a
        /// setting for practical use, and recorded in the dump whenever it is not 1.
        /// </summary>
        public virtual float corridorWidthScale => 1.0f;

        /// <summary>
        /// How the field combines the transforms of the nodes influencing a point.
        ///
        /// On the builder beside the weighting rather than on the output, even though it is strictly a
        /// property of evaluation and not of construction. A builder already decides how deformation
        /// happens here - a binding-based one deforms through bindings and has no field to blend at
        /// all - so this is the same kind of choice as the ones around it, and it lands in the golden
        /// dump by the same route.
        /// </summary>
        public virtual EDFieldBlendMode deformationFieldBlendMode => EDFieldBlendMode.LinearAffine;

        /// <summary>
        /// The two halves of a decomposed blend. Ignored by LinearAffine, which has no parts to
        /// combine separately.
        /// </summary>
        public virtual EDFieldRotationBlend deformationFieldRotationBlend => EDFieldRotationBlend.Chordal;
        public virtual EDFieldScaleBlend deformationFieldScaleBlend => EDFieldScaleBlend.Full;

        /// <summary>
        /// What the field's trilinear sampling interpolates between cell centres - the cells' weights,
        /// every golden's, or their distances, the weights then made at the point (2026-10-30). See
        /// EDFieldInterpolation.
        /// </summary>
        public virtual EDFieldInterpolation deformationFieldInterpolation => EDFieldInterpolation.Weights;

        /// <summary>
        /// The distance-to-weight mapping this builder's settings describe, freshly built.
        ///
        /// Null for a builder that produces no field, which ComputeWeights reads as the legacy
        /// inverse-distance mapping. Called by the field build and, separately, by the golden harness
        /// to ask what the settings *now* say so it can compare that against what the built field
        /// actually used - which is why it returns a new instance rather than a cached one.
        /// </summary>
        public virtual FullDeformationField.WeightResolver CreateFieldWeightResolver() => null;

        public float maxSegmentLength => structureMaxSegmentLength;

        /// <summary>
        /// A length setting of this builder in world units: the setting itself under Absolute, or
        /// the setting times the rest size - the largest side of the rest geometry's bounding box,
        /// <see cref="RestSizeOf"/> - under FractionOfRestSize (2026-10-23).
        /// </summary>
        public float ResolveLength(float setting, double restSize)
            => (lengthBasisSetting == EDLengthBasis.FractionOfRestSize) ? ((float)(setting * restSize)) : (setting);

        public float ResolvedSampleMinDistance(double restSize) => ResolveLength(sampleMinDistance, restSize);
        public float ResolvedMaxSegmentLength(double restSize) => ResolveLength(structureMaxSegmentLength, restSize);

        /// <summary>
        /// The largest side of a topology's vertex bounds, or 1 for an empty one: what a
        /// FractionOfRestSize length is a fraction of. EmbededDeformation.restSize reads the same
        /// number off the rest vertices this topology becomes, so a build and a dump agree.
        /// </summary>
        public static double RestSizeOf(TopologyStatic topology)
        {
            if ((topology == null) || (topology.vertexCount == 0)) return 1.0;

            Vector3 min = topology.GetVertexPosition(0);
            Vector3 max = min;

            for (int i = 1; i < topology.vertexCount; i++)
            {
                Vector3 p = topology.GetVertexPosition(i);

                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 size = max - min;
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            return (largest > 0.0f) ? (largest) : (1.0);
        }

        /// <summary>
        /// The slot counts a field built from these settings would end up with.
        ///
        /// Shared rather than duplicated: the field build applies these clamps, and so does the
        /// golden harness when it checks whether an existing field still matches the settings. Two
        /// copies of this arithmetic would agree right up until one of them was edited, and the
        /// symptom would be a verification pass that means nothing.
        /// </summary>
        public static void ResolveFieldSlots(int maxWeightsSetting, EDFieldDistanceStorage storage, int storageSlotsSetting, int nodeCount, out int maxWeights, out int storageSlots)
        {
            int usableNodes = Mathf.Max(1, nodeCount);

            maxWeights = Mathf.Clamp(maxWeightsSetting, 1, usableNodes);

            switch (storage)
            {
                // Resolved against the live node count rather than stored, so it stays "all" when the
                // graph gains a node instead of quietly becoming a number that used to be all.
                case EDFieldDistanceStorage.AllNodes:
                    storageSlots = usableNodes;
                    break;

                case EDFieldDistanceStorage.Explicit:
                    storageSlots = Mathf.Clamp(storageSlotsSetting, maxWeights, usableNodes);
                    break;

                default:
                    storageSlots = maxWeights;
                    break;
            }
        }

        public abstract Instance NewInstance(EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav);

        public abstract class Instance
        {
            public EDGraphBuilder       builder { get; private set; }
            public EmbededDeformation   deformation { get; private set; }

            /// <summary>
            /// Snapshot of the skeleton. Never serialized - it wraps live scene objects.
            /// </summary>
            public IEDStructureSource   structureSource { get; private set; }
            public EDNavQueries         nav { get; private set; }

            protected Instance(EDGraphBuilder builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
            {
                this.builder = builder;
                this.deformation = deformation;
                this.structureSource = structureSource;
                this.nav = nav;
            }

            // The builder's two lengths in world units for this build, resolved once against the
            // topology's rest size at the start of Build (2026-10-23); before that, against the
            // deformation's, so a caller outside Build - BuildNavigationData after a reload - reads
            // the same number a build would.
            private bool  lengthsResolved;
            private float sampleMinDistanceResolved;
            private float maxSegmentLengthResolved;

            /// <summary>The rest size the lengths were resolved against: the largest side of the topology's bounds.</summary>
            public double restSize { get; private set; } = 1.0;

            /// <summary>The builder's sample distance in world units for this build.</summary>
            public float resolvedSampleMinDistance => (lengthsResolved) ? (sampleMinDistanceResolved) : (builder.ResolvedSampleMinDistance(deformation.restSize));

            /// <summary>The builder's structure segment cap in world units for this build.</summary>
            public float resolvedMaxSegmentLength => (lengthsResolved) ? (maxSegmentLengthResolved) : (builder.ResolvedMaxSegmentLength(deformation.restSize));

            /// <summary>Resolves the two lengths against the topology the build is on; the first thing a Build does with its topology.</summary>
            protected void ResolveLengths(TopologyStatic topology)
            {
                restSize = RestSizeOf(topology);
                sampleMinDistanceResolved = builder.ResolvedSampleMinDistance(restSize);
                maxSegmentLengthResolved = builder.ResolvedMaxSegmentLength(restSize);
                lengthsResolved = true;
            }

            /// <summary>
            /// The mesh this builder is built over, or null with the error already reported. The
            /// owner supplies both candidates through SetTopology before the build; asking for it
            /// here rather than being handed it is what stops the graph, the bindings and the
            /// vertex constraints ending up on different geometry.
            /// </summary>
            protected TopologyStatic ResolveTopology()
            {
                TopologyStatic topology = deformation.GetTopology(builder.topologySource);

                if (topology == null)
                    Debug.LogError($"{builder.name}: no {builder.topologySource} topology was supplied. Call SetTopology before building.");

                return topology;
            }

            /// <summary>
            /// The report the owner is timing a build into, or null. Set around Build, so the
            /// stages inside - the skeleton, the bindings, the field's passes - land under whatever
            /// stage the owner opened; null times nothing. A property rather than an argument of
            /// Build because RebuildDeformationField reaches the same stages from a press, which
            /// has no report of this kind.
            /// </summary>
            public DebugProfilerReport timings { get; set; }

            /// <summary>A stage of the owner's report, or nothing to dispose when there is none. Keep it in a using.</summary>
            protected IDisposable Stage(string name) => timings?.Begin(name);

            public abstract void Build(List<int> forcedVertices);

            /// <summary>
            /// Rebuilds only the deformation field, for when it is missing after a reload or stale
            /// after a settings change, without the rest of Build. Nothing for a builder that
            /// builds no field.
            /// </summary>
            public virtual void RebuildDeformationField()
            {
            }

            /// <summary>
            /// Builds the per-segment bindings and clearance probes the nav-aware energies measure
            /// through. Separate from Build, and driven by the owner, because whether a run is
            /// navigation-aware is a property of the solver and the energy model rather than of the
            /// graph - the same structure graph serves both a NavED run and a plain one.
            ///
            /// The binding configuration comes from the builder because that is where it lives now:
            /// it describes how this graph reaches geometry, which is a construction question.
            /// </summary>
            public virtual void BuildNavigationData()
            {
                deformation.BuildNavigationData(builder.binding, resolvedSampleMinDistance);
            }
        }
    }

    /// <summary>
    /// What a graph builder's lengths are in (2026-10-23): world units, or fractions of the rest
    /// geometry's size - the largest side of its bounding box, the basis the field's voxel density
    /// already uses - so one builder asset serves meshes of any size.
    /// </summary>
    public enum EDLengthBasis
    {
        Absolute,
        FractionOfRestSize,
    }
}
#endif
