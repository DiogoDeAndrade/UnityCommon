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
                int row = rowOffset;

                // Deliberately not guarding the vertex index the way the residual does, which is a
                // real asymmetry rather than an oversight: the residual emits three zero rows for a
                // constraint naming a vertex outside the mesh, and this throws. It is preserved
                // because the baselines were captured against it and no golden configuration
                // contains such a constraint - so the guard has never had anything to do.
                for (int c = 0; c < deformation.vertexConstraints.Count; c++)
                    row = FillJacobianBlock(jacobian, row, deformation.vertexConstraints[c].vertexIndex, residualWeight, ref jacobianNormSq);
            }

            /// <summary>
            /// The three rows for one constrained vertex, analytically.
            ///
            /// It accumulates rather than assigns, and that is load-bearing: the vertex is deformed
            /// through its binding, so it writes into every node it is bound to, and two entries of
            /// one binding can name the same node. Assigning would keep only the last of them.
            ///
            /// No state, for the same reason the regularization block needs none - the residual is
            /// affine in the parameters, so every entry is a rest offset times a binding weight.
            /// </summary>
            private int FillJacobianBlock(DenseMatrix J, int row, int vertexIndex, double wCon, ref double jNormRunningTotalSq)
            {
                DVector3 v = deformation.restVertices[vertexIndex];
                EDVertexBinding binding = deformation.bindings[vertexIndex];

                for (int b = 0; b < binding.nodeIndices.Length; b++)
                {
                    int nodeIndex = binding.nodeIndices[b];
                    if (nodeIndex < 0)
                        continue;

                    double wb = ((binding.weights != null) && (b < binding.weights.Length)) ? (binding.weights[b]) : (1.0 / binding.nodeIndices.Length);

                    int p = EDStateView.ParamBase(nodeIndex);

                    DVector3 g = deformation.nodes[nodeIndex].restPosition;
                    DVector3 u = v - g;

                    double ux = u.x;
                    double uy = u.y;
                    double uz = u.z;

                    double s = wCon * wb;

                    // residual x
                    J[row + 0, p + 0] += s * ux; // m00
                    J[row + 0, p + 1] += s * uy; // m01
                    J[row + 0, p + 2] += s * uz; // m02
                    J[row + 0, p + 3] += s;      // m03 / tx

                    // residual y
                    J[row + 1, p + 4] += s * ux; // m10
                    J[row + 1, p + 5] += s * uy; // m11
                    J[row + 1, p + 6] += s * uz; // m12
                    J[row + 1, p + 7] += s;      // m13 / ty

                    // residual z
                    J[row + 2, p + 8] += s * ux; // m20
                    J[row + 2, p + 9] += s * uy; // m21
                    J[row + 2, p + 10] += s * uz; // m22
                    J[row + 2, p + 11] += s;      // m23 / tz

                    double u2 = ux * ux + uy * uy + uz * uz;
                    jNormRunningTotalSq += 3.0 * s * s * (u2 + 1.0);
                }

                return row + 3;
            }
        }
#endif
    }
}
#endif
