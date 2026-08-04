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
    /// Stops a node turning inside out.
    ///
    /// The rotation term asks a node's axes to stay perpendicular and unit length, which are both
    /// properties of A transpose times A - and a reflection satisfies them exactly. So a mirrored
    /// node sits at zero rotation cost, and the solver is free to reach one whenever mirroring
    /// happens to satisfy the constraints more cheaply than turning. Measured on the golden set,
    /// plain embedded deformation ends up with 17-24% of its nodes reversed this way, and the
    /// navigation terms reduce that without removing it.
    ///
    /// It is invisible until it matters, which is why it survived so long. A reversed node flips
    /// normals wherever it dominates, and displaces positions by roughly the distance from the node
    /// times how far the frame turned - so on the navmesh itself, where every vertex sits within a
    /// sampling distance of its nodes, it reads as odd shading rather than as broken geometry. On
    /// output geometry several sampling distances away it throws vertices across the piece.
    ///
    /// One row per node, one-sided: a determinant above the floor costs nothing, so this does not
    /// argue with the rotation term about scale. The floor is above zero rather than at it because
    /// zero is degenerate too - a node collapsing onto a plane is as unusable as one mirrored
    /// through it, and it is the state a node has to pass through to invert.
    /// </summary>
    [Serializable]
    [PolymorphicName("Determinant (orientation)")]
    public class EDDeterminantTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("The smallest determinant a node's linear part may have before the term objects. 1 is undeformed; 0 is collapsed onto a plane; below 0 is inside out. A floor above zero also keeps a node away from the collapsed state it would have to cross to get there.")]
        private float minDeterminant = 0.5f;

        public override string name => "determinant";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new DeterminantInstance(this, deformation);

        public class DeterminantInstance : Instance
        {
            private readonly EDDeterminantTerm determinantTerm;

            public DeterminantInstance(EDDeterminantTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                determinantTerm = term;
            }

            protected override int ComputeRowCount() => deformation.nodes.Count;

            /// <summary>
            /// The determinant of a node's linear part, as the scalar triple product of its axes.
            /// The axes are the matrix columns, which is also what makes the gradient below a set of
            /// cross products rather than nine hand-written cofactors.
            /// </summary>
            private static double Determinant(EDStateView state, int nodeIndex)
            {
                DVector3 axisX = state.GetAxisX(nodeIndex);
                DVector3 axisY = state.GetAxisY(nodeIndex);
                DVector3 axisZ = state.GetAxisZ(nodeIndex);

                return DVector3.Dot(axisX, DVector3.Cross(axisY, axisZ));
            }

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = residualWeight;
                double floor = determinantTerm.minDeterminant;

                int row = rowOffset;

                for (int i = 0; i < deformation.nodes.Count; i++)
                {
                    double shortfall = floor - Determinant(state, i);

                    residual[row++] = (shortfall > 0.0) ? (w * shortfall) : (0.0);
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                var stateView = new EDStateView(state);

                double w = residualWeight;
                double floor = determinantTerm.minDeterminant;

                int row = rowOffset;

                for (int i = 0; i < deformation.nodes.Count; i++, row++)
                {
                    // Rows above the floor are left alone. The Jacobian is allocated zero-filled, so
                    // not writing is the same as writing zeros, and it is what the rotation term's
                    // disabled length rows already do.
                    if (Determinant(stateView, i) >= floor) continue;

                    DVector3 axisX = stateView.GetAxisX(i);
                    DVector3 axisY = stateView.GetAxisY(i);
                    DVector3 axisZ = stateView.GetAxisZ(i);

                    // d(det)/d(column) is the cross product of the other two columns, in cyclic
                    // order. The residual is (floor - det), so its derivative is the negation.
                    DVector3 dX = DVector3.Cross(axisY, axisZ);
                    DVector3 dY = DVector3.Cross(axisZ, axisX);
                    DVector3 dZ = DVector3.Cross(axisX, axisY);

                    int parameterBase = i * 12;

                    for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                    {
                        // Column c of the matrix holds axis c, and row r of that column lives at
                        // base + r*4 + c - the 4 being the stride of a 3x4 row, translation included.
                        int rowBase = parameterBase + outputAxis * 4;

                        double vx = -w * dX.GetComponent(outputAxis);
                        double vy = -w * dY.GetComponent(outputAxis);
                        double vz = -w * dZ.GetComponent(outputAxis);

                        jacobian[row, rowBase + 0] = vx;
                        jacobian[row, rowBase + 1] = vy;
                        jacobian[row, rowBase + 2] = vz;

                        jacobianNormSq += (vx * vx) + (vy * vy) + (vz * vz);
                    }
                }
            }
        }
#endif
    }
}
#endif
