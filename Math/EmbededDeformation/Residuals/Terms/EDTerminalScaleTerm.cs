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
                    residual[row++] = EvaluateRow(state, i, residualWeight);
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                var stateView = new EDStateView(state);

                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    row = FillJacobianBlock(stateView, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }

            /// <summary>
            /// How far the terminal's deformed right axis is from the width its connector asked for.
            /// Signed, unlike the one-sided navigation energies - a connector that is too narrow and
            /// one that is too wide both fail to join, so there is no permitted side.
            /// </summary>
            private double EvaluateRow(EDStateView state, int terminalIndex, double wTerminalScale)
            {
                EDTerminalConstraint terminal = deformation.terminalConstraints[terminalIndex];

                EDNode node = deformation.nodes[terminal.nodeIndex];

                DVector3 transformedRight = state.TransformVector(terminal.nodeIndex, node.restRight);

                double currentScale = transformedRight.magnitude;
                double targetScale = Math.Max(terminal.targetScale, 1e-8);

                return wTerminalScale * (currentScale - targetScale);
            }

            /// <summary>
            /// One row, analytically. The residual is the length of A times the rest right axis, so
            /// its derivative with respect to each matrix entry is the corresponding component of
            /// that axis, scaled by the normalized current one - no finite differences needed, which
            /// is what separates this from its orientation companion.
            ///
            /// A node whose right axis has collapsed leaves the row at zero rather than dividing by
            /// the length: at zero length the derivative genuinely does not exist, and there is no
            /// direction to push it back out along.
            /// </summary>
            private int FillJacobianBlock(EDStateView state, DenseMatrix J, int row, int terminalIndex, double wTerminalScale, ref double jNorm)
            {
                EDTerminalConstraint terminal = deformation.terminalConstraints[terminalIndex];

                int nodeIndex = terminal.nodeIndex;

                EDNode      node = deformation.nodes[nodeIndex];
                DVector3    restRight = node.restRight;
                DVector3    currentRight = state.TransformVector(nodeIndex, restRight);
                double      currentScale = currentRight.magnitude;

                if (currentScale < 1e-12)
                    return row + 1;

                int parameterBase = EDStateView.ParamBase(nodeIndex);

                for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                {
                    double normalizedCurrentComponent = currentRight.GetComponent(outputAxis) / currentScale;

                    for (int inputAxis = 0; inputAxis < 3; inputAxis++)
                    {
                        int col = parameterBase + outputAxis * 4 + inputAxis;

                        double value = wTerminalScale * normalizedCurrentComponent * restRight.GetComponent(inputAxis);

                        J[row, col] = value;

                        jNorm += value * value;
                    }
                }

                return row + 1;
            }
        }
#endif
    }
}
#endif
