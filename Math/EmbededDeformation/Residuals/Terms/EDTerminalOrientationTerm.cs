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
    /// Holds a terminal node facing the way its connector demands. Three rows per terminal, one per
    /// axis of the orientation error.
    ///
    /// This is what makes a deformed piece still fit its neighbours: a terminal is where two map
    /// pieces meet, so its orientation is not free even though everything around it is. It is
    /// weighted far above the shape energies for the same reason - a piece that no longer joins is
    /// not a worse solution, it is not a solution.
    ///
    /// Structure graphs only, and unlike the navigation energies its rows are gated on the weight
    /// alone rather than on the navmesh data, matching the block it replaces. The terminals come
    /// from the structure, so they exist whether or not a navmesh was ever supplied.
    /// </summary>
    [Serializable]
    [PolymorphicName("Terminal Orientation (Structure)")]
    public class EDTerminalOrientationTerm : EDResidualTerm
    {
        public override string name => "terminalOrientation";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new TerminalOrientationInstance(this, deformation);

        public class TerminalOrientationInstance : Instance
        {
            public TerminalOrientationInstance(EDTerminalOrientationTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
                => 3 * ((deformation.terminalConstraints != null) ? (deformation.terminalConstraints.Count) : (0));

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 3); i++)
                {
                    DVector3 r = deformation.EvaluateSingleTerminalOrientationResidual(state, i, residualWeight);

                    residual[row++] = r.x;
                    residual[row++] = r.y;
                    residual[row++] = r.z;
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 3); i++)
                    row = deformation.FillTerminalOrientationJacobianBlock(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }
}
#endif
