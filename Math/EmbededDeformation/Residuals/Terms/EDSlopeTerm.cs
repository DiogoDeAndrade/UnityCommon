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
    /// Penalises ground that has been tilted past what an agent can walk, softly: zero below the
    /// soft angle, rising to one at the limit and beyond it after that. One row per thing measured.
    ///
    /// The two forms differ only in what "thing measured" means - navmesh graphs measure structure
    /// segments, structure graphs measure nodes - so everything except the domain and the two calls
    /// that depend on it lives here. Like clearance this has no analytic derivative and is built by
    /// finite differences, but one row at a time rather than in parallel, matching the block it
    /// replaces.
    ///
    /// The limit and the soft band belong to the term and are read straight off it. They used to
    /// arrive through the navigation parameters from a field on the component, which meant an
    /// experiment's slope constraint was not part of the energy that expressed it; then they were
    /// pushed onto the deformation before a solve, which put the energy's own number somewhere
    /// anything could read it.
    ///
    /// **EmbededDeformation.maxSlope is a different number that happens to agree**, and this term no
    /// longer writes it. That one is the piece's navigability limit, used by the corridor probe and
    /// the gizmos at build time - before any energy exists to be asked. While this pushed, the
    /// corridor was measured against whatever the previous solve left behind, and against the
    /// default 45 on a piece that had never been solved.
    /// </summary>
    [Serializable]
    public abstract class EDSlopeTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("Ground steeper than this is penalised.")]
        private float maxSlope = 45.0f;

        [SerializeField, Min(0.0f), Tooltip("How far below the limit the penalty starts rising, so the constraint has a soft edge rather than a cliff.")]
        private float softBand = 5.0f;

        public override string name => "slope";

