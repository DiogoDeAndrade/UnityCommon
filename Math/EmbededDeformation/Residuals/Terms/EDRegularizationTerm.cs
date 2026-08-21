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
    /// Asks neighbouring nodes to agree about where each other should end up: node j predicts node
    /// k's deformed position by carrying the rest offset through its own transform, and the residual
    /// is how far that prediction lands from where k actually went. It is what stops the graph
    /// tearing, and it is the term that propagates a handle's influence out across nodes that have
    /// no constraint of their own.
    ///
    /// Three rows per *directed* edge, so a neighbour pair is measured twice, once from each end.
    /// Both graph sources use this unchanged - the energy only reads rest positions, translations
    /// and neighbour lists, none of which differ between them.
    /// </summary>
    [Serializable]
    [PolymorphicName("Regularization")]
    public class EDRegularizationTerm : EDResidualTerm
    {
        public override string name => "regularization";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new RegularizationInstance(this, deformation);

        public class RegularizationInstance : Instance
        {
            public RegularizationInstance(EDRegularizationTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
            {
                int directedEdgeCount = 0;

                for (int i = 0; i < deformation.nodes.Count; i++)
                    directedEdgeCount += deformation.nodes[i].neighbors.Count;

                return 3 * directedEdgeCount;
            }

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = residualWeight;
                int row = rowOffset;

                for (int j = 0; j < deformation.nodes.Count; j++)
                {
                    EDNode nodeJ = deformation.nodes[j];

                    DVector3 gj = nodeJ.restPosition;
                    DVector3 tj = state.GetTranslation(j);

                    foreach (int k in nodeJ.neighbors)
                    {
                        EDNode nodeK = deformation.nodes[k];

                        DVector3 gk = nodeK.restPosition;
                        DVector3 tk = state.GetTranslation(k);

                        DVector3 diff = gk - gj;
                        DVector3 rotatedDiff = state.TransformVector(j, diff);

                        DVector3 r = rotatedDiff + gj + tj - (gk + tk);

                        residual[row++] = w * r.x;
                        residual[row++] = w * r.y;
                        residual[row++] = w * r.z;
                    }
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int j = 0; j < deformation.nodes.Count; j++)
                {
                    foreach (int k in deformation.nodes[j].neighbors)
                        row = FillJacobianBlock(jacobian, row, j, k, residualWeight, ref jacobianNormSq);
                }
            }

            /// <summary>
            /// The three rows for one directed edge, analytically.
            ///
            /// It takes no state, which is worth noticing rather than tidying past: the residual is
            /// affine in the parameters - node j's linear part acting on a *rest* offset, plus j's
            /// translation, minus k's - so every derivative here is a rest quantity and the current
            /// state cannot enter. That is also why this term is cheap while slope and clearance
            /// have to be differenced.
            /// </summary>
            private int FillJacobianBlock(DenseMatrix J, int row, int nodeJ, int nodeK, double wReg, ref double jNormRunningTotalSq)
            {
                int pj = EDStateView.ParamBase(nodeJ);
                int pk = EDStateView.ParamBase(nodeK);

                DVector3 gj = deformation.nodes[nodeJ].restPosition;
                DVector3 gk = deformation.nodes[nodeK].restPosition;
                DVector3 d = gk - gj;

                double dx = d.x;
                double dy = d.y;
                double dz = d.z;

                // r.x
                J[row, pj + 0] = wReg * dx;   // m00
                J[row, pj + 1] = wReg * dy;   // m01
                J[row, pj + 2] = wReg * dz;   // m02
                J[row, pj + 3] = wReg * 1.0;  // m03 / tx_j
                J[row, pk + 3] = wReg * -1.0; // tx_k
                row++;

                // r.y
                J[row, pj + 4] = wReg * dx;   // m10
                J[row, pj + 5] = wReg * dy;   // m11
                J[row, pj + 6] = wReg * dz;   // m12
                J[row, pj + 7] = wReg * 1.0;  // m13 / ty_j
                J[row, pk + 7] = wReg * -1.0; // ty_k
                row++;

                // r.z
                J[row, pj + 8] = wReg * dx;   // m20
                J[row, pj + 9] = wReg * dy;   // m21
                J[row, pj + 10] = wReg * dz;   // m22
                J[row, pj + 11] = wReg * 1.0;  // m23 / tz_j
                J[row, pk + 11] = wReg * -1.0; // tz_k
                row++;

                double d2 = dx * dx + dy * dy + dz * dz;
                jNormRunningTotalSq += 3.0 * wReg * wReg * (d2 + 2.0);

                return row;
            }
        }
#endif
    }
}
#endif
