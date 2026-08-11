using System.Collections.Generic;
using UnityEngine;

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

        private static readonly EDBindingConfig fixedBinding = new EDBindingConfig();

        public override DeformationGraphSource graphSource => DeformationGraphSource.StructureOnly;
        public override EDBindingConfig binding => fixedBinding;
        public override float deformationFieldVoxelDensity => fieldVoxelDensity;
        public override int deformationFieldMaxWeights => fieldMaxWeights;

        public override Instance NewInstance(EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav) => new StructureInstance(this, deformation, structureSource, nav);

        public class StructureInstance : Instance
        {
            public StructureInstance(EDGraphBuilderStructure builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
                : base(builder, deformation, structureSource, nav)
            {
            }

            public override void Build(TopologyStatic topology, List<int> forcedVertices)
            {
                var def = (EDGraphBuilderStructure)builder;
                var b = fixedBinding;

                // Sampling distance, link mode, forced vertices and the direction-aware settings are all ignored by the structure path; they are passed as defaults rather than left to look meaningful.
                deformation.BuildDeformationGraph(DeformationGraphSource.StructureOnly, topology, 1.0f, forcedVertices, false,
                                                  b.selectionMode, b.weightMode,
                                                  GraphLinkMode.PartitionAdjacency,
                                                  structureSource, def.structureMaxSegmentLength,
                                                  nav.upVector, nav.tryGetSurfaceNormal,
                                                  b.nearestK, 2.0f, 20.0f,
                                                  nav.hasLOS,
                                                  b.attenuationPower,
                                                  b.ResolveSigma(1.0f),
                                                  def.fieldVoxelDensity, def.fieldMaxWeights);
            }
        }
    }
}
#endif
