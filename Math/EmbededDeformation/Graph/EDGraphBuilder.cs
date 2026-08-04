using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Where the deformation graph comes from, and the parameters that construction needs.
    ///
    /// The two builders answer genuinely different questions. Sampling the navmesh has to decide
    /// which vertices become nodes and how to link them; taking the skeleton gets both for free
    /// from the structure, and instead builds a volumetric field. Because of that they also imply
    /// different ways of deforming a point, which is why the builder produces the deformer.
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

        public float maxSegmentLength => structureMaxSegmentLength;

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

            public abstract void Build(TopologyStatic topology, List<int> forcedVertices);
        }
    }
}
#endif
