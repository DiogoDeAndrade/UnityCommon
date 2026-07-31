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

                    row = deformation.FillRotationJacobianBlockStructure(stateView, jacobian, row, i, residualWeight, allowRightScale, ref jacobianNormSq);
                }
            }
        }
#endif
    }
}
#endif
