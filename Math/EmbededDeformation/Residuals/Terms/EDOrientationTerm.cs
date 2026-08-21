using System;
using UnityEngine;
using UC.DoubleMath;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Holds each piece of the structure pointing the way it originally pointed. Three rows per
    /// thing measured, one per axis of the orientation error, so unlike slope this constrains a
    /// direction rather than a scalar and cannot be satisfied by moving along it.
    ///
    /// Split the same way slope is, and for the same reason: navmesh graphs measure structure
    /// segments, structure graphs measure nodes, and nothing else about the two differs. Built by
    /// finite differences one block at a time.
    ///
    /// Note there is no early-out when the residual is already near zero, which slope does have.
    /// That asymmetry is in the blocks being replaced and is preserved here rather than tidied,
    /// since a skipped block leaves Jacobian rows at zero and that is a numerical difference.
    /// </summary>
    [Serializable]
    public abstract class EDOrientationTerm : EDResidualTerm
    {
        public override string name => "orientation";

#if MATH_NET_AVAILABLE
        public abstract class OrientationInstance : Instance
        {
            protected OrientationInstance(EDOrientationTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            /// <summary>
            /// How many things this form measures - segments or nodes. Three rows each.
            /// </summary>
            protected abstract int domainCount { get; }

            protected abstract DVector3 EvaluateItem(EDStateView state, int index, double weight);

            protected abstract int FillItem(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq);

            protected override int ComputeRowCount() => (deformation.isNavConfigured) ? (3 * domainCount) : (0);

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 3); i++)
                {
                    DVector3 r = EvaluateItem(state, i, residualWeight);

                    residual[row++] = r.x;
                    residual[row++] = r.y;
                    residual[row++] = r.z;
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 3); i++)
                    row = FillItem(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }

    /// <summary>
    /// Orientation measured per structure segment.
    /// </summary>
    [Serializable]
    [PolymorphicName("Orientation (NavMesh)")]
    public class EDOrientationTermNavMesh : EDOrientationTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new OrientationNavMeshInstance(this, deformation);

        public class OrientationNavMeshInstance : OrientationInstance
        {
            public OrientationNavMeshInstance(EDOrientationTermNavMesh term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int domainCount => (deformation.structure != null) ? (deformation.structure.Count) : (0);

            /// <summary>
            /// The segment's probe-triangle normal against the normal it was built with. A
            /// degenerate frame returns the negated target, which is the residual a normal of zero
            /// would give and is what makes the degenerate case the worst score rather than an
            /// accidental zero.
            /// </summary>
            protected override DVector3 EvaluateItem(EDStateView state, int index, double weight)
            {
                Vector3 current = deformation.GetTransformedSegmentSlopeNormal(state, index);

                if (current.sqrMagnitude < 1e-12f)
                {
                    // Degenerate local frame: strongly invalid.
                    Vector3 fallback = deformation.structure[index].normal.ToVector3();
                    if (fallback.sqrMagnitude < 1e-12f)
                        fallback = deformation.upVector.normalized;
                    else
                        fallback.Normalize();

                    return new DVector3(-weight * fallback.x, -weight * fallback.y, -weight * fallback.z);
                }

                current.Normalize();

                Vector3 target = deformation.structure[index].normal.ToVector3().SafeNormalized();

                return new DVector3(weight * (current.x - target.x), weight * (current.y - target.y), weight * (current.z - target.z));
            }

            protected override int FillItem(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
            {
                var baseView = new EDStateView(state);

                DVector3 r0 = EvaluateItem(baseView, index, weight);

                for (int col = 0; col < state.Count; col++)
                {
                    double original = state.Get(col);
                    double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                    var modifiedState = new EDStateView(state, col, eps);

                    DVector3 r1 = EvaluateItem(modifiedState, index, weight);

                    double jx = (r1.x - r0.x) / eps;
                    double jy = (r1.y - r0.y) / eps;
                    double jz = (r1.z - r0.z) / eps;

                    jacobian[row + 0, col] = jx;
                    jacobian[row + 1, col] = jy;
                    jacobian[row + 2, col] = jz;

                    jacobianNormSq += jx * jx + jy * jy + jz * jz;
                }

                return row + 3;
            }
        }
#endif
    }

    /// <summary>
    /// Orientation measured per graph node, from the node's own frame.
    /// </summary>
    [Serializable]
    [PolymorphicName("Orientation (Structure)")]
    public class EDOrientationTermStructure : EDOrientationTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new OrientationStructureInstance(this, deformation);

        public class OrientationStructureInstance : OrientationInstance
        {
            public OrientationStructureInstance(EDOrientationTermStructure term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int domainCount => deformation.nodes.Count;

            /// <summary>
            /// The node's deformed up against its rest up. Unlike the structure slope form this
            /// does not take an absolute value anywhere, so it does hold the node the right way up -
            /// which is why this is the term that word belongs to, and the structure bend term is
            /// not.
            /// </summary>
            protected override DVector3 EvaluateItem(EDStateView state, int index, double weight)
            {
                DVector3 currentUp = state.TransformDirection(index, deformation.nodes[index].restUp);

                DVector3 restUp = deformation.nodes[index].restUp.normalized;

                if (currentUp.sqrMagnitude < 1e-12f)
                    return -weight * restUp;

                return weight * (currentUp - restUp);
            }

            protected override int FillItem(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
            {
                var baseView = new EDStateView(state);

                DVector3 r0 = EvaluateItem(baseView, index, weight);

                for (int col = 0; col < state.Count; col++)
                {
                    double original = state.Get(col);
                    double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                    var modifiedState = new EDStateView(state, col, eps);

                    DVector3 r1 = EvaluateItem(modifiedState, index, weight);

                    double jx = (r1.x - r0.x) / eps;
                    double jy = (r1.y - r0.y) / eps;
                    double jz = (r1.z - r0.z) / eps;

                    jacobian[row + 0, col] = jx;
                    jacobian[row + 1, col] = jy;
                    jacobian[row + 2, col] = jz;

                    jacobianNormSq += jx * jx + jy * jy + jz * jz;
                }

                return row + 3;
            }
        }
#endif
    }
}
#endif
