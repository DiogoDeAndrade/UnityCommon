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
                    row = deformation.FillRotationJacobianBlock(stateView, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }
}
#endif
