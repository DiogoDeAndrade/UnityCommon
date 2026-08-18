using UnityEngine;
using UC.DoubleMath;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Maps a point from its rest position to where the current deformation puts it.
    ///
    /// Returns a matrix rather than just a position because callers deforming real geometry also
    /// have to carry normals and tangents, and those need the linear part of the transform.
    /// </summary>
    public abstract class EDDeformer
    {
        /// <summary>
        /// The affine transform acting at a rest-space point, in world space. False when the point
        /// lies outside the deformation's influence, in which case it should be left alone.
        /// </summary>
        public abstract bool TryGetDeformationMatrix(Vector3 restWorldPosition, out Matrix4x4 matrix);

        /// <summary>
        /// Where the deformation carries a point.
        ///
        /// Virtual, with "get the transform and apply it" as the default, because that is what it
        /// means for both mechanisms here. The field path overrides it: for the linear-affine blend
        /// the two are algebraically identical but not bit-identical, and the goldens were captured
        /// against the other one.
        /// </summary>
        public virtual Vector3 DeformPosition(Vector3 restWorldPosition)
        {
            if (!TryGetDeformationMatrix(restWorldPosition, out Matrix4x4 matrix)) return restWorldPosition;

            return matrix.MultiplyPoint3x4(restWorldPosition);
        }
    }

    public partial class EmbededDeformation
    {
        private BindingSelectionMode    boundSelectionMode;
        private BindingWeightMode       boundWeightMode;
        private int                     boundK = 4;
        private float                   boundPower = 2.0f;
        private float                   boundSigma = 1.0f;

        private void RememberBindingSettings(BindingSelectionMode bindMode, BindingWeightMode weightMode, int k, float power, float sigma)
        {
            boundSelectionMode = bindMode;
            boundWeightMode = weightMode;
            boundK = k;
            boundPower = power;
            boundSigma = sigma;
        }

        /// <summary>
        /// Builds the deformer appropriate to how the graph was constructed.
        ///
        /// A structure-derived graph carries a volumetric deformation field and samples that; a
        /// navmesh-sampled graph has no field, and blends the transforms of the nodes a point is
        /// bound to. Both are exact for their own construction - the binding blend is not an
        /// approximation of the field, they are simply two different deformations.
        /// </summary>
        public EDDeformer CreateDeformer()
        {
            if ((nodes == null) || (nodes.Count == 0))
                return null;

            if (currentState == null)
                return null;

            // Deliberately not a plain null check on the field - see usesDeformationField.
            if (usesDeformationField)
                return new FieldDeformer(this);

            WarnIfFieldMissing(nameof(CreateDeformer));

            return new BindingDeformer(this);
        }

        /// <summary>
        /// Trilinear sampling of the volumetric field.
        ///
        /// The blender freezes the node frames, which are constant for the duration of a deformation
        /// pass, and precomputes each node's transform once instead of rebuilding it per vertex.
        /// </summary>
        private sealed class FieldDeformer : EDDeformer
        {
            private readonly FullDeformationField.TransformBlender blender;

            public FieldDeformer(EmbededDeformation owner)
            {
                this.blender = owner.CreateFieldBlender(new EDStateView(owner.currentState));
            }

            public override bool TryGetDeformationMatrix(Vector3 restWorldPosition, out Matrix4x4 matrix)
                => blender.TryGetMatrix(restWorldPosition, trilinear: true, out matrix);

            // Not the base "get the matrix and apply it": the field's position path never formed a
            // matrix, and under the linear-affine blend that difference is invisible in the reals and
            // real in float. LinearAffineBlender keeps the original arithmetic; this routes to it.
            public override Vector3 DeformPosition(Vector3 restWorldPosition)
                => blender.DeformPosition(restWorldPosition, trilinear: true);
        }

        /// <summary>
        /// Blends the affine transforms of the nodes a point binds to.
        ///
        /// Each node contributes R(v - g) + g + t, which rearranges to R*v + (g + t - R*g) and is
        /// therefore affine in v. The weighted sum of those is a single affine transform that does
        /// not depend on v, so it can be handed back as one matrix and applied to positions,
        /// normals and tangents alike - the same contract the field path offers.
        /// </summary>
        private sealed class BindingDeformer : EDDeformer
        {
            private readonly EmbededDeformation owner;
            private readonly EDStateView state;

            public BindingDeformer(EmbededDeformation owner)
            {
                this.owner = owner;
                this.state = new EDStateView(owner.currentState);
            }

            public override bool TryGetDeformationMatrix(Vector3 restWorldPosition, out Matrix4x4 matrix)
            {
                var binding = owner.BindPoint(restWorldPosition);

                return owner.TryBlendBindingAffine(binding, state, out matrix);
            }
        }

        /// <summary>
        /// Binds an arbitrary world point with the settings the graph was built under.
        ///
        /// The settings are remembered rather than passed because a point bound with different ones
        /// is not the point this deformation acts on. That matters most for the scene-view probe: a
        /// tool answering "where does this go" from a binding the solve never used would be worse
        /// than one that could not answer at all.
        /// </summary>
        internal EDVertexBinding BindPoint(Vector3 restWorldPosition)
        {
            return GetBinding(restWorldPosition.ToDVector3(),
                              boundSelectionMode,
                              boundWeightMode,
                              boundK,
                              boundPower,
                              boundSigma);
        }

        /// <summary>
        /// Blends a binding's nodes into the single affine transform acting at that point.
        ///
        /// Shared by the deformer that produces geometry and by the scene-view probe, so what a tool
        /// reports and what the mesh actually does cannot drift apart. That is the whole value of it
        /// being one method - a probe that agreed with the mesh only by reimplementing it would stop
        /// agreeing the first time either changed.
        /// </summary>
        internal bool TryBlendBindingAffine(in EDVertexBinding binding, EDStateView state, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;

            if ((binding.nodeIndices == null) || (binding.nodeIndices.Length == 0))
                return false;

            // Accumulated in double precision to match the rest of the solver, then narrowed
            // once at the end.
            double m00 = 0.0, m01 = 0.0, m02 = 0.0, m03 = 0.0;
            double m10 = 0.0, m11 = 0.0, m12 = 0.0, m13 = 0.0;
            double m20 = 0.0, m21 = 0.0, m22 = 0.0, m23 = 0.0;

            double totalWeight = 0.0;

            for (int i = 0; i < binding.nodeIndices.Length; i++)
            {
                int nodeIndex = binding.nodeIndices[i];
                double w = binding.weights[i];

                if (w == 0.0) continue;

                int o = nodeIndex * 12;

                double r00 = state.Get(o + 0), r01 = state.Get(o + 1), r02 = state.Get(o + 2), tx = state.Get(o + 3);
                double r10 = state.Get(o + 4), r11 = state.Get(o + 5), r12 = state.Get(o + 6), ty = state.Get(o + 7);
                double r20 = state.Get(o + 8), r21 = state.Get(o + 9), r22 = state.Get(o + 10), tz = state.Get(o + 11);

                DVector3 g = nodes[nodeIndex].restPosition;

                // Translation of this node's affine: g + t - R*g
                double ox = g.x + tx - ((r00 * g.x) + (r01 * g.y) + (r02 * g.z));
                double oy = g.y + ty - ((r10 * g.x) + (r11 * g.y) + (r12 * g.z));
                double oz = g.z + tz - ((r20 * g.x) + (r21 * g.y) + (r22 * g.z));

                m00 += w * r00; m01 += w * r01; m02 += w * r02; m03 += w * ox;
                m10 += w * r10; m11 += w * r11; m12 += w * r12; m13 += w * oy;
                m20 += w * r20; m21 += w * r21; m22 += w * r22; m23 += w * oz;

                totalWeight += w;
            }

            if (totalWeight <= 0.0)
                return false;

            matrix.SetRow(0, new Vector4((float)m00, (float)m01, (float)m02, (float)m03));
            matrix.SetRow(1, new Vector4((float)m10, (float)m11, (float)m12, (float)m13));
            matrix.SetRow(2, new Vector4((float)m20, (float)m21, (float)m22, (float)m23));
            matrix.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

            return true;
        }

        /// <summary>
        /// The blended affine acting at an arbitrary world point, in binding mode.
        /// </summary>
        internal bool TryGetDebugBindingMatrix(Vector3 restWorldPosition, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;

            if ((nodes == null) || (nodes.Count == 0) || (currentState == null))
                return false;

            return TryBlendBindingAffine(BindPoint(restWorldPosition), new EDStateView(currentState), out matrix);
        }

        /// <summary>
        /// Where one node alone would carry a point, ignoring every other influence: R(v - g) + g + t.
        ///
        /// The binding-mode counterpart of the field's single-frame sampling, and the thing that turns
        /// "this point went somewhere absurd" into "this node took it there".
        /// </summary>
        internal Vector3 DeformDebugPointBySingleNode(Vector3 restWorldPosition, int nodeIndex)
        {
            if ((nodes == null) || (nodeIndex < 0) || (nodeIndex >= nodes.Count) || (currentState == null))
                return restWorldPosition;

            EDStateView state = new EDStateView(currentState);

            int o = nodeIndex * 12;

            DVector3 g = nodes[nodeIndex].restPosition;
            DVector3 v = restWorldPosition.ToDVector3();

            double dx = v.x - g.x;
            double dy = v.y - g.y;
            double dz = v.z - g.z;

            double x = ((state.Get(o + 0) * dx) + (state.Get(o + 1) * dy) + (state.Get(o + 2) * dz)) + g.x + state.Get(o + 3);
            double y = ((state.Get(o + 4) * dx) + (state.Get(o + 5) * dy) + (state.Get(o + 6) * dz)) + g.y + state.Get(o + 7);
            double z = ((state.Get(o + 8) * dx) + (state.Get(o + 9) * dy) + (state.Get(o + 10) * dz)) + g.z + state.Get(o + 11);

            return new DVector3(x, y, z).ToVector3();
        }

        /// <summary>
        /// Whether this deformation maps points through the volumetric field rather than through
        /// bindings. Exposed so a diagnostic can say which mechanism it is reporting on, since the
        /// two answer the same question by different means.
        /// </summary>
        internal bool UsesDeformationFieldForDebug => usesDeformationField;
    }
}
#endif
