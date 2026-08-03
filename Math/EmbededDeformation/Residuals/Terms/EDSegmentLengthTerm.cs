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
    /// One class for both graph sources, unlike slope and orientation. Those genuinely measure
    /// different domains - segments against nodes - whereas this always measures segments, and the
    /// only question is where a segment's endpoints are read from. That is something the term can
    /// ask the deformation rather than something the caller has to pick a subclass for: deformed
    /// node positions when the graph came from the structure and the segment knows its nodes, the
    /// vertex bindings otherwise.
    ///
    /// It was two classes because it was two functions, and they differed by more than the branch:
    /// one computed w * (loss / length) and the other (w * loss) / length. Equal in arithmetic,
    /// unequal in the last bits, so collapsing them meant choosing. This keeps the second, which is
    /// what the structure configurations were already using.
    /// </summary>
    [Serializable]
    [PolymorphicName("Segment Length")]
    public class EDSegmentLengthTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("A segment may shrink to this fraction of its rest length before the term objects.")]
        private float minRatio = 0.85f;

        public override string name => "segmentLength";

        public override void ApplyRuntimeParameters(EmbededDeformation deformation)
        {
            deformation.segmentMinRatio = minRatio;
        }

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new SegmentLengthInstance(this, deformation);

        public class SegmentLengthInstance : Instance
        {
            public SegmentLengthInstance(EDSegmentLengthTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
            {
                if (!deformation.isNavConfigured) return 0;

                return (deformation.structure != null) ? (deformation.structure.Count) : (0);
            }

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    residual[row++] = deformation.EvaluateSingleSegmentLengthResidual(state, i, residualWeight);
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    row = deformation.FillSegmentLengthJacobianBlock(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }
}
#endif
