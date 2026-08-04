using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Embedded deformation as originally published: sample the graph from the object being
    /// deformed.
    ///
    /// Everything else here samples a navigation mesh - a proxy for the object - and that difference
    /// is not cosmetic. A graph living on a walkable surface has to deform walls, ceilings and
    /// undersides that sit several node spacings away from the nearest node, and a per-node affine
    /// displaces a point by roughly the distance from the node times how far its frame turned. Far
    /// geometry is therefore extrapolated rather than deformed. Sumner never meets this, because
    /// there the graph is sampled from the thing it deforms.
    ///
    /// The sampling is the same construction the navmesh builder uses, because that construction was
    /// never navmesh-specific - it picks vertices a minimum distance apart, binds the rest to them,
    /// and links by adjacency. Only the mesh changes, which is what topologySource says.
    ///
    /// What is deliberately absent: skeleton seeding, and direction-aware linking. Both are
    /// navigation notions. Linking geometry nodes by line of sight through a navmesh would be a
    /// query about somewhere the nodes do not live.
    ///
    /// **This is only coherent with plain-ED energies.** Clearance, slope, segment length and the
    /// terminal energies all measure navigation or skeleton quantities that a geometry-sampled graph
    /// does not have. Pairing it with those is not a configuration worth supporting, and the owner
    /// is expected to reject it rather than produce a number for it.
    /// </summary>
    [CreateAssetMenu(fileName = "EDGraphBuilderMeshSampled", menuName = "Unity Common/ED/Graph Builder/Mesh Sampled")]
    public class EDGraphBuilderMeshSampled : EDGraphBuilder
    {
        [SerializeField, Min(0.0f), Tooltip("Minimum spacing between sampled nodes. The geometry is far denser than a navmesh, so this decides the node count much more sharply here.")]
        private float sampleDistance = 1.0f;

        [SerializeField]
        private EDBindingConfig bindingConfig = new EDBindingConfig();

        // Reported as NavMeshAndStructure because that is what the enum means everywhere it is read:
        // not "the graph came from a navmesh" but "the graph was sampled and carries bindings",
        // as against a skeleton graph carrying a volumetric field. Introducing a third value would
        // change the meaning of every test of it - including the one deciding whether clearance is
        // measured through the field - for a distinction those tests do not care about.
        public override DeformationGraphSource graphSource => DeformationGraphSource.NavMeshAndStructure;

        public override EDTopologySource topologySource => EDTopologySource.SourceGeometry;

        public override EDBindingConfig binding => bindingConfig;
        public override float sampleMinDistance => sampleDistance;

        // Left at the base defaults on purpose: no skeleton seeding, and plain adjacency linking.
        // Both alternatives are navigation-specific, and the values still surface in the dump so a
        // golden records that this configuration did not use them.

        public override Instance NewInstance(EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
            => new MeshSampledInstance(this, deformation, structureSource, nav);

        public class MeshSampledInstance : Instance
        {
            public MeshSampledInstance(EDGraphBuilderMeshSampled builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
                : base(builder, deformation, structureSource, nav)
            {
            }

            public override void Build(TopologyStatic topology, List<int> forcedVertices)
            {
                var def = (EDGraphBuilderMeshSampled)builder;
                var b = def.bindingConfig;

                deformation.BuildDeformationGraph(DeformationGraphSource.NavMeshAndStructure,
                                                  topology,
                                                  def.sampleDistance,
                                                  forcedVertices,
                                                  // No skeleton seeding: the nodes come from the
                                                  // geometry and nothing else.
                                                  false,
                                                  b.selectionMode,
                                                  b.weightMode,
                                                  GraphLinkMode.PartitionAdjacency,
                                                  structureSource,
                                                  def.structureMaxSegmentLength,
                                                  nav.upVector,
                                                  nav.tryGetSurfaceNormal,
                                                  b.nearestK,
                                                  // Direction-aware linking is not offered, so the
                                                  // two values it needs are inert.
                                                  0.0f,
                                                  0.0f,
                                                  null,
                                                  b.attenuationPower,
                                                  b.ResolveSigma(def.sampleDistance));
            }
        }
    }
}
#endif
