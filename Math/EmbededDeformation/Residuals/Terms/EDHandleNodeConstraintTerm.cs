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
    /// The structure-graph counterpart of the vertex constraint. There is no mesh to bind to here,
    /// so a handle drives the nearest structure node directly rather than a set of navmesh vertices.
    ///
    /// The row count is the one thing about this term that is not a multiple of anything: a centre
    /// handle constrains its single point, but a terminal handle constrains both ends of its bar, so
    /// that its width and its orientation are pinned along with its position. Three rows per
    /// constrained *point*, not per constraint.
    ///
    /// A handle whose rest position finds no node still occupies its rows, left at zero. Dropping
    /// them instead would make the layout depend on how well the handles happen to line up with the
    /// graph, which is exactly the kind of quiet coupling the row count should not have.
    /// </summary>
    [Serializable]
    [PolymorphicName("Handle Constraint (Structure)")]
    public class EDHandleNodeConstraintTerm : EDResidualTerm
    {
        public override string name => "constraint";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new HandleNodeConstraintInstance(this, deformation);

        public class HandleNodeConstraintInstance : Instance
        {
            public HandleNodeConstraintInstance(EDHandleNodeConstraintTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
                => 3 * deformation.GetStructureHandlePositionConstraintPointCount();

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                if (deformation.handleConstraints == null) return;

                double w = residualWeight;
                int row = rowOffset;

                for (int c = 0; c < deformation.handleConstraints.Count; c++)
                {
                    EDHandleConstraint hc = deformation.handleConstraints[c];

                    int handleRowCount = (hc.isTerminal) ? (6) : (3);

                    if (!deformation.TryGetStructureHandleNodeIndex(hc, out int nodeIndex))
                    {
                        row += handleRowCount;
                        continue;
                    }

                    EDNode node = deformation.nodes[nodeIndex];

                    if (hc.isTerminal)
                    {
                        EmbededDeformation.GetStructureHandleBarPoints(hc, out DVector3 restLeft, out DVector3 restRight, out DVector3 targetLeft, out DVector3 targetRight);

                        DVector3 deformedLeft = state.DeformVertex(nodeIndex, restLeft, node.restPosition);
                        DVector3 deformedRight = state.DeformVertex(nodeIndex, restRight, node.restPosition);

                        DVector3 leftError = deformedLeft - targetLeft;
                        DVector3 rightError = deformedRight - targetRight;

                        residual[row++] = w * leftError.x;
                        residual[row++] = w * leftError.y;
                        residual[row++] = w * leftError.z;

                        residual[row++] = w * rightError.x;
                        residual[row++] = w * rightError.y;
                        residual[row++] = w * rightError.z;
                    }
                    else
                    {
                        DVector3 restPosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();
                        DVector3 targetPosition = hc.currentHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();

                        DVector3 deformedPosition = state.DeformVertex(nodeIndex, restPosition, node.restPosition);

                        DVector3 error = deformedPosition - targetPosition;

                        residual[row++] = w * error.x;
                        residual[row++] = w * error.y;
                        residual[row++] = w * error.z;
                    }
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                if (deformation.handleConstraints == null) return;

                int row = rowOffset;

                for (int c = 0; c < deformation.handleConstraints.Count; c++)
                {
                    EDHandleConstraint hc = deformation.handleConstraints[c];

                    int handleRowCount = (hc.isTerminal) ? (6) : (3);

                    if (!deformation.TryGetStructureHandleNodeIndex(hc, out int nodeIndex))
                    {
                        row += handleRowCount;
                        continue;
                    }

                    if (hc.isTerminal)
                    {
                        EmbededDeformation.GetStructureHandleBarPoints(hc, out DVector3 restLeft, out DVector3 restRight, out _, out _);

                        row = deformation.FillNodePositionJacobianBlockStructure(jacobian, row, nodeIndex, restLeft, residualWeight, ref jacobianNormSq);
                        row = deformation.FillNodePositionJacobianBlockStructure(jacobian, row, nodeIndex, restRight, residualWeight, ref jacobianNormSq);
                    }
                    else
                    {
                        DVector3 restPosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();

                        row = deformation.FillNodePositionJacobianBlockStructure(jacobian, row, nodeIndex, restPosition, residualWeight, ref jacobianNormSq);
                    }
                }
            }
        }
#endif
    }
}
#endif
