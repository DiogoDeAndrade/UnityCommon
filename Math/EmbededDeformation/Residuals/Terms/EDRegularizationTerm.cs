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
                var stateView = new EDStateView(state);

                int row = rowOffset;

                for (int j = 0; j < deformation.nodes.Count; j++)
                {
                    foreach (int k in deformation.nodes[j].neighbors)
                        row = deformation.FillRegularizationJacobianBlock(stateView, jacobian, row, j, k, residualWeight, ref jacobianNormSq);
                }
            }
        }
#endif
    }
}
#endif
