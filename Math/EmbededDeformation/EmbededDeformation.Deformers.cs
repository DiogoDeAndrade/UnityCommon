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

            if (deformationField != null)
                return new FieldDeformer(this);

            return new BindingDeformer(this);
        }

        /// <summary>
        /// Trilinear sampling of the volumetric field. The node frames are captured once, since
        /// they are constant for the duration of a deformation pass.
        /// </summary>
        private sealed class FieldDeformer : EDDeformer
        {
            private readonly EmbededDeformation owner;
            private readonly System.Collections.Generic.List<FullDeformationField.Frame> nodeFrames;

            public FieldDeformer(EmbededDeformation owner)
            {
                this.owner = owner;
                this.nodeFrames = owner.BuildNodeFrames(new EDStateView(owner.currentState));
            }

            public override bool TryGetDeformationMatrix(Vector3 restWorldPosition, out Matrix4x4 matrix)
                => owner.deformationField.TryGetDeformationMatrixTrilinear(restWorldPosition, nodeFrames, out matrix);
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
                matrix = Matrix4x4.identity;

                var binding = owner.GetBinding(restWorldPosition.ToDVector3(),
                                               owner.boundSelectionMode,
                                               owner.boundWeightMode,
                                               owner.boundK,
                                               owner.boundPower,
                                               owner.boundSigma);

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

                    DVector3 g = owner.nodes[nodeIndex].restPosition;

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
        }
    }
}
#endif
