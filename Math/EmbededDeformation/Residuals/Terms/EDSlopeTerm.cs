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
    /// The limit and the soft band still come from the deformation rather than from this term. They
    /// are supplied by SetNavEDParameters today, and moving them here cannot be checked with the
    /// parity tool - see the note on ApplyRuntimeParameters in EDResidualTerm.
    /// </summary>
    [Serializable]
    public abstract class EDSlopeTerm : EDResidualTerm
    {
        public override string name => "slope";

#if MATH_NET_AVAILABLE
        public abstract class SlopeInstance : Instance
        {
            protected SlopeInstance(EDSlopeTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

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

            protected override double EvaluateRow(EDStateView state, int index, double weight)
                => deformation.EvaluateSingleSlopeResidual(state, index, weight);

            protected override int FillRow(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
                => deformation.FillSlopeJacobianBlock(state, jacobian, row, index, weight, ref jacobianNormSq);
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

            protected override double EvaluateRow(EDStateView state, int index, double weight)
                => deformation.EvaluateSingleNodeSlopeResidualStructure(state, index, weight);

            protected override int FillRow(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
                => deformation.FillSlopeJacobianBlockStructure(state, jacobian, row, index, weight, ref jacobianNormSq);
        }
#endif
    }
}
#endif
