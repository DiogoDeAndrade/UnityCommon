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

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal double FillClearanceJacobianRow(EDState state, DenseMatrix J, int row, int segmentIndex, double wClearance, FullDeformationField.TransformBlender blender = null)
        {
            var baseView = new EDStateView(state);

            // Serial fallback. In the StructureOnly Jacobian path, this should
            // already have been supplied by the worker-local scratch object.
            if ((UseDeformationFieldForClearance) && (blender == null))
            {
                blender = CreateFieldBlender(baseView);
            }

            double r0 = EvaluateSingleClearanceResidual(baseView, segmentIndex, wClearance, blender);

            if (Math.Abs(r0) <= 1e-12) return 0.0;

            double localJNorm = 0.0;

            // This is the loop over each perturbed Jacobian column.
            for (int col = 0; col < state.Count; col++)
            {
                double originalParameter = state.Get(col);

                double eps = 1e-6 * Math.Max(1.0, Math.Abs(originalParameter));

                var modifiedState = new EDStateView(state, col, eps);

                if (blender != null)
                {
                    // Twelve consecutive parameters belong to one ED node, so only this node's frame
                    // changes for this perturbation.
                    blender.SetNodeOverride(col / 12, GetNodeFrame(col / 12, modifiedState));
                }

                double r1;

                try
                {
                    r1 = EvaluateSingleClearanceResidual(modifiedState, segmentIndex, wClearance, blender);
                }
                finally
                {
                    // Back to the frozen transforms before the next column. Unconditional, because
                    // clearing is total - there is no state left over from a Set that did not run.
                    blender?.ClearNodeOverride();
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

            var baseView = new EDStateView(state);

            // Base residual value for this segment.
            double r0 = EvaluateSingleSlopeResidual(baseView, segmentIndex, wSlope);

            if (r0 <= 1e-12)
            {
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


            return row + 1;
        }

        internal int FillSlopeJacobianBlockStructure(EDState state, DenseMatrix J, int row, int nodeIndex, double wSlope, ref double jNorm)
        {

            var baseView = new EDStateView(state);

            // Base residual value for this segment.
            double r0 = EvaluateSingleNodeSlopeResidualStructure(baseView, nodeIndex, wSlope);

            if (r0 <= 1e-12)
            {
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
        internal int FillTerminalOrientationJacobianBlock(EDState state, DenseMatrix J, int row, int terminalIndex, double wTerminalOrientation, ref double jNorm)
        {
            EDTerminalConstraint terminal = terminalConstraints[terminalIndex];

            int nodeIndex = terminal.nodeIndex;

            var baseView = new EDStateView(state);

            DVector3 r0 = EvaluateSingleTerminalOrientationResidual(baseView, terminalIndex, wTerminalOrientation);

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

            int parameterBase = EDStateView.ParamBase(nodeIndex);

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

        #endregion

        #region Structure-based constraints

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


        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal int FillNodePositionJacobianBlockStructure(DenseMatrix J, int row, int nodeIndex, DVector3 restPoint, double wCon, ref double jNormRunningTotalSq)
        {

            if ((nodeIndex < 0) || (nodeIndex >= nodes.Count))
            {

                return row + 3;
            }

            int p = EDStateView.ParamBase(nodeIndex);

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


            return row + 3;
        }

        #endregion

#endif
    }
}
#endif
