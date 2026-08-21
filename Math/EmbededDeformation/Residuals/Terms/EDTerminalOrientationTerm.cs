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
    /// Holds a terminal node facing the way its connector demands. Three rows per terminal, one per
    /// axis of the orientation error.
    ///
    /// This is what makes a deformed piece still fit its neighbours: a terminal is where two map
    /// pieces meet, so its orientation is not free even though everything around it is. It is
    /// weighted far above the shape energies for the same reason - a piece that no longer joins is
    /// not a worse solution, it is not a solution.
    ///
    /// Structure graphs only, and unlike the navigation energies its rows are gated on the weight
    /// alone rather than on the navmesh data, matching the block it replaces. The terminals come
    /// from the structure, so they exist whether or not a navmesh was ever supplied.
    /// </summary>
    [Serializable]
    [PolymorphicName("Terminal Orientation (Structure)")]
    public class EDTerminalOrientationTerm : EDResidualTerm
    {
        public override string name => "terminalOrientation";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new TerminalOrientationInstance(this, deformation);

        public class TerminalOrientationInstance : Instance
        {
            public TerminalOrientationInstance(EDTerminalOrientationTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override int ComputeRowCount()
                => 3 * ((deformation.terminalConstraints != null) ? (deformation.terminalConstraints.Count) : (0));

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
                    row = FillJacobianBlock(state, jacobian, row, i, residualWeight, ref jacobianNormSq);
            }

            /// <summary>
            /// The rotation vector of the error between where the terminal points and where its
            /// connector says it should - the axis-angle log of one rotation relative to the other,
            /// so the three rows are a genuine rotation error rather than three separate angles.
            ///
            /// A node whose frame cannot be read as a rotation at all scores pi on the first row and
            /// zero on the other two. That is deliberately not a zero: an unreadable frame is the
            /// worst case, not an unconstrained one - but it is also not a direction, which is why
            /// only the magnitude is meaningful there.
            /// </summary>
            private DVector3 EvaluateItem(EDStateView state, int terminalIndex, double wTerminalOrientation)
            {
                EDTerminalConstraint terminal = deformation.terminalConstraints[terminalIndex];

                if (!TryGetNodeRotation(state, terminal.nodeIndex, out Quaternion currentRotation))
                {
                    return new DVector3(wTerminalOrientation * Math.PI, 0.0, 0.0);
                }

                Vector3 targetForward = terminal.targetForward.ToVector3().normalized;
                Vector3 targetUp = terminal.targetUp.ToVector3().normalized;

                Quaternion targetRotation = Quaternion.LookRotation(targetForward, targetUp);

                Quaternion rotationError = Quaternion.Inverse(targetRotation) * currentRotation;

                return wTerminalOrientation * QuaternionRotationVector(rotationError);
            }

            /// <summary>
            /// The three rows for one terminal, by finite differences over the node's own nine
            /// linear parameters only - translation cannot turn a frame, so those three columns
            /// are left at zero rather than differenced and found to be zero.
            ///
            /// The step is 1e-5 rather than the 1e-6 every other differenced block uses, and that is
            /// not a stray digit: the residual runs through Quaternion.LookRotation and quaternion
            /// normalization, which are Unity float operations, so a step small enough for a double
            /// residual would be differencing float noise here.
            /// </summary>
            private int FillJacobianBlock(EDState state, DenseMatrix J, int row, int terminalIndex, double wTerminalOrientation, ref double jNorm)
            {
                EDTerminalConstraint terminal = deformation.terminalConstraints[terminalIndex];

                int nodeIndex = terminal.nodeIndex;

                var baseView = new EDStateView(state);

                DVector3 r0 = EvaluateItem(baseView, terminalIndex, wTerminalOrientation);

                int parameterBase = EDStateView.ParamBase(nodeIndex);

                // Only the linear 3x3 transform affects orientation.
                for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                {
                    for (int inputAxis = 0; inputAxis < 3; inputAxis++)
                    {
                        int col = parameterBase + outputAxis * 4 + inputAxis;

                        double original = state.Get(col);

                        // Orientation construction uses Unity float quaternions.
                        double eps = 1e-5 * Math.Max(1.0, Math.Abs(original));

                        var modified = new EDStateView(state, col, eps);

                        DVector3 r1 = EvaluateItem(modified, terminalIndex, wTerminalOrientation);

                        double jx = (r1.x - r0.x) / eps;
                        double jy = (r1.y - r0.y) / eps;
                        double jz = (r1.z - r0.z) / eps;

                        J[row + 0, col] = jx;
                        J[row + 1, col] = jy;
                        J[row + 2, col] = jz;

                        jNorm += jx * jx + jy * jy + jz * jz;
                    }
                }

                return row + 3;
            }

            /// <summary>
            /// The node's frame read back as a pure rotation, with scale and shear removed - up is
            /// projected off forward rather than used as it comes, so a sheared node still reports
            /// the orientation it is turned to rather than one contaminated by the shear.
            ///
            /// Every fallback here is a repair of a degenerate frame in the order the frame degrades:
            /// a collapsed forward is rebuilt from right and up, a collapsed up from forward and
            /// right, and a frame with only a forward left gets an arbitrary up chosen off whichever
            /// world axis is least parallel to it. False only when even forward cannot be recovered.
            /// </summary>
            private bool TryGetNodeRotation(EDStateView state, int nodeIndex, out Quaternion rotation)
            {
                const float epsilon = 1e-8f;

                rotation = Quaternion.identity;

                if ((nodeIndex < 0) || (nodeIndex >= deformation.nodes.Count))
                {
                    return false;
                }

                EDNode node = deformation.nodes[nodeIndex];

                DVector3 right = state.TransformVector(nodeIndex, node.restRight);
                DVector3 up = state.TransformVector(nodeIndex, node.restUp);
                DVector3 forward = state.TransformVector(nodeIndex, node.restForward);

                if (forward.sqrMagnitude < epsilon)
                {
                    if ((right.sqrMagnitude > epsilon) && (up.sqrMagnitude > epsilon))
                    {
                        forward = DVector3.Cross(right, up);
                    }
                }

                if (forward.sqrMagnitude < epsilon)
                    return false;

                forward.Normalize();

                // Remove scale and shear from the orientation measurement.
                up = DVector3.ProjectOnPlane(up, forward);

                if ((up.sqrMagnitude < epsilon) && (right.sqrMagnitude > epsilon))
                {
                    up = DVector3.Cross(forward, right);
                }

                if (up.sqrMagnitude < epsilon)
                {
                    DVector3 fallback = (Math.Abs(DVector3.Dot(forward, DVector3.up)) < 0.95f) ? (DVector3.up) : (DVector3.right);

                    up = DVector3.ProjectOnPlane(fallback, forward);
                }

                if (up.sqrMagnitude < epsilon) return false;

                up.Normalize();

                rotation = Quaternion.LookRotation(forward.ToVector3(), up.ToVector3());

                return true;
            }

            /// <summary>
            /// The axis-angle vector of a rotation - its logarithm, as a vector of length equal to
            /// the angle.
            ///
            /// The shortest representation is chosen first, since q and -q are the same rotation and
            /// only one of them has an angle below pi. Near identity it falls back to 2v, which is
            /// the limit of the same expression and avoids dividing by a magnitude that is noise.
            /// The angle comes from atan2 of the vector part against w rather than from acos(w),
            /// which loses precision exactly where rotations are small.
            /// </summary>
            private static DVector3 QuaternionRotationVector(Quaternion rotation)
            {
                Quaternion q = rotation.normalized;

                // Select the shortest quaternion representation.
                if (q.w < 0.0f)
                {
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }

                Vector3 vectorPart = new Vector3(q.x, q.y, q.z);

                double sinHalfAngle = vectorPart.magnitude;

                if (sinHalfAngle < 1e-8)
                {
                    // log(q) ~= 2v close to identity.
                    return new DVector3(2.0 * q.x, 2.0 * q.y, 2.0 * q.z);
                }

                double angle = 2.0 * Math.Atan2(sinHalfAngle, Math.Clamp(q.w, -1.0f, 1.0f));

                Vector3 axis = vectorPart / (float)sinHalfAngle;

                return angle * axis.ToDVector3();
            }
        }
#endif
    }
}
#endif
