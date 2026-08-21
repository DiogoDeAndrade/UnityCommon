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

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new SegmentLengthInstance(this, deformation);

        public class SegmentLengthInstance : Instance
        {
            private readonly EDSegmentLengthTerm segmentLengthTerm;

            public SegmentLengthInstance(EDSegmentLengthTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                segmentLengthTerm = term;
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
                    residual[row++] = EvaluateRow(state, i, residualWeight);
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    row = FillJacobianBlock(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }

            /// <summary>
            /// How far one segment has been crushed below the floor, weighted. Zero when it is
            /// longer than the floor - stretching is not a problem.
            ///
            /// Where the endpoints come from is the one branch here: the deformed nodes when the
            /// graph came from the structure and the segment knows them, since there the nodes *are*
            /// the structure, and the vertex bindings otherwise.
            /// </summary>
            private double EvaluateRow(EDStateView state, int segmentIndex, double wSegmentLength)
            {
                NavEDSegments seg = deformation.structure[segmentIndex];

                DVector3 p1;
                DVector3 p2;

                if ((deformation.deformationGraphSource == DeformationGraphSource.StructureOnly) &&
                    (seg.node1 >= 0) &&
                    (seg.node2 >= 0))
                {
                    p1 = state.DeformNodePosition(seg.node1, deformation.nodes[seg.node1].restPosition);
                    p2 = state.DeformNodePosition(seg.node2, deformation.nodes[seg.node2].restPosition);
                }
                else
                {
                    p1 = deformation.DeformVertex(seg.p1, seg.bind1, state);
                    p2 = deformation.DeformVertex(seg.p2, seg.bind2, state);
                }

                double originalLength = (seg.p2 - seg.p1).magnitude;

                if (originalLength < 1e-8)
                    return 0.0;

                double currentLength = (p2 - p1).magnitude;

                double minRatio = Math.Clamp(segmentLengthTerm.minRatio, 0.0, 1.0);

                double minAllowedLength = minRatio * originalLength;

                double shrinkage = Math.Max(0.0, minAllowedLength - currentLength);

                return wSegmentLength * shrinkage / originalLength;
            }

            /// <summary>
            /// One row by finite differences. A segment's length runs through the bindings or the
            /// node positions and then a square root, so there is no analytic derivative worth
            /// keeping in step with the branch above.
            ///
            /// The early-out when the segment is not being crushed skips the whole column loop and
            /// leaves the row at zero, which is correct rather than approximate: the residual is
            /// identically zero in a neighbourhood of any configuration above the floor.
            /// </summary>
            private int FillJacobianBlock(EDState state, DenseMatrix J, int row, int segmentIndex, double wSegmentLength, ref double jNorm)
            {
                var baseView = new EDStateView(state);

                double r0 = EvaluateRow(baseView, segmentIndex, wSegmentLength);

                if (Math.Abs(r0) <= 1e-12)
                    return row + 1;

                for (int col = 0; col < state.Count; col++)
                {
                    double original = state.Get(col);
                    double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                    var modifiedState = new EDStateView(state, col, eps);

                    double r1 = EvaluateRow(modifiedState, segmentIndex, wSegmentLength);
                    double v = (r1 - r0) / eps;

                    J[row, col] = v;
                    jNorm += v * v;
                }

                return row + 1;
            }
        }
#endif
    }
}
#endif
