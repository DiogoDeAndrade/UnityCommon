using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    public partial class EmbededDeformation
    {
#if MATH_NET_AVAILABLE
        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int GetStructureHandlePositionConstraintPointCount()
        {
            if (handleConstraints == null)
                return 0;

            int pointCount = 0;

            for (int i = 0; i < handleConstraints.Count; i++)
            {
                // Root/centre handles constrain one point.
                // Terminal handles constrain both ends of the handle bar.
                pointCount += handleConstraints[i].isTerminal ? 2 : 1;
            }

            return pointCount;
        }

        /// <summary>
        /// Emits the residual row layout into an active golden dump, in the order the terms are
        /// actually evaluated in - which is the order the rows are in.
        ///
        /// This used to name all ten blocks from a fixed list, zeros included, in an order that did
        /// not match the rows: link angle was printed after the terminal blocks but evaluated
        /// before them. Naming the terms the model actually carries removes the possibility of the
        /// dump and the solve disagreeing, and a term that is not in the model is simply absent
        /// rather than reported as zero.
        /// </summary>
        private void TraceResidualLayout(EDEnergyModel.Instance energy)
        {
            if (EDDiagnostics.activeTrace == null) return;
            if (energy == null) return;

            EDDiagnostics.Trace("[layout]");

            var layout = energy.DescribeLayout();

            for (int i = 0; i < layout.Count; i++)
                EDDiagnostics.Trace($"{layout[i].name} {layout[i].rows}");

            EDDiagnostics.Trace($"total {energy.totalRows}");
        }

        #region NavMesh-based constraints

        private int ParamBase(int nodeIndex) => nodeIndex * 12;

        // Widened as terms adopt it - see FillRotationJacobianBlock below.
        internal int FillRotationJacobianBlockStructure(EDStateView state, DenseMatrix J, int row, int nodeIndex, double wRot, bool allowRightScale, ref double jNorm)
        {
            static int FillFrameDotJacobianRow(DenseMatrix J, int row, int parameterBase, DVector3 transformedA, DVector3 restA, DVector3 transformedB, DVector3 restB, double weight, ref double jNorm)
            {
                for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                {
                    double a = transformedA.GetComponent(outputAxis);
                    double b = transformedB.GetComponent(outputAxis);

                    for (int inputAxis = 0; inputAxis < 3; inputAxis++)
                    {
                        int col = parameterBase + outputAxis * 4 + inputAxis;

                        double value = weight * (b * restA.GetComponent(inputAxis) + a * restB.GetComponent(inputAxis));

                        J[row, col] = value;
                        jNorm += value * value;
                    }
                }

                return row + 1;
            }
            
            static int FillFrameLengthJacobianRow(DenseMatrix J, int row, int parameterBase, DVector3 transformed, DVector3 rest, double weight, bool enabled, ref double jNorm)
            {
                if (!enabled) return row + 1;

                for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                {
                    double transformedComponent = transformed.GetComponent(outputAxis);

                    for (int inputAxis = 0; inputAxis < 3; inputAxis++)
                    {
                        int col = parameterBase + outputAxis * 4 + inputAxis;
                        double value = 2.0 * weight * transformedComponent * rest.GetComponent(inputAxis);

                        J[row, col] = value;

                        jNorm += value * value;
                    }
                }

                return row + 1;
            }

            EDNode node = nodes[nodeIndex];

            DVector3 right = state.TransformVector(nodeIndex, node.restRight);
            DVector3 up = state.TransformVector(nodeIndex, node.restUp);
            DVector3 forward = state.TransformVector(nodeIndex, node.restForward);

            int parameterBase = ParamBase(nodeIndex);

            row = FillFrameDotJacobianRow(J, row, parameterBase, right, node.restRight, up, node.restUp, wRot, ref jNorm);
            row = FillFrameDotJacobianRow(J, row, parameterBase, right, node.restRight, forward, node.restForward, wRot, ref jNorm);
            row = FillFrameDotJacobianRow(J, row, parameterBase, up, node.restUp, forward, node.restForward, wRot, ref jNorm);
            row = FillFrameLengthJacobianRow(J, row, parameterBase, right, node.restRight, wRot, !allowRightScale, ref jNorm);
            row = FillFrameLengthJacobianRow(J, row, parameterBase, up, node.restUp, wRot, true, ref jNorm);
            row = FillFrameLengthJacobianRow(J, row, parameterBase, forward, node.restForward, wRot, true, ref jNorm);

            return row;
        }

        // Widened as terms adopt it. A migrated term calls the very same filler the legacy path
        // does, so the two agree by construction rather than by careful re-derivation.
        internal int FillRotationJacobianBlock(EDStateView state, Matrix<double> J, int row, int nodeIndex, double wRot, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildRotation);

            int p = ParamBase(nodeIndex);
            var aX = state.GetAxisX(nodeIndex);
            var aY = state.GetAxisY(nodeIndex);
            var aZ = state.GetAxisZ(nodeIndex);

            // r0 = X dot Y
            J[row, p + 0] = wRot * aY.x;
            J[row, p + 4] = wRot * aY.y;
            J[row, p + 8] = wRot * aY.z;

            J[row, p + 1] = wRot * aX.x;
            J[row, p + 5] = wRot * aX.y;
            J[row, p + 9] = wRot * aX.z;
            row++;

            // r1 = X dot Z
            J[row, p + 0] = wRot * aZ.x;
            J[row, p + 4] = wRot * aZ.y;
            J[row, p + 8] = wRot * aZ.z;

            J[row, p + 2] = wRot * aX.x;
            J[row, p + 6] = wRot * aX.y;
            J[row, p + 10] = wRot * aX.z;
            row++;

            // r2 = Y dot Z
            J[row, p + 1] = wRot * aZ.x;
            J[row, p + 5] = wRot * aZ.y;
            J[row, p + 9] = wRot * aZ.z;

            J[row, p + 2] = wRot * aY.x;
            J[row, p + 6] = wRot * aY.y;
            J[row, p + 10] = wRot * aY.z;
            row++;

            // r3 = X dot X - 1
            J[row, p + 0] = wRot * 2.0 * aX.x;
            J[row, p + 4] = wRot * 2.0 * aX.y;
            J[row, p + 8] = wRot * 2.0 * aX.z;
            row++;

            // r4 = Y dot Y - 1
            J[row, p + 1] = wRot * 2.0 * aY.x;
            J[row, p + 5] = wRot * 2.0 * aY.y;
            J[row, p + 9] = wRot * 2.0 * aY.z;
            row++;

            // r5 = Z dot Z - 1
            J[row, p + 2] = wRot * 2.0 * aZ.x;
            J[row, p + 6] = wRot * 2.0 * aZ.y;
            J[row, p + 10] = wRot * 2.0 * aZ.z;
            row++;

            double ax2 = aX.x * aX.x + aX.y * aX.y + aX.z * aX.z;
            double ay2 = aY.x * aY.x + aY.y * aY.y + aY.z * aY.z;
            double az2 = aZ.x * aZ.x + aZ.y * aZ.y + aZ.z * aZ.z;

            jNormRunningTotalSq += 6.0 * wRot * wRot * (ax2 + ay2 + az2);

            DebugProfiler.DebugMark(timeJacobianBuildRotation);

            return row;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock above.
        internal int FillRegularizationJacobianBlock(EDStateView state, Matrix<double> J, int row, int nodeJ, int nodeK, double wReg, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildRegularization);

            int pj = ParamBase(nodeJ);
            int pk = ParamBase(nodeK);

            DVector3 gj = nodes[nodeJ].restPosition;
            DVector3 gk = nodes[nodeK].restPosition;
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

            DebugProfiler.DebugMark(timeJacobianBuildRegularization);

            return row;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillConstraintJacobianBlock(EDStateView state, Matrix<double> J, int row, int vertexIndex, double wCon, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            DVector3 v = restVertices[vertexIndex];
            EDVertexBinding binding = bindings[vertexIndex];

            for (int b = 0; b < binding.nodeIndices.Length; b++)
            {
                int nodeIndex = binding.nodeIndices[b];
                if (nodeIndex < 0)
                    continue;

                double wb = ((binding.weights != null) && (b < binding.weights.Length)) ? (binding.weights[b]) : (1.0 / binding.nodeIndices.Length);

                int p = ParamBase(nodeIndex);

                DVector3 g = nodes[nodeIndex].restPosition;
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

            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            return row + 3;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal double FillClearanceJacobianRow(EDState state, DenseMatrix J, int row, int segmentIndex, double wClearance, List<FullDeformationField.Frame> nodeFrames = null)
        {
            var baseView = new EDStateView(state);

            // Serial fallback. In the StructureOnly Jacobian path, this should
            // already have been supplied by the worker-local scratch object.
            if ((UseDeformationFieldForClearance) && (nodeFrames == null))
            {
                nodeFrames = BuildNodeFrames(baseView);
            }

            double r0 = EvaluateSingleClearanceResidual(baseView, segmentIndex, wClearance, nodeFrames);

            if (Math.Abs(r0) <= 1e-12) return 0.0;

            double localJNorm = 0.0;

            // This is the loop over each perturbed Jacobian column.
            for (int col = 0; col < state.Count; col++)
            {
                double originalParameter = state.Get(col);

                double eps = 1e-6 * Math.Max(1.0, Math.Abs(originalParameter));

                var modifiedState = new EDStateView(state, col, eps);

                int perturbedNodeIndex = -1;
                FullDeformationField.Frame originalFrame = default;

                if (nodeFrames != null)
                {
                    // Twelve consecutive parameters belong to one ED node.
                    perturbedNodeIndex = col / 12;

                    originalFrame = nodeFrames[perturbedNodeIndex];

                    // Only this node's frame changes for this perturbation.
                    nodeFrames[perturbedNodeIndex] = GetNodeFrame(perturbedNodeIndex, modifiedState);
                }

                double r1;

                try
                {
                    r1 = EvaluateSingleClearanceResidual(modifiedState, segmentIndex, wClearance, nodeFrames);
                }
                finally
                {
                    // Restore the base frame before evaluating the next column.
                    if (perturbedNodeIndex >= 0)
                    {
                        nodeFrames[perturbedNodeIndex] = originalFrame;
                    }
                }

                double value = (r1 - r0) / eps;

                J[row, col] = value;
                localJNorm += value * value;
            }

            return localJNorm;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillSlopeJacobianBlock(EDState state, DenseMatrix J, int row, int segmentIndex, double wSlope, ref double jNorm)
        {
            DebugProfiler.DebugMark(timeJacobianBuildSlope);

            var baseView = new EDStateView(state);

            // Base residual value for this segment.
            double r0 = EvaluateSingleSlopeResidual(baseView, segmentIndex, wSlope);

            if (r0 <= 1e-12)
            {
                DebugProfiler.DebugMark(timeJacobianBuildSlope);
                return row + 1;
            }

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);

                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));
                var modifiedState = new EDStateView(state, col, eps);

                double r1 = EvaluateSingleSlopeResidual(modifiedState, segmentIndex, wSlope);

                J[row, col] = (r1 - r0) / eps;

                jNorm += J[row, col] * J[row, col];
            }

            DebugProfiler.DebugMark(timeJacobianBuildSlope);

            return row + 1;
        }

        internal int FillSlopeJacobianBlockStructure(EDState state, DenseMatrix J, int row, int nodeIndex, double wSlope, ref double jNorm)
        {
            DebugProfiler.DebugMark(timeJacobianBuildSlope);

            var baseView = new EDStateView(state);

            // Base residual value for this segment.
            double r0 = EvaluateSingleNodeSlopeResidualStructure(baseView, nodeIndex, wSlope);

            if (r0 <= 1e-12)
            {
                DebugProfiler.DebugMark(timeJacobianBuildSlope);
                return row + 1;
            }

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);

                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));
                var modifiedState = new EDStateView(state, col, eps);

                double r1 = EvaluateSingleNodeSlopeResidualStructure(modifiedState, nodeIndex, wSlope);

                J[row, col] = (r1 - r0) / eps;

                jNorm += J[row, col] * J[row, col];
            }

            DebugProfiler.DebugMark(timeJacobianBuildSlope);

            return row + 1;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillOrientationJacobianBlock(EDState state, DenseMatrix J, int row, int segmentIndex, double wOrientation, ref double jNorm)
        {
            var baseView = new EDStateView(state);

            DVector3 r0 = EvaluateSingleOrientationResidual(baseView, segmentIndex, wOrientation);

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);
                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                var modifiedState = new EDStateView(state, col, eps);

                DVector3 r1 = EvaluateSingleOrientationResidual(modifiedState, segmentIndex, wOrientation);

                double jx = (r1.x - r0.x) / eps;
                double jy = (r1.y - r0.y) / eps;
                double jz = (r1.z - r0.z) / eps;

                J[row + 0, col] = jx;
                J[row + 1, col] = jy;
                J[row + 2, col] = jz;

                jNorm += jx * jx + jy * jy + jz * jz;
            }

            return row + 3;
        }

        internal int FillOrientationJacobianBlockStructure(EDState state, DenseMatrix J, int row, int nodeIndex, double wOrientation, ref double jNorm)
        {
            var baseView = new EDStateView(state);

            DVector3 r0 = EvaluateSingleNodeOrientationResidualStructure(baseView, nodeIndex, wOrientation);

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);
                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                var modifiedState = new EDStateView(state, col, eps);

                DVector3 r1 = EvaluateSingleNodeOrientationResidualStructure(modifiedState, nodeIndex, wOrientation);

                double jx = (r1.x - r0.x) / eps;
                double jy = (r1.y - r0.y) / eps;
                double jz = (r1.z - r0.z) / eps;

                J[row + 0, col] = jx;
                J[row + 1, col] = jy;
                J[row + 2, col] = jz;

                jNorm += jx * jx + jy * jy + jz * jz;
            }

            return row + 3;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillSegmentLengthJacobianBlock(EDState state, DenseMatrix J, int row, int segmentIndex, double wSegmentLength, ref double jNorm)
        {
            var baseView = new EDStateView(state);

            double r0 = EvaluateSingleSegmentLengthResidual(baseView, segmentIndex, wSegmentLength);

            if (Math.Abs(r0) <= 1e-12)
                return row + 1;

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);
                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                var modifiedState = new EDStateView(state, col, eps);

                double r1 = EvaluateSingleSegmentLengthResidual(modifiedState, segmentIndex, wSegmentLength);
                double v = (r1 - r0) / eps;

                J[row, col] = v;
                jNorm += v * v;
            }

            return row + 1;
        }

        internal int FillSegmentLengthJacobianBlockStructure(EDState state, DenseMatrix J, int row, int segmentIndex, double wSegmentLength, ref double jNorm)
        {
            var baseView = new EDStateView(state);

            double r0 = EvaluateSingleSegmentLengthResidualStructure(baseView, segmentIndex, wSegmentLength);

            if (Math.Abs(r0) <= 1e-12)
                return row + 1;

            for (int col = 0; col < state.Count; col++)
            {
                double original = state.Get(col);
                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                var modifiedState = new EDStateView(state, col, eps);

                double r1 = EvaluateSingleSegmentLengthResidualStructure(modifiedState, segmentIndex, wSegmentLength);
                double v = (r1 - r0) / eps;

                J[row, col] = v;
                jNorm += v * v;
            }

            return row + 1;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillTerminalOrientationJacobianBlock(EDState state, DenseMatrix J, int row, int terminalIndex, double wTerminalOrientation, ref double jNorm)
        {
            EDTerminalConstraint terminal = terminalConstraints[terminalIndex];

            int nodeIndex = terminal.nodeIndex;

            var baseView = new EDStateView(state);

            DVector3 r0 = EvaluateSingleTerminalOrientationResidual(baseView, terminalIndex, wTerminalOrientation);

            int parameterBase = ParamBase(nodeIndex);

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

                    DVector3 r1 = EvaluateSingleTerminalOrientationResidual(modified, terminalIndex, wTerminalOrientation);

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

        internal int FillTerminalScaleJacobianBlock(EDStateView state, DenseMatrix J, int row, int terminalIndex, double wTerminalScale, ref double jNorm)
        {
            EDTerminalConstraint terminal = terminalConstraints[terminalIndex];

            int nodeIndex = terminal.nodeIndex;

            EDNode      node = nodes[nodeIndex];
            DVector3    restRight = node.restRight;
            DVector3    currentRight = state.TransformVector(nodeIndex, restRight);
            double      currentScale = currentRight.magnitude;

            if (currentScale < 1e-12)
                return row + 1;

            int parameterBase = ParamBase(nodeIndex);

            for (int outputAxis = 0; outputAxis < 3; outputAxis++)
            {
                double normalizedCurrentComponent = currentRight.GetComponent(outputAxis) / currentScale;

                for (int inputAxis = 0; inputAxis < 3; inputAxis++)
                {
                    int col = parameterBase + outputAxis * 4 + inputAxis;

                    double value = wTerminalScale * normalizedCurrentComponent * restRight.GetComponent(inputAxis);

                    J[row, col] = value;

                    jNorm += value * value;
                }
            }

            return row + 1;
        }

        private void FillLinkAngleJacobianColumn(EDState state, DenseMatrix J, int row, int constraintIndex, double wLinkAngle, double baseCosineResidual, double baseSineResidual, int col, ref double jNorm)
        {
            double original = state.Get(col);

            double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

            EDStateView modified = new EDStateView(state, col, eps);

            EvaluateSingleLinkAngleResidual(modified, constraintIndex, wLinkAngle, out double modifiedCosineResidual, out double modifiedSineResidual);

            double cosineDerivative = (modifiedCosineResidual - baseCosineResidual) / eps;

            double sineDerivative = (modifiedSineResidual - baseSineResidual) / eps;

            J[row + 0, col] = cosineDerivative;
            J[row + 1, col] = sineDerivative;

            jNorm += cosineDerivative * cosineDerivative + sineDerivative * sineDerivative;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillLinkAngleJacobianBlock(EDState state, DenseMatrix J, int row, int constraintIndex, double wLinkAngle, ref double jNorm)
        {
            EDLinkAngleConstraint constraint = linkAngleConstraints[constraintIndex];

            EDStateView baseView = new EDStateView(state);

            EvaluateSingleLinkAngleResidual(baseView, constraintIndex, wLinkAngle, out double baseCosineResidual, out double baseSineResidual);

            // Centre node: orientation and translation both matter.
            int centerBase = ParamBase(constraint.centerNode);

            for (int localParameter = 0; localParameter < 12; localParameter++)
            {
                FillLinkAngleJacobianColumn(state, J, row, constraintIndex, wLinkAngle, baseCosineResidual, baseSineResidual, centerBase + localParameter, ref jNorm);
            }

            // Neighbour positions depend only on translation.
            int neighborABase = ParamBase(constraint.neighborA);

            for (int outputAxis = 0; outputAxis < 3; outputAxis++)
            {
                int col = neighborABase + outputAxis * 4 + 3;

                FillLinkAngleJacobianColumn(state, J, row, constraintIndex, wLinkAngle, baseCosineResidual, baseSineResidual, col, ref jNorm);
            }

            int neighborBBase = ParamBase(constraint.neighborB);

            for (int outputAxis = 0; outputAxis < 3; outputAxis++)
            {
                int col = neighborBBase + outputAxis * 4 + 3;

                FillLinkAngleJacobianColumn(state, J, row, constraintIndex, wLinkAngle, baseCosineResidual, baseSineResidual, col, ref jNorm);
            }

            return row + 2;
        }

        public void DebugJacobianNullspace(Matrix<double> J, double singularValueTolerance = 1e-10, int topCount = 20)
        {
            var svd = J.Svd(true);

            var s = svd.S;
            double sigmaMax = s[0];
            double sigmaMin = s[s.Count - 1];
            double tol = singularValueTolerance * sigmaMax;

            int rank = 0;
            for (int i = 0; i < s.Count; i++)
            {
                if (s[i] > tol)
                    rank++;
            }

            Debug.Log($"[ED] J rows = {J.RowCount}, cols = {J.ColumnCount}");
            Debug.Log($"[ED] Rank \u2245 {rank}/{J.ColumnCount}");
            Debug.Log($"[ED] Nullity \u2245 {J.ColumnCount - rank}");
            Debug.Log($"[ED] sigmaMax = {sigmaMax}");
            Debug.Log($"[ED] sigmaMin = {sigmaMin}");
            Debug.Log($"[ED] Condition \u2245 {sigmaMax / Math.Max(sigmaMin, 1e-300)}");

            // Math.NET returns VT, so the smallest right singular vector is the last row of VT
            var vt = svd.VT;
            int lastRow = vt.RowCount - 1;
            Vector<double> nullVec = vt.Row(lastRow);

            // Normalize for easier reading
            double maxAbs = 0.0;
            for (int i = 0; i < nullVec.Count; i++)
                maxAbs = Math.Max(maxAbs, Math.Abs(nullVec[i]));

            if (maxAbs > 0.0)
                nullVec = nullVec / maxAbs;

            // Collect largest entries
            List<(int index, double value)> entries = new();
            for (int i = 0; i < nullVec.Count; i++)
                entries.Add((i, nullVec[i]));

            entries.Sort((a, b) => Math.Abs(b.value).CompareTo(Math.Abs(a.value)));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[ED] Dominant entries in smallest right singular vector:");

            int count = Math.Min(topCount, entries.Count);
            for (int i = 0; i < count; i++)
            {
                int idx = entries[i].index;
                double val = entries[i].value;

                int nodeIndex = idx / 12;
                int localParam = idx % 12;

                sb.AppendLine(
                    $"  #{i + 1}: global={idx}, node={nodeIndex}, param={ParamName(localParam)}, value={val}");
            }

            Debug.Log(sb.ToString());

            // Optional summary per node
            StringBuilder sbNode = new StringBuilder();
            sbNode.AppendLine("[ED] Null-space magnitude per node:");

            for (int n = 0; n < nodes.Count; n++)
            {
                int p = n * 12;

                double blockNormSq = 0.0;
                double matrixNormSq = 0.0;
                double translationNormSq = 0.0;

                for (int k = 0; k < 12; k++)
                {
                    double v = nullVec[p + k];
                    blockNormSq += v * v;

                    if (k < 9) matrixNormSq += v * v;
                    else translationNormSq += v * v;
                }

                double blockNorm = Math.Sqrt(blockNormSq);
                double matrixNorm = Math.Sqrt(matrixNormSq);
                double translationNorm = Math.Sqrt(translationNormSq);

                sbNode.AppendLine(
                    $"  node {n}: total={blockNorm}, matrix={matrixNorm}, translation={translationNorm}");
            }

            Debug.Log(sbNode.ToString());
        }

        private string ParamName(int localParam)
        {
            switch (localParam)
            {
                case 0: return "m00";
                case 1: return "m01";
                case 2: return "m02";
                case 3: return "tx";

                case 4: return "m10";
                case 5: return "m11";
                case 6: return "m12";
                case 7: return "ty";

                case 8: return "m20";
                case 9: return "m21";
                case 10: return "m22";
                case 11: return "tz";

                default: return $"p{localParam}";
            }
        }

        #endregion

        #region Structure-based constraints

        private bool TryGetStructureHandlePositionConstraint(EDHandleConstraint hc, out int nodeIndex, out DVector3 targetPosition)
        {
            nodeIndex = -1;
            targetPosition = DVector3.zero;

            if ((nodes == null) || (nodes.Count == 0))
                return false;

            Vector3 restHandlePosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero);

            Vector3 currentHandlePosition = hc.currentHandleMatrix.MultiplyPoint3x4(Vector3.zero);

            if (hc.isTerminal)
            {
                // Connector handles target terminal structure endpoints.
                nodeIndex = GetClosestLeafNodeIndex(restHandlePosition);
            }
            else
            {
                // Center/root handles target the corresponding structure/root node.
                nodeIndex = GetClosestDebugNodeIndex(restHandlePosition);
            }

            if ((nodeIndex < 0) || (nodeIndex >= nodes.Count))
                return false;

            targetPosition = currentHandlePosition.ToDVector3();

            return true;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal bool TryGetStructureHandleNodeIndex(EDHandleConstraint hc, out int nodeIndex)
        {
            nodeIndex = -1;

            if ((nodes == null) || (nodes.Count == 0))
                return false;

            Vector3 restHandlePosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero);

            if (hc.isTerminal)
            {
                // Connector handles target terminal structure endpoints.
                nodeIndex = GetClosestLeafNodeIndex(restHandlePosition);
            }
            else
            {
                // Centre/root handles target the corresponding structure node.
                nodeIndex = GetClosestDebugNodeIndex(restHandlePosition);
            }

            return (nodeIndex >= 0) && (nodeIndex < nodes.Count);
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal static void GetStructureHandleBarPoints(EDHandleConstraint hc, out DVector3 restLeft, out DVector3 restRight, out DVector3 targetLeft, out DVector3 targetRight)
        {
            const float epsilon = 1e-8f;

            Vector3 restCenter = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero);

            Vector3 targetCenter = hc.currentHandleMatrix.MultiplyPoint3x4(Vector3.zero);

            Vector3 restAxis = hc.restHandleMatrix.MultiplyVector(Vector3.right);

            Vector3 targetAxis = hc.currentHandleMatrix.MultiplyVector(Vector3.right);

            float restAxisLength = restAxis.magnitude;
            float targetAxisLength = targetAxis.magnitude;

            Vector3 restDirection = (restAxisLength > epsilon) ? (restAxis / restAxisLength) : Vector3.right;

            Vector3 targetDirection = (targetAxisLength > epsilon) ? (targetAxis / targetAxisLength) : (restDirection);

            float halfRestWidth = 0.5f * Mathf.Abs(hc.width);

            // The current transform's X scale relative to the rest transform
            // determines the requested terminal width scale.
            float targetScale = targetAxisLength / Mathf.Max(restAxisLength, epsilon);

            float halfTargetWidth = halfRestWidth * targetScale;

            restLeft = (restCenter - restDirection * halfRestWidth).ToDVector3();

            restRight = (restCenter + restDirection * halfRestWidth).ToDVector3();

            targetLeft = (targetCenter - targetDirection * halfTargetWidth).ToDVector3();

            targetRight = (targetCenter + targetDirection * halfTargetWidth).ToDVector3();
        }


        private DVector3 DeformStructureNodePosition(int nodeIndex, EDStateView state)
        {
            DVector3 restPosition = nodes[nodeIndex].restPosition;

            // At the node itself, local offset is zero, so only the node translation matters.
            return restPosition + state.TransformOffset(nodeIndex, DVector3.zero);
        }

        private int FillStructureNodePositionJacobianBlock(DenseMatrix J, int row, int nodeIndex, double wCon, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            if ((nodeIndex < 0) || (nodeIndex >= nodes.Count))
            {
                DebugProfiler.DebugMark(timeJacobianBuildConstraint);
                return row + 3;
            }

            int p = ParamBase(nodeIndex);

            // Residual:
            // r = nodePosition' - target
            //
            // nodePosition' = restPosition + translation
            //
            // So the derivative only touches tx, ty, tz.
            J[row + 0, p + 3] = wCon;   // tx
            J[row + 1, p + 7] = wCon;   // ty
            J[row + 2, p + 11] = wCon;  // tz

            jNormRunningTotalSq += 3.0 * wCon * wCon;

            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            return row + 3;
        }

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillNodePositionJacobianBlockStructure(DenseMatrix J, int row, int nodeIndex, DVector3 restPoint, double wCon, ref double jNormRunningTotalSq)
        {
            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            if ((nodeIndex < 0) || (nodeIndex >= nodes.Count))
            {
                DebugProfiler.DebugMark(timeJacobianBuildConstraint);

                return row + 3;
            }

            int p = ParamBase(nodeIndex);

            DVector3 localOffset = restPoint - nodes[nodeIndex].restPosition;

            double ux = localOffset.x;
            double uy = localOffset.y;
            double uz = localOffset.z;

            // X residual.
            J[row + 0, p + 0] = wCon * ux;
            J[row + 0, p + 1] = wCon * uy;
            J[row + 0, p + 2] = wCon * uz;
            J[row + 0, p + 3] = wCon;

            // Y residual.
            J[row + 1, p + 4] = wCon * ux;
            J[row + 1, p + 5] = wCon * uy;
            J[row + 1, p + 6] = wCon * uz;
            J[row + 1, p + 7] = wCon;

            // Z residual.
            J[row + 2, p + 8] = wCon * ux;
            J[row + 2, p + 9] = wCon * uy;
            J[row + 2, p + 10] = wCon * uz;
            J[row + 2, p + 11] = wCon;

            double offsetLengthSq = ux * ux + uy * uy + uz * uz;

            jNormRunningTotalSq += 3.0 * wCon * wCon * (offsetLengthSq + 1.0);

            DebugProfiler.DebugMark(timeJacobianBuildConstraint);

            return row + 3;
        }

        #endregion

#endif
    }
}
#endif
