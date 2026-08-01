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
    /// Holds the angle between two structure links meeting at a node, so a junction keeps its shape
    /// rather than folding flat or splaying open as the piece deforms.
    ///
    /// Two rows per constraint, a cosine and a sine of the angle error rather than the angle itself.
    /// That is what makes it well behaved all the way round: an angle residual jumps when it wraps,
    /// and the pair does not.
    ///
    /// Structure graphs only - the constraints are built from the structure's links, so a navmesh
    /// graph has none and the term contributes nothing there.
    ///
    /// **This is the first of the three structure-only energies in row order**, ahead of both
    /// terminal blocks. The layout struct lists it last, which is a field ordering that feeds only a
    /// sum; the residual evaluator and the Jacobian both emit it first, and they are what places
    /// rows.
    /// </summary>
    [Serializable]
    [PolymorphicName("Link Angle (Structure)")]
    public class EDLinkAngleTerm : EDResidualTerm
    {
        public override string name => "linkAngle";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new LinkAngleInstance(this, deformation);

        public class LinkAngleInstance : Instance
        {
            public LinkAngleInstance(EDLinkAngleTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
                => 2 * ((deformation.linkAngleConstraints != null) ? (deformation.linkAngleConstraints.Count) : (0));

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 2); i++)
                {
                    deformation.EvaluateSingleLinkAngleResidual(state, i, residualWeight, out double cosineResidual, out double sineResidual);

                    residual[row++] = cosineResidual;
                    residual[row++] = sineResidual;
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 2); i++)
                    row = deformation.FillLinkAngleJacobianBlock(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }
}
#endif
