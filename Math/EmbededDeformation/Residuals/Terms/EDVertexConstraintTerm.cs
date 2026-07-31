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
    /// Pulls constrained navmesh vertices onto their target positions - three rows each, the
    /// deformed vertex minus where the handle says it should be. This is the term that actually
    /// drives the deformation; every other energy only says what the graph may do on the way.
    ///
    /// The vertex is deformed through its binding, so a single constrained vertex writes into every
    /// node it is bound to. That is why the block accumulates into the Jacobian rather than
    /// assigning: two bindings can name the same node.
    ///
    /// A constraint naming a vertex outside the rest mesh contributes three zero rows rather than
    /// being dropped, which keeps the row count a plain three per constraint.
    /// </summary>
    [Serializable]
    [PolymorphicName("Vertex Constraint (NavMesh)")]
    public class EDVertexConstraintTerm : EDResidualTerm
    {
        public override string name => "constraint";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new VertexConstraintInstance(this, deformation);

        public class VertexConstraintInstance : Instance
        {
            public VertexConstraintInstance(EDVertexConstraintTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
                => 3 * ((deformation.vertexConstraints != null) ? (deformation.vertexConstraints.Count) : (0));

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = residualWeight;
                int row = rowOffset;

                for (int c = 0; c < deformation.vertexConstraints.Count; c++)
                {
                    EDVertexConstraint vc = deformation.vertexConstraints[c];

                    if ((vc.vertexIndex < 0) || (vc.vertexIndex >= deformation.restVertices.Length))
                    {
                        residual[row++] = 0.0;
                        residual[row++] = 0.0;
                        residual[row++] = 0.0;
                        continue;
                    }

                    DVector3 deformed = deformation.DeformVertex(deformation.restVertices[vc.vertexIndex], deformation.bindings[vc.vertexIndex], state);
                    DVector3 r = deformed - vc.targetPosition;

                    residual[row++] = w * r.x;
                    residual[row++] = w * r.y;
                    residual[row++] = w * r.z;
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                var stateView = new EDStateView(state);

                int row = rowOffset;

                // Deliberately not guarding the vertex index the way the residual does. The legacy
                // block does not either, and the two have to agree bit for bit; a constraint that
                // would trip it throws there just as it does here.
                for (int c = 0; c < deformation.vertexConstraints.Count; c++)
                    row = deformation.FillConstraintJacobianBlock(stateView, jacobian, row, deformation.vertexConstraints[c].vertexIndex, residualWeight, ref jacobianNormSq);
            }
        }
#endif
    }
}
#endif