#if MATH_NET_AVAILABLE
        public abstract class SlopeInstance : Instance
        {
            private readonly EDSlopeTerm slopeTerm;

            protected SlopeInstance(EDSlopeTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                slopeTerm = term;
            }

            /// <summary>
            /// Ground steeper than this is penalised, in degrees.
            /// </summary>
            protected float maxSlope => slopeTerm.maxSlope;

            /// <summary>
            /// How far below the limit the penalty starts rising.
            /// </summary>
            protected float softBand => slopeTerm.softBand;

            /// <summary>
            /// How many things this form measures - segments or nodes. Gated on the navigation data
            /// being present for the same reason clearance is: the slope of a deformed surface is
            /// measured against the navmesh, and without it there is nothing to measure.
            /// </summary>
            protected abstract int domainCount { get; }

            protected abstract double EvaluateRow(EDStateView state, int index, double weight);

            protected abstract int FillRow(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq);

            protected override int ComputeRowCount() => (deformation.isNavConfigured) ? (domainCount) : (0);

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    residual[row++] = EvaluateRow(state, i, residualWeight);
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    row = FillRow(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }

    /// <summary>
    /// Slope measured per structure segment, against the navmesh the segment crosses.
    /// </summary>
    [Serializable]
    [PolymorphicName("Slope (NavMesh)")]
    public class EDSlopeTermNavMesh : EDSlopeTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new SlopeNavMeshInstance(this, deformation);

        public class SlopeNavMeshInstance : SlopeInstance
        {
            public SlopeNavMeshInstance(EDSlopeTermNavMesh term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int domainCount => (deformation.structure != null) ? (deformation.structure.Count) : (0);

            /// <summary>
            /// The segment's probe-triangle normal against the piece's up, through the same hinge
            /// the structure form uses. A degenerate frame scores a full penalty rather than zero,
            /// since a segment whose normal cannot be recovered is not a flat one.
            /// </summary>
            protected override double EvaluateRow(EDStateView state, int index, double weight)
            {
                // Normalized hinge:
                //   0 at or below maxSlope - softBand
                //   1 at maxSlope
                //   >1 beyond maxSlope
                // -------------------------------------------------------------
                double hardAngleDeg = maxSlope;
                double softAngleDeg = Math.Max(0.0, maxSlope - softBand);

                double hardAngle = hardAngleDeg * Math.PI / 180.0;
                double softAngle = softAngleDeg * Math.PI / 180.0;

                double hardDot = Math.Cos(hardAngle);
                double softDot = Math.Cos(softAngle);

                double denom = Math.Max(softDot - hardDot, 1e-12);

                Vector3 upNorm = deformation.upVector.normalized;
                Vector3 segNormal = deformation.GetTransformedSegmentSlopeNormal(state, index);

                double penalty;

                if (segNormal.sqrMagnitude < 1e-12f)
                {
                    // Degenerate frame: strongly invalid.
                    penalty = 1.0;
                }
                else
                {
                    segNormal.Normalize();

                    double dp = Vector3.Dot(segNormal, upNorm);
                    dp = Math.Clamp(dp, -1.0, 1.0);

                    penalty = Math.Max(0.0, (softDot - dp) / denom);
                }

                return weight * penalty;
            }

            protected override int FillRow(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
            {
                var baseView = new EDStateView(state);

                // Base residual value for this segment.
                double r0 = EvaluateRow(baseView, index, weight);

                if (r0 <= 1e-12)
                {
                    return row + 1;
                }

                for (int col = 0; col < state.Count; col++)
                {
                    double original = state.Get(col);

                    double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));
                    var modifiedState = new EDStateView(state, col, eps);

                    double r1 = EvaluateRow(modifiedState, index, weight);

                    jacobian[row, col] = (r1 - r0) / eps;

                    jacobianNormSq += jacobian[row, col] * jacobian[row, col];
                }

                return row + 1;
            }
        }
#endif
    }

    /// <summary>
    /// Slope measured per graph node. A structure graph's nodes carry the frame the slope is read
    /// from, so there is no separate segment to measure.
    /// </summary>
    [Serializable]
    [PolymorphicName("Slope (Structure)")]
    public class EDSlopeTermStructure : EDSlopeTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new SlopeStructureInstance(this, deformation);

        public class SlopeStructureInstance : SlopeInstance
        {
            public SlopeStructureInstance(EDSlopeTermStructure term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int domainCount => deformation.nodes.Count;

            /// <summary>
            /// The node's deformed up against the piece's up, through the same hinge the navmesh
            /// form uses.
            ///
            /// Two differences from that form, both preserved rather than reconciled: the dot is
            /// taken in absolute value, so a node flipped upside down reads as flat ground rather
            /// than as a cliff, and a collapsed up scores the full weight directly instead of going
            /// through the hinge.
            /// </summary>
            protected override double EvaluateRow(EDStateView state, int index, double weight)
            {
                double hardAngle = maxSlope * Math.PI / 180.0;

                double softAngle = Math.Max(0.0, maxSlope - softBand) * Math.PI / 180.0;

                double hardDot = Math.Cos(hardAngle);
                double softDot = Math.Cos(softAngle);

                double denom = Math.Max(softDot - hardDot, 1e-12);

                DVector3 currentUp = state.TransformDirection(index, deformation.nodes[index].restUp);

                if (currentUp.sqrMagnitude < 1e-12) return weight;

                double dp = Math.Abs(DVector3.Dot(currentUp, deformation.upVectorD));

                dp = Math.Clamp(dp, 0.0, 1.0);

                double penalty = Math.Max(0.0, (softDot - dp) / denom);

                return weight * penalty;
            }

            protected override int FillRow(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
            {
                var baseView = new EDStateView(state);

                // Base residual value for this node.
                double r0 = EvaluateRow(baseView, index, weight);

                if (r0 <= 1e-12)
                {
                    return row + 1;
                }

                for (int col = 0; col < state.Count; col++)
                {
                    double original = state.Get(col);

                    double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));
                    var modifiedState = new EDStateView(state, col, eps);

                    double r1 = EvaluateRow(modifiedState, index, weight);

                    jacobian[row, col] = (r1 - r0) / eps;

                    jacobianNormSq += jacobian[row, col] * jacobian[row, col];
                }

                return row + 1;
            }
        }
#endif
    }
}
#endif
