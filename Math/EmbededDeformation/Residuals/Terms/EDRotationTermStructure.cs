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
    /// The structure-graph form of the rotation energy. Six rows per node, as in the navmesh form,
    /// but measured against the node's rest frame rather than the raw matrix axes: a structure node
    /// carries a meaningful right/up/forward built from the segment it sits on, and it is that frame
    /// which has to stay orthonormal.
    ///
    /// One row is conditional. A node held by a terminal constraint has its scale along the right
    /// axis dictated by the terminal, so asking that axis to keep unit length here would fight it.
    /// The row is still allocated and left at zero rather than dropped, which is what keeps the row
    /// count a plain 6 per node and independent of which nodes happen to be terminals.
    /// </summary>
    [Serializable]
    [PolymorphicName("Rotation (Structure)")]
    public class EDRotationTermStructure : EDResidualTerm
    {
        public override string name => "rotation";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new RotationStructureInstance(this, deformation);

        public class RotationStructureInstance : Instance
        {
            public RotationStructureInstance(EDRotationTermStructure term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount() => 6 * deformation.nodes.Count;

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = residualWeight;
                int row = rowOffset;

                for (int i = 0; i < deformation.nodes.Count; i++)
                {
                    EDNode node = deformation.nodes[i];

                    DVector3 right = state.TransformVector(i, node.restRight);
                    DVector3 up = state.TransformVector(i, node.restUp);
                    DVector3 forward = state.TransformVector(i, node.restForward);

                    residual[row++] = w * DVector3.Dot(right, up);
                    residual[row++] = w * DVector3.Dot(right, forward);
                    residual[row++] = w * DVector3.Dot(up, forward);

                    bool allowRightScale = deformation.HasTerminalScaleConstraint(i);

                    residual[row++] = (allowRightScale) ? (0.0) : (w * (DVector3.Dot(right, right) - 1.0));
                    residual[row++] = w * (DVector3.Dot(up, up) - 1.0);
                    residual[row++] = w * (DVector3.Dot(forward, forward) - 1.0);
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                var stateView = new EDStateView(state);

                int row = rowOffset;

                for (int i = 0; i < deformation.nodes.Count; i++)
                {
                    bool allowRightScale = deformation.HasTerminalScaleConstraint(i);

                    row = FillJacobianBlock(stateView, jacobian, row, i, residualWeight, allowRightScale, ref jacobianNormSq);
                }
            }

            /// <summary>
            /// The six rows for one node, analytically, measured against the node's rest frame
            /// rather than the raw matrix axes - so unlike the navmesh form the rest vector appears
            /// in every derivative.
            ///
            /// The disabled right-axis length row advances the row counter and writes nothing. The
            /// Jacobian arrives zero-filled, so not writing is writing zeros, and it is what keeps
            /// the block a plain six rows whether or not this node is a terminal.
            /// </summary>
            private int FillJacobianBlock(EDStateView state, DenseMatrix J, int row, int nodeIndex, double wRot, bool allowRightScale, ref double jNorm)
            {
                static int FillFrameDotJacobianRow(DenseMatrix J, int row, int parameterBase, DVector3 transformedA, DVector3 restA, DVector3 transformedB, DVector3 restB, double weight, ref double jNorm)
                {
                    for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                    {
                        double a = transformedA.GetComponent(outputAxis);
                        double b = transformedB.GetComponent(outputAxis);

                        for (int inputAxis = 0; inputAxis < 3; inputAxis++)
                        {
                            int col = parameterBase + outputAxis * 4 + inputAxis;

                            double value = weight * (b * restA.GetComponent(inputAxis) + a * restB.GetComponent(inputAxis));

                            J[row, col] = value;
                            jNorm += value * value;
                        }
                    }

                    return row + 1;
                }

                static int FillFrameLengthJacobianRow(DenseMatrix J, int row, int parameterBase, DVector3 transformed, DVector3 rest, double weight, bool enabled, ref double jNorm)
                {
                    if (!enabled) return row + 1;

                    for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                    {
                        double transformedComponent = transformed.GetComponent(outputAxis);

                        for (int inputAxis = 0; inputAxis < 3; inputAxis++)
                        {
                            int col = parameterBase + outputAxis * 4 + inputAxis;
                            double value = 2.0 * weight * transformedComponent * rest.GetComponent(inputAxis);

                            J[row, col] = value;

                            jNorm += value * value;
                        }
                    }

                    return row + 1;
                }

                EDNode node = deformation.nodes[nodeIndex];

                DVector3 right = state.TransformVector(nodeIndex, node.restRight);
                DVector3 up = state.TransformVector(nodeIndex, node.restUp);
                DVector3 forward = state.TransformVector(nodeIndex, node.restForward);

                int parameterBase = EDStateView.ParamBase(nodeIndex);

                row = FillFrameDotJacobianRow(J, row, parameterBase, right, node.restRight, up, node.restUp, wRot, ref jNorm);
                row = FillFrameDotJacobianRow(J, row, parameterBase, right, node.restRight, forward, node.restForward, wRot, ref jNorm);
                row = FillFrameDotJacobianRow(J, row, parameterBase, up, node.restUp, forward, node.restForward, wRot, ref jNorm);
                row = FillFrameLengthJacobianRow(J, row, parameterBase, right, node.restRight, wRot, !allowRightScale, ref jNorm);
                row = FillFrameLengthJacobianRow(J, row, parameterBase, up, node.restUp, wRot, true, ref jNorm);
                row = FillFrameLengthJacobianRow(J, row, parameterBase, forward, node.restForward, wRot, true, ref jNorm);

                return row;
            }
        }
#endif
    }
}
#endif
