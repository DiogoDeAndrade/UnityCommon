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
        public virtual float deformationFieldVoxelDensity => 0.05f;
        public virtual int deformationFieldMaxWeights => 4;
        public virtual EDFieldDistanceStorage deformationFieldDistanceStorage => EDFieldDistanceStorage.NearestWeighted;
        public virtual int deformationFieldStorageSlots => 0;
        public virtual EDFieldConnectivity deformationFieldConnectivity => EDFieldConnectivity.Faces6;
        public virtual bool deformationFieldSeedTerminals => false;
        public virtual bool deformationFieldSeedCorridors => false;

        public float maxSegmentLength => structureMaxSegmentLength;

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

            public abstract void Build(List<int> forcedVertices);

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
                deformation.BuildNavigationData(builder.binding, builder.sampleMinDistance);
            }
        }
    }
}
#endif
