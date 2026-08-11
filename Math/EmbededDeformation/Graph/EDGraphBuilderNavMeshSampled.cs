using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Builds the deformation graph by sampling the navmesh itself: pick vertices at least a
    /// certain distance apart as nodes, bind every navmesh vertex to some of them, and link the
    /// nodes according to the chosen strategy.
    ///
    /// This is the classic embedded deformation construction. The skeleton is still used, but only
    /// to seed nodes and to carry the navigation structure - it does not define the graph.
    /// </summary>
    [CreateAssetMenu(fileName = "EDGraphBuilderNavMeshSampled", menuName = "Unity Common/ED/Graph Builder/NavMesh Sampled")]
    public class EDGraphBuilderNavMeshSampled : EDGraphBuilderSampled
    {
        [SerializeField, Min(0.0f), Tooltip("Minimum spacing between sampled nodes.")]
        private float sampleDistance = 1.0f;
        [SerializeField, Tooltip("Force skeleton endpoints to become nodes as well as the sampled vertices.")]
        private bool forceStructure = true;

        [SerializeField]
        private EDBindingConfig bindingConfig = new EDBindingConfig();

        [SerializeField, Tooltip("How nodes are connected to each other.")]
        private GraphLinkMode links = GraphLinkMode.PartitionAdjacency;
        [SerializeField, ShowIf(nameof(isDirectionAware)), Min(0.0f)]
        private float directionAwareMaxDistance = 2.0f;
        [SerializeField, ShowIf(nameof(isDirectionAware)), Min(0.0f)]
        private float directionAwareMinAngle = 20.0f;

        private bool isDirectionAware => (links == GraphLinkMode.DirectionAware);

        public override DeformationGraphSource graphSource => DeformationGraphSource.NavMeshAndStructure;
        public override EDBindingConfig binding => bindingConfig;
        public override float sampleMinDistance => sampleDistance;
        public override bool forceStructureNodes => forceStructure;
        public override GraphLinkMode linkMode => links;
        public override float maxBindDistance => directionAwareMaxDistance;
        public override float minBindAngle => directionAwareMinAngle;

        public override Instance NewInstance(EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
            => new NavMeshSampledInstance(this, deformation, structureSource, nav);

        /// <summary>
        /// Nothing of its own: the sampling, the binding and all three linking strategies are the
        /// shared construction, driven entirely by the parameters above.
        /// </summary>
        public class NavMeshSampledInstance : SampledInstance
        {
            public NavMeshSampledInstance(EDGraphBuilderNavMeshSampled builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
                : base(builder, deformation, structureSource, nav)
            {
            }
        }
    }
}
#endif
