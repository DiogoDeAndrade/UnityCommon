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
    ///
    /// **The deformation field, optional (2026-10-20, VED).** On, the geometry is voxelized and
    /// every node seeded as a point source, as the structure builder does, so the field quality
    /// term has the field's cells to measure. The carrier then says what moves the geometry: ED's
    /// bindings, the field contributing only its cells; or the field, under its blend, the pinned
    /// vertices included. Off, nothing here differs from what it was, and the dump reads
    /// "field none" as before.
    /// </summary>
    [CreateAssetMenu(fileName = "EDGraphBuilderMeshSampled", menuName = "Unity Common/ED/Graph Builder/Mesh Sampled")]
    public class EDGraphBuilderMeshSampled : EDGraphBuilderSampled
    {
        [SerializeField, Min(0.0f), Tooltip("Minimum spacing between sampled nodes. The geometry is far denser than a navmesh, so this decides the node count much more sharply here.")]
        private float sampleDistance = 1.0f;

        [SerializeField]
        private EDBindingConfig bindingConfig = new EDBindingConfig();

        [Header("Deformation field")]
        [SerializeField, Tooltip("Build a deformation field over the sampled graph: the geometry voxelized, every node a point source, geodesic distances through the solid. What the field quality terms measure. Off, the graph is ED's alone and the dump reads 'field none'.")]
        private bool buildField = false;
        [SerializeField, ShowIf(nameof(buildField)), Tooltip("What carries the geometry - the output, the field's cell corners the field quality term measures, the mesh terms' samples and the pinned vertices, all one way. Bindings: ED's own blend over the nearest nodes, the field giving only its cells. Deformation Field: the field's geodesic influences, trilinearly filtered, under the blend below - the pinned vertices too, so a pin is met where the output puts the vertex under either blend.")]
        private EDGeometryCarrier carrier = EDGeometryCarrier.Bindings;
        [SerializeField, Min(0.0f), ShowIf(nameof(buildField)), Tooltip("Voxel size, as a fraction of the largest side of the geometry's bounds. Nodes closer than one voxel share a cell and the field cannot tell them apart; the build says so.")]
        private float fieldVoxelDensity = 0.05f;
        [SerializeField, Min(1), ShowIf(nameof(buildField)), Tooltip("How many nodes may influence a single field cell.")]
        private int fieldMaxWeights = 4;
        [SerializeField, ShowIf(nameof(buildField)), Tooltip("How many nodes each cell records, as opposed to how many it weights. AllNodes keeps every distance exact; the others let eviction cut a node's shortest path short. See the structure builder's setting of the same name.")]
        private EDFieldDistanceStorage fieldDistanceStorage = EDFieldDistanceStorage.AllNodes;
        [SerializeField, Min(1), ShowIf(nameof(usesExplicitFieldStorage)), Tooltip("Clamped to at least Field Max Weights and at most the node count.")]
        private int fieldStorageSlots = 4;
        [SerializeField, ShowIf(nameof(buildField)), Tooltip("Which neighbours the distance wavefront may step to. Faces6 measures an L1 distance; the 26-neighbour stencil steps the diagonals at their true length.")]
        private EDFieldConnectivity fieldConnectivity = EDFieldConnectivity.FacesEdgesCorners26;
        [SerializeField, ShowIf(nameof(buildField)), Tooltip("How a cell turns its stored distances into blend weights. InversePower at p = 2 is the mapping the structure graphs settled on. The weights carry the geometry only under the Deformation Field carrier.")]
        private EDFieldWeightMode fieldWeightMode = EDFieldWeightMode.InversePower;
        [SerializeField, Min(0.01f), ShowIf(nameof(usesFieldWeightPower)), Tooltip("InversePower: 1/max(d, floor)^p. Gaussian: exp(-(d/sigma)^p).")]
        private float fieldWeightPower = 2.0f;
        [SerializeField, Min(1e-4f), ShowIf(nameof(usesFieldWeightFloor)), Tooltip("Distances below this are clamped to it, in world units.")]
        private float fieldWeightDistanceFloor = 0.01f;
        [SerializeField, Min(1e-4f), ShowIf(nameof(usesFieldWeightSigma)), Tooltip("Gaussian falloff width: a fraction of the furthest kept distance when Normalize Distances is on, a world length when it is off.")]
        private float fieldWeightSigma = 0.4f;
        [SerializeField, Range(1.01f, 2.0f), ShowIf(nameof(usesFieldWeightAlpha)), Tooltip("Entmax alpha: towards 1 softmax-like, 2 sparsemax.")]
        private float fieldWeightAlpha = 1.5f;
        [SerializeField, Min(1e-4f), ShowIf(nameof(usesFieldWeightAlpha)), Tooltip("Divides the distances before entmax sees them.")]
        private float fieldWeightTemperature = 0.25f;
        [SerializeField, ShowIf(nameof(usesFieldNormalizedDistances)), Tooltip("Divide each cell's distances by the furthest one it keeps before mapping them, so sigma and the temperature are dimensionless.")]
        private bool fieldWeightNormalizeDistances = true;
        [SerializeField, ShowIf(nameof(usesFieldCarrier)), Tooltip("How the influencing nodes' transforms are combined. LinearAffine, the default, is ED's own blend - the weighted mean of the affine maps - with the field's weights in place of the bindings', and gives the pin term an analytic Jacobian. Polar blends translation, rotation and stretch each where it lives, and the pin term's Jacobian is then taken by finite differences.")]
        private EDFieldBlendMode fieldBlendMode = EDFieldBlendMode.LinearAffine;
        [SerializeField, ShowIf(nameof(usesFieldDecomposedBlend)), Tooltip("How the rotations are averaged under Polar. Chordal is the smallest departure from the linear blend.")]
        private EDFieldRotationBlend fieldRotationBlend = EDFieldRotationBlend.Chordal;
        [SerializeField, ShowIf(nameof(usesFieldDecomposedBlend)), Tooltip("Under Polar: Full blends the whole stretch and keeps shear; Diagonal only the principal stretches.")]
        private EDFieldScaleBlend fieldScaleBlend = EDFieldScaleBlend.Full;

        private bool usesFieldCarrier => (buildField) && (carrier == EDGeometryCarrier.DeformationField);
        private bool usesFieldDecomposedBlend => (usesFieldCarrier) && (fieldBlendMode != EDFieldBlendMode.LinearAffine);
        private bool usesExplicitFieldStorage => (buildField) && (fieldDistanceStorage == EDFieldDistanceStorage.Explicit);
        private bool usesFieldWeightPower => (buildField) && ((fieldWeightMode == EDFieldWeightMode.InversePower) || (fieldWeightMode == EDFieldWeightMode.Gaussian));
        private bool usesFieldWeightFloor => (buildField) && (fieldWeightMode == EDFieldWeightMode.InversePower);
        private bool usesFieldWeightSigma => (buildField) && (fieldWeightMode == EDFieldWeightMode.Gaussian);
        private bool usesFieldWeightAlpha => (buildField) && (fieldWeightMode == EDFieldWeightMode.Entmax);
        private bool usesFieldNormalizedDistances => (buildField) && ((fieldWeightMode == EDFieldWeightMode.Gaussian) || (fieldWeightMode == EDFieldWeightMode.Entmax));

        public override bool buildsDeformationField => buildField;
        public override EDGeometryCarrier geometryCarrier => carrier;
        public override float deformationFieldVoxelDensity => fieldVoxelDensity;
        public override int deformationFieldMaxWeights => fieldMaxWeights;
        public override EDFieldDistanceStorage deformationFieldDistanceStorage => fieldDistanceStorage;
        public override int deformationFieldStorageSlots => fieldStorageSlots;
        public override EDFieldConnectivity deformationFieldConnectivity => fieldConnectivity;
        // The blend only means something when the field carries the geometry; under the bindings
        // carrier nothing is blended through the field, and the field records the plain mode.
        public override EDFieldBlendMode deformationFieldBlendMode => (usesFieldCarrier) ? (fieldBlendMode) : (EDFieldBlendMode.LinearAffine);
        public override EDFieldRotationBlend deformationFieldRotationBlend => fieldRotationBlend;
        public override EDFieldScaleBlend deformationFieldScaleBlend => fieldScaleBlend;

        // The structure builder's mapping, read off this builder's own settings. Null with the field
        // off, as for a builder that produces none.
        public override FullDeformationField.WeightResolver CreateFieldWeightResolver()
        {
            if (!buildField) return null;

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

        /// <summary>
        /// The same shared construction the navmesh builder runs, differing only in the parameters
        /// above: the geometry topology, no skeleton seeding, plain adjacency linking. The
        /// direction-aware settings are never read, because linkMode is left at the default.
        /// </summary>
        public class MeshSampledInstance : SampledInstance
        {
            public MeshSampledInstance(EDGraphBuilderMeshSampled builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
                : base(builder, deformation, structureSource, nav)
            {
            }
        }
    }
}
#endif
