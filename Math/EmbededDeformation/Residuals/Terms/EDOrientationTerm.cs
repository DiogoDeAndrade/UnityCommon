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
    /// Holds each piece of the structure pointing the way it originally pointed. Three rows per
    /// thing measured, one per axis of the orientation error, so unlike slope this constrains a
    /// direction rather than a scalar and cannot be satisfied by moving along it.
    ///
    /// Split the same way slope is, and for the same reason: navmesh graphs measure structure
    /// segments, structure graphs measure nodes, and nothing else about the two differs. Built by
    /// finite differences one block at a time.
    ///
    /// Note there is no early-out when the residual is already near zero, which slope does have.
    /// That asymmetry is in the blocks being replaced and is preserved here rather than tidied,
    /// since a skipped block leaves Jacobian rows at zero and that is a numerical difference.
    /// </summary>
    [Serializable]
    public abstract class EDOrientationTerm : EDResidualTerm
    {
        public override string name => "orientation";

#if MATH_NET_AVAILABLE
        public abstract class OrientationInstance : Instance
        {
            protected OrientationInstance(EDOrientationTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            /// <summary>
            /// How many things this form measures - segments or nodes. Three rows each.
            /// </summary>
            protected abstract int domainCount { get; }

            protected abstract DVector3 EvaluateItem(EDStateView state, int index, double weight);

            protected abstract int FillItem(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq);

            protected override int ComputeRowCount() => (deformation.isNavConfigured) ? (3 * domainCount) : (0);

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 3); i++)
                {
                    DVector3 r = EvaluateItem(state, i, residualWeight);

                    residual[row++] = r.x;
                    residual[row++] = r.y;
                    residual[row++] = r.z;
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 3); i++)
                    row = FillItem(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }

    /// <summary>
    /// Orientation measured per structure segment.
    /// </summary>
    [Serializable]
    [PolymorphicName("Orientation (NavMesh)")]
    public class EDOrientationTermNavMesh : EDOrientationTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new OrientationNavMeshInstance(this, deformation);

        public class OrientationNavMeshInstance : OrientationInstance
        {
            public OrientationNavMeshInstance(EDOrientationTermNavMesh term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int domainCount => (deformation.structure != null) ? (deformation.structure.Count) : (0);

            protected override DVector3 EvaluateItem(EDStateView state, int index, double weight)
                => deformation.EvaluateSingleOrientationResidual(state, index, weight);

            protected override int FillItem(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
                => deformation.FillOrientationJacobianBlock(state, jacobian, row, index, weight, ref jacobianNormSq);
        }
#endif
    }

    /// <summary>
    /// Orientation measured per graph node, from the node's own frame.
    /// </summary>
    [Serializable]
    [PolymorphicName("Orientation (Structure)")]
    public class EDOrientationTermStructure : EDOrientationTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new OrientationStructureInstance(this, deformation);

        public class OrientationStructureInstance : OrientationInstance
        {
            public OrientationStructureInstance(EDOrientationTermStructure term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int domainCount => deformation.nodes.Count;

            protected override DVector3 EvaluateItem(EDStateView state, int index, double weight)
                => deformation.EvaluateSingleNodeOrientationResidualStructure(state, index, weight);

            protected override int FillItem(EDState state, DenseMatrix jacobian, int row, int index, double weight, ref double jacobianNormSq)
                => deformation.FillOrientationJacobianBlockStructure(state, jacobian, row, index, weight, ref jacobianNormSq);
        }
#endif
    }
}
#endif
