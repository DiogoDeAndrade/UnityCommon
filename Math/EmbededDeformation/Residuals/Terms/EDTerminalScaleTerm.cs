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
    /// Holds a terminal at the width its connector demands. One row per terminal.
    ///
    /// The companion to the terminal orientation energy, and the last of the thirteen. Together they
    /// pin how a terminal is turned and how wide it is; position comes from the handle constraint.
    ///
    /// This is the energy the structure rotation term defers to. A node held by a terminal has its
    /// right axis scaled by whatever this asks for, so the rotation energy stops demanding that axis
    /// keep unit length - the two would otherwise pull against each other over the same parameter.
    /// See EDRotationTermStructure, which zeroes that row rather than dropping it.
    /// </summary>
    [Serializable]
    [PolymorphicName("Terminal Scale (Structure)")]
    public class EDTerminalScaleTerm : EDResidualTerm
    {
        public override string name => "terminalScale";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new TerminalScaleInstance(this, deformation);

        public class TerminalScaleInstance : Instance
        {
            public TerminalScaleInstance(EDTerminalScaleTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
                => (deformation.terminalConstraints != null) ? (deformation.terminalConstraints.Count) : (0);

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    residual[row++] = deformation.EvaluateSingleTerminalScaleResidual(state, i, residualWeight);
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                // Takes a view rather than the state itself, as the legacy call site does.
                var stateView = new EDStateView(state);

                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    row = deformation.FillTerminalScaleJacobianBlock(stateView, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }
}
#endif
