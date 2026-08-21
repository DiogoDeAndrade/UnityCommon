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
    /// Keeps each node's linear part a rotation: three rows asking its axes to stay perpendicular
    /// and three asking them to keep unit length. Without it the solver is free to shear and scale
    /// the geometry to satisfy everything else cheaply.
    ///
    /// Six rows per node. This is the navmesh-graph form, which measures the raw matrix axes; the
    /// structure form measures the node's rest frame instead and permits scaling along a terminal's
    /// right axis.
    /// </summary>
    [Serializable]
    [PolymorphicName("Rotation (NavMesh)")]
    public class EDRotationTermNavMesh : EDResidualTerm
    {
        public override string name => "rotation";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new RotationInstance(this, deformation);

        public class RotationInstance : Instance
        {
            public RotationInstance(EDRotationTermNavMesh term, EmbededDeformation deformation)
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
                    var axisX = state.GetAxisX(i);
                    var axisY = state.GetAxisY(i);
                    var axisZ = state.GetAxisZ(i);

                    residual[row++] = w * DVector3.Dot(axisX, axisY);
                    residual[row++] = w * DVector3.Dot(axisX, axisZ);
                    residual[row++] = w * DVector3.Dot(axisY, axisZ);

                    residual[row++] = w * (DVector3.Dot(axisX, axisX) - 1.0);
                    residual[row++] = w * (DVector3.Dot(axisY, axisY) - 1.0);
                    residual[row++] = w * (DVector3.Dot(axisZ, axisZ) - 1.0);
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                var stateView = new EDStateView(state);

                int row = rowOffset;

                for (int i = 0; i < deformation.nodes.Count; i++)
                    row = FillJacobianBlock(stateView, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }

            /// <summary>
            /// The six rows for one node, analytically. Three dot products and three squared
            /// lengths, all quadratic in the matrix entries, so the derivatives are the other axis
            /// and twice the axis respectively.
            ///
            /// The norm contribution is added in closed form at the end rather than accumulated
            /// entry by entry, which is not the same sum in floating point - it is the sum the
            /// baselines were captured against.
            /// </summary>
            private int FillJacobianBlock(EDStateView state, DenseMatrix J, int row, int nodeIndex, double wRot, ref double jNormRunningTotalSq)
            {
                int p = EDStateView.ParamBase(nodeIndex);
                var aX = state.GetAxisX(nodeIndex);
                var aY = state.GetAxisY(nodeIndex);
                var aZ = state.GetAxisZ(nodeIndex);

                // r0 = X dot Y
                J[row, p + 0] = wRot * aY.x;
                J[row, p + 4] = wRot * aY.y;
                J[row, p + 8] = wRot * aY.z;

                J[row, p + 1] = wRot * aX.x;
                J[row, p + 5] = wRot * aX.y;
                J[row, p + 9] = wRot * aX.z;
                row++;

                // r1 = X dot Z
                J[row, p + 0] = wRot * aZ.x;
                J[row, p + 4] = wRot * aZ.y;
                J[row, p + 8] = wRot * aZ.z;

                J[row, p + 2] = wRot * aX.x;
                J[row, p + 6] = wRot * aX.y;
                J[row, p + 10] = wRot * aX.z;
                row++;

                // r2 = Y dot Z
                J[row, p + 1] = wRot * aZ.x;
                J[row, p + 5] = wRot * aZ.y;
                J[row, p + 9] = wRot * aZ.z;

                J[row, p + 2] = wRot * aY.x;
                J[row, p + 6] = wRot * aY.y;
                J[row, p + 10] = wRot * aY.z;
                row++;

                // r3 = X dot X - 1
                J[row, p + 0] = wRot * 2.0 * aX.x;
                J[row, p + 4] = wRot * 2.0 * aX.y;
                J[row, p + 8] = wRot * 2.0 * aX.z;
                row++;

                // r4 = Y dot Y - 1
                J[row, p + 1] = wRot * 2.0 * aY.x;
                J[row, p + 5] = wRot * 2.0 * aY.y;
                J[row, p + 9] = wRot * 2.0 * aY.z;
                row++;

                // r5 = Z dot Z - 1
                J[row, p + 2] = wRot * 2.0 * aZ.x;
                J[row, p + 6] = wRot * 2.0 * aZ.y;
                J[row, p + 10] = wRot * 2.0 * aZ.z;
                row++;

                double ax2 = aX.x * aX.x + aX.y * aX.y + aX.z * aX.z;
                double ay2 = aY.x * aY.x + aY.y * aY.y + aY.z * aY.z;
                double az2 = aZ.x * aZ.x + aZ.y * aZ.y + aZ.z * aZ.z;

                jNormRunningTotalSq += 6.0 * wRot * wRot * (ax2 + ay2 + az2);

                return row;
            }
        }
#endif
    }
}
#endif
