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
    /// Stops a structure segment being crushed shorter than a fraction of its rest length. One row
    /// per segment, penalising only shrinkage - a corridor that stretches is not a problem.
    ///
    /// Unlike slope and orientation, both forms measure the same domain and produce the same number
    /// of rows. What differs is how the two ends of a segment are located: the structure form
    /// prefers the deformed node positions when the segment knows its nodes and the graph came from
    /// the structure, and falls back to the bindings otherwise, which is what the navmesh form
    /// always does.
    ///
    /// So in a navmesh configuration the two forms take the same branch and differ only in the order
    /// they apply the weight and the division - w * (loss / length) against (w * loss) / length. That
    /// is not an arithmetic identity in floating point, so the parity check can still tell them
    /// apart, but only on segments actually in violation. Where nothing is being crushed both are
    /// exactly zero and the choice is unobservable.
    /// </summary>
    [Serializable]
    public abstract class EDSegmentLengthTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("A segment may shrink to this fraction of its rest length before the term objects.")]
        private float minRatio = 0.85f;

        public override string name => "segmentLength";

        public override void ApplyRuntimeParameters(EmbededDeformation deformation)
        {
            deformation.segmentMinRatio = minRatio;
        }

#if MATH_NET_AVAILABLE
        public abstract class SegmentLengthInstance : Instance
        {
            protected SegmentLengthInstance(EDSegmentLengthTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected abstract double EvaluateRow(EDStateView state, int segmentIndex, double weight);

            protected abstract int FillRow(EDState state, DenseMatrix jacobian, int row, int segmentIndex, double weight, ref double jacobianNormSq);

            protected override int ComputeRowCount()
            {
                if (!deformation.isNavConfigured) return 0;

                return (deformation.structure != null) ? (deformation.structure.Count) : (0);
            }

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
    /// Segment length with both ends located through the vertex bindings.
    /// </summary>
    [Serializable]
    [PolymorphicName("Segment Length (NavMesh)")]
    public class EDSegmentLengthTermNavMesh : EDSegmentLengthTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new SegmentLengthNavMeshInstance(this, deformation);

        public class SegmentLengthNavMeshInstance : SegmentLengthInstance
        {
            public SegmentLengthNavMeshInstance(EDSegmentLengthTermNavMesh term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override double EvaluateRow(EDStateView state, int segmentIndex, double weight)
                => deformation.EvaluateSingleSegmentLengthResidual(state, segmentIndex, weight);

            protected override int FillRow(EDState state, DenseMatrix jacobian, int row, int segmentIndex, double weight, ref double jacobianNormSq)
                => deformation.FillSegmentLengthJacobianBlock(state, jacobian, row, segmentIndex, weight, ref jacobianNormSq);
        }
#endif
    }

    /// <summary>
    /// Segment length with both ends taken from the deformed nodes where the segment knows them,
    /// which in a structure graph is the more direct measurement - the nodes are the structure.
    /// </summary>
    [Serializable]
    [PolymorphicName("Segment Length (Structure)")]
    public class EDSegmentLengthTermStructure : EDSegmentLengthTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new SegmentLengthStructureInstance(this, deformation);

        public class SegmentLengthStructureInstance : SegmentLengthInstance
        {
            public SegmentLengthStructureInstance(EDSegmentLengthTermStructure term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override double EvaluateRow(EDStateView state, int segmentIndex, double weight)
                => deformation.EvaluateSingleSegmentLengthResidualStructure(state, segmentIndex, weight);

            protected override int FillRow(EDState state, DenseMatrix jacobian, int row, int segmentIndex, double weight, ref double jacobianNormSq)
                => deformation.FillSegmentLengthJacobianBlockStructure(state, jacobian, row, segmentIndex, weight, ref jacobianNormSq);
        }
#endif
    }
}
#endif
