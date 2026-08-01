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

        private struct EDResidualLayout
        {
            public int rotationRows;
            public int regularizationRows;
            public int constraintRows;
            public int clearanceRows;
            public int slopeRows;
            public int orientationRows;
            public int segmentLengthRows;
            public int terminalOrientationRows;
            public int terminalScaleRows;
            public int linkAngleRows;

            public int totalRows => rotationRows + regularizationRows + constraintRows + clearanceRows + slopeRows + orientationRows + segmentLengthRows + terminalOrientationRows + terminalScaleRows + linkAngleRows;
        }

        private EDResidualLayout BuildResidualLayoutCommon(WeightConfig weights, int constraintCount)
        {
            int nodeCount = nodes.Count;

            int directedEdgeCount = 0;
            for (int i = 0; i < nodeCount; i++)
                directedEdgeCount += nodes[i].neighbors.Count;

            int structureCount = (structure != null) ? structure.Count : 0;

            // The navigation-aware energies all evaluate against data that only SetNavEDParameters
            // supplies - the navmesh topology and the per-segment bindings and probes. Without it
            // they have nothing to measure, so they contribute no rows at all.
            //
            // Gating here rather than at each evaluator keeps the layout, the residual and the
            // Jacobian in agreement automatically, since all three derive their block sizes from
            // this. Note the weights themselves cannot be trusted as a proxy: GenerateED hides the
            // nav weight fields outside NavED mode but still passes their serialized values, so
            // they are routinely non-zero in a plain ED solve.
            bool nav = isNavConfigured;

            EDResidualLayout layout = new EDResidualLayout
            {
                rotationRows = 6 * nodeCount,
                regularizationRows = 3 * directedEdgeCount,

                constraintRows = 3 * constraintCount,

                clearanceRows = ((nav) && (weights.clearanceWeight > 0.0)) ? structureCount : 0,
                slopeRows = ((nav) && (weights.slopeWeight > 0.0)) ? structureCount : 0,
                orientationRows = ((nav) && (weights.orientationWeight > 0.0)) ? 3 * structureCount : 0,
                segmentLengthRows = ((nav) && (weights.segmentLengthWeight > 0.0)) ? structureCount : 0
            };

            return layout;
        }

        private EDResidualLayout BuildResidualLayoutNavMesh(WeightConfig weights)
        {
            int constraintCount = (vertexConstraints != null) ? vertexConstraints.Count : 0;

            return BuildResidualLayoutCommon(weights, constraintCount);
        }

        private EDResidualLayout BuildResidualLayoutStructure(WeightConfig weights)
        {
            int nodeCount = nodes.Count;

            int directedEdgeCount = 0;
            for (int i = 0; i < nodeCount; i++)
                directedEdgeCount += nodes[i].neighbors.Count;

            int structureCount = (structure != null) ? structure.Count : 0;

            int constraintPointCount = GetStructureHandlePositionConstraintPointCount();

            int terminalCount = (terminalConstraints != null) ? (terminalConstraints.Count) : (0);

            // See BuildResidualLayoutCommon for why the nav energies are gated on configuration
            // rather than on their weights alone.
            bool nav = isNavConfigured;

            return new EDResidualLayout
            {
                rotationRows = 6 * nodeCount,
                regularizationRows = 3 * directedEdgeCount,
                constraintRows = 3 * constraintPointCount,
                clearanceRows = ((nav) && (weights.clearanceWeight > 0.0)) ? (structureCount) : (0),
                slopeRows = ((nav) && (weights.slopeWeight > 0.0)) ? (nodeCount) : (0),
                orientationRows = ((nav) && (weights.orientationWeight > 0.0)) ? (3 * nodeCount) : (0),
                segmentLengthRows = ((nav) && (weights.segmentLengthWeight > 0.0)) ? (structureCount) : (0),
                terminalOrientationRows = (weights.terminalOrientationWeight > 0.0) ? (3 * terminalCount) : (0),
                terminalScaleRows = (weights.terminalScaleWeight > 0.0) ? (terminalCount) : (0),
                linkAngleRows = (weights.linkAngleWeight > 0.0) ? (2 * linkAngleConstraints.Count) : (0),
            };
        }

        private EDResidualLayout BuildResidualLayoutForCurrentGraph(WeightConfig weights)
        {
            switch (deformationGraphSource)
            {
                case DeformationGraphSource.NavMeshAndStructure:
                    return BuildResidualLayoutNavMesh(weights);

                case DeformationGraphSource.StructureOnly:
                    return BuildResidualLayoutStructure(weights);

                default:
                    return BuildResidualLayoutNavMesh(weights);
            }
        }

        /// <summary>
        /// Emits the residual row layout into an active golden dump. The row counts per block are
        /// the thing most likely to shift silently during the term refactor, so they are recorded
        /// before any iteration runs.
        /// </summary>
        /// <summary>
        /// Emits the residual row layout into an active golden dump. The row counts per block are
        /// the thing most likely to shift silently during the term refactor, so they are recorded
        /// before any iteration runs.
        ///
        /// The counts now come from the term list, but the section keeps the shape the layout struct
        /// gave it: every block named, in the struct's order, zeros included. That is deliberately
        /// not the order the terms are evaluated in - link angle is emitted before the terminal
        /// blocks - and it is preserved only so this change can be checked against the existing
        /// baselines byte for byte. Once the legacy path is deleted this should emit the terms in
        /// their real order and the baselines should be re-captured.
        /// </summary>
        private void TraceResidualLayout(EDEnergyModel.Instance energy)
        {
            if (EDDiagnostics.activeTrace == null) return;
            if (energy == null) return;

            EDDiagnostics.Trace("[layout]");

            int total = 0;

            foreach (string blockName in new[] { "rotation", "regularization", "constraint", "clearance",
                                                 "slope", "orientation", "segmentLength",
                                                 "terminalOrientation", "terminalScale", "linkAngle" })
            {
                int rows = energy.GetRowCount(blockName);

                EDDiagnostics.Trace($"{blockName} {rows}");

                total += rows;
            }

            EDDiagnostics.Trace($"total {total}");
        }

        private static double BuildResidualWeight(double conceptualWeight, int residualRows, bool normalizeResidualGroups)
        {
            if ((conceptualWeight <= 0.0) || (residualRows <= 0))
                return 0.0;

            double denom = normalizeResidualGroups ? residualRows : 1.0;

            return Math.Sqrt(conceptualWeight / denom);
        }

        #region NavMesh-based constraints
        private Vector<double> EvaluateResidualVectorNavMesh(EDStateView state, WeightConfig weights)
        {
            DebugProfiler.DebugMark(timeResidualEvaluate);

            int nodeCount = nodes.Count;
            int directedEdgeCount = 0;
            for (int i = 0; i < nodes.Count; i++)
                directedEdgeCount += nodes[i].neighbors.Count;
            int constraintCount = vertexConstraints.Count;

            EDResidualLayout layout = BuildResidualLayoutNavMesh(weights);
            int residualCount = layout.totalRows;

            Vector<double> residual = DenseVector.Create(residualCount, 0.0);

            double wRot = BuildResidualWeight(weights.rotationWeight, layout.rotationRows, weights.normalizeWeights);
            double wReg = BuildResidualWeight(weights.regularizationWeight, layout.regularizationRows, weights.normalizeWeights);
            double wCon = BuildResidualWeight(weights.constraintWeight, layout.constraintRows, weights.normalizeWeights);
            double wClearance = BuildResidualWeight(weights.clearanceWeight, layout.clearanceRows, weights.normalizeWeights);
            double wSlope = BuildResidualWeight(weights.slopeWeight, layout.slopeRows, weights.normalizeWeights);
            double wOrientation = BuildResidualWeight(weights.orientationWeight, layout.orientationRows, weights.normalizeWeights);
            double wSegmentLength = BuildResidualWeight(weights.segmentLengthWeight, layout.segmentLengthRows, weights.normalizeWeights);

            int row = 0;

            // -------------------------------------------------------------
            // 1) Rotation residuals: 6 per node
            //    We extract the 3 basis (3 axis) and build 6 conditions:
            //    1. axisX and axisY are perpendicular
            //    2. axisX and axisZ are perpendicular
            //    3. axisY and axisZ are perpendicular
            //    4. axisX has unit length
            //    5. axisY has unit length
            //    6. axisZ has unit length
            //    This basically states - is this matrix a valid rotation?
            // -------------------------------------------------------------
            for (int i = 0; i < nodeCount; i++)
            {
                var axisX = state.GetAxisX(i);
                var axisY = state.GetAxisY(i);
                var axisZ = state.GetAxisZ(i);

                residual[row++] = wRot * DVector3.Dot(axisX, axisY);
                residual[row++] = wRot * DVector3.Dot(axisX, axisZ);
                residual[row++] = wRot * DVector3.Dot(axisY, axisZ);

                residual[row++] = wRot * (DVector3.Dot(axisX, axisX) - 1.0);
                residual[row++] = wRot * (DVector3.Dot(axisY, axisY) - 1.0);
                residual[row++] = wRot * (DVector3.Dot(axisZ, axisZ) - 1.0);
            }

            // -------------------------------------------------------------
            // 2) Regularization residuals: 3 per directed edge
            //
            //    R_j (g_k - g_j) + g_j + t_j - (g_k + t_k)
            //    Basically, we find where node j predicts node k should end up
            //    Then we compare (subtract) with the actual position of node k
            //    The idea here is to check if neighbour nodes are moving in a coherent 
            //    fashion.
            // -------------------------------------------------------------
            for (int j = 0; j < nodeCount; j++)
            {
                EDNode nodeJ = nodes[j];
                DVector3 gj = nodeJ.restPosition;
                DVector3 tj = state.GetTranslation(j);

                foreach (int k in nodeJ.neighbors)
                {
                    EDNode nodeK = nodes[k];
                    DVector3 gk = nodeK.restPosition;
                    DVector3 tk = state.GetTranslation(k);

                    DVector3 diff = gk - gj;
                    DVector3 rotatedDiff = state.TransformVector(j, diff);

                    DVector3 r = rotatedDiff + gj + tj - (gk + tk);

                    residual[row++] = wReg * r.x;
                    residual[row++] = wReg * r.y;
                    residual[row++] = wReg * r.z;
                }
            }

            // -------------------------------------------------------------
            // 3) Positional constraints: 3 per constrained vertex
            //
            //    deformed(v) - target
            //    Locks positions to anchor points
            // -------------------------------------------------------------
            for (int c = 0; c < constraintCount; c++)
            {
                EDVertexConstraint vc = vertexConstraints[c];

                if ((vc.vertexIndex < 0) || (vc.vertexIndex >= restVertices.Length))
                {
                    residual[row++] = 0.0;
                    residual[row++] = 0.0;
                    residual[row++] = 0.0;
                    continue;
                }

                DVector3 deformed = DeformVertex(restVertices[vc.vertexIndex], bindings[vc.vertexIndex], state);
                DVector3 r = deformed - vc.targetPosition;

                residual[row++] = wCon * r.x;
                residual[row++] = wCon * r.y;
                residual[row++] = wCon * r.z;
            }

            if (wClearance > 0)
            {
                // -------------------------------------------------------------
                // 4) Clearance constraints - allow for clearance to grow, but constrained it going smaller
                //
                // -------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    var originalClearance = restState.GetClearance(i);
                    var currentClearance = state.GetClearance(i);

                    residual[row++] = wClearance * ComputeClearanceLoss(originalClearance, currentClearance);
                }
            }

            if (wSlope > 0)
            {
                // -------------------------------------------------------------
                // 5) Slope constraints
                // -------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    residual[row++] = EvaluateSingleSlopeResidual(state, i, wSlope);
                }
            }

            if (wOrientation > 0)
            {
                // -------------------------------------------------------------
                // 6) Orientation constraint
                //    Tries to preserve orientation of segments from the initial setup
                // -------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    DVector3 r = EvaluateSingleOrientationResidual(state, i, wOrientation);

                    residual[row++] = r.x;
                    residual[row++] = r.y;
                    residual[row++] = r.z;
                }
            }

            if (wSegmentLength > 0)
            {
                // -------------------------------------------------------------
                // 7) Structural segment length constraint
                //    Allow segments to grow, but discourage excessive shrinking.
                // -------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    residual[row++] = EvaluateSingleSegmentLengthResidual(state, i, wSegmentLength);
                }
            }

            DebugProfiler.DebugMark(timeResidualEvaluate);

            return residual;
        }

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

        Matrix<double> BuildJacobianNavMesh(EDState state, out double jNorm, WeightConfig weights)
        {
            DebugProfiler.DebugMark(timeJacobianBuild);

            // First rows are rotation constraints, then regularization, then positional constraints, then clearance constraints (if enabled)
            // We also compute an estimate of the Jacobian norm while filling it, which can be used for scaling other terms
            // Rotation, regularization, and constraint blocks are calculated analytically, while clearance and slope weight is currently left for numerical differentiation
            jNorm = 0.0;

            int nodeCount = nodes.Count;

            int directedEdgeCount = 0;
            for (int i = 0; i < nodeCount; i++)
                directedEdgeCount += nodes[i].neighbors.Count;

            int constraintCount = vertexConstraints.Count;

            EDResidualLayout layout = BuildResidualLayoutNavMesh(weights);
            int rowCount = layout.totalRows;
            int colCount = 12 * nodeCount;

            var J = DenseMatrix.Create(rowCount, colCount, 0.0);

            double wRot = BuildResidualWeight(weights.rotationWeight, layout.rotationRows, weights.normalizeWeights);
            double wReg = BuildResidualWeight(weights.regularizationWeight, layout.regularizationRows, weights.normalizeWeights);
            double wCon = BuildResidualWeight(weights.constraintWeight, layout.constraintRows, weights.normalizeWeights);
            double wClearance = BuildResidualWeight(weights.clearanceWeight, layout.clearanceRows, weights.normalizeWeights);
            double wSlope = BuildResidualWeight(weights.slopeWeight, layout.slopeRows, weights.normalizeWeights);
            double wOrientation = BuildResidualWeight(weights.orientationWeight, layout.orientationRows, weights.normalizeWeights);
            double wSegmentLength = BuildResidualWeight(weights.segmentLengthWeight, layout.segmentLengthRows, weights.normalizeWeights);

            int row = 0;

            var stateView = new EDStateView(state);

            // Rotation
            for (int i = 0; i < nodeCount; i++)
            {
                row = FillRotationJacobianBlock(stateView, J, row, i, wRot, ref jNorm);
            }

            // Regularization (directed)
            for (int j = 0; j < nodeCount; j++)
            {
                foreach (int k in nodes[j].neighbors)
                {
                    row = FillRegularizationJacobianBlock(stateView, J, row, j, k, wReg, ref jNorm);
                }
            }

            // Constraints
            for (int c = 0; c < constraintCount; c++)
            {
                row = FillConstraintJacobianBlock(stateView, J, row, vertexConstraints[c].vertexIndex, wCon, ref jNorm);
            }

            if (wClearance > 0)
            {
                int clearanceStartRow = row;

                // One slot per segment, summed serially below. Accumulating the worker-local
                // partials in completion order made jNorm vary run-to-run in the low bits,
                // which is enough to stop a diagnostic dump from reproducing.
                double[] clearanceJNorm = new double[structure.Count];

                DebugProfiler.DebugMark(timeJacobianBuildClearance);

                Parallel.For(0, structure.Count, EDDiagnostics.parallelOptions, i =>
                {
                    clearanceJNorm[i] = FillClearanceJacobianRow(state, J, clearanceStartRow + i, i, wClearance);
                });

                DebugProfiler.DebugMark(timeJacobianBuildClearance);

                for (int i = 0; i < clearanceJNorm.Length; i++)
                    jNorm += clearanceJNorm[i];

                row += structure.Count;
            }

            if (wSlope > 0)
            {
                for (int i = 0; i < structure.Count; i++)
                {
                    row = FillSlopeJacobianBlock(state, J, row, i, wSlope, ref jNorm);
                }
            }

            if (wOrientation > 0)
            {
                for (int i = 0; i < structure.Count; i++)
                {
                    row = FillOrientationJacobianBlock(state, J, row, i, wOrientation, ref jNorm);
                }
            }

            if (wSegmentLength > 0)
            {
                for (int i = 0; i < structure.Count; i++)
                {
                    row = FillSegmentLengthJacobianBlock(state, J, row, i, wSegmentLength, ref jNorm);
                }
            }

            jNorm = Math.Sqrt(jNorm);

            DebugProfiler.DebugMark(timeJacobianBuild);

            return J;
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

        private Vector<double> EvaluateResidualVectorStructure(EDStateView state, WeightConfig weights)
        {
            DebugProfiler.DebugMark(timeResidualEvaluate);

            int nodeCount = nodes.Count;

            EDResidualLayout layout = BuildResidualLayoutStructure(weights);
            Vector<double> residual = DenseVector.Create(layout.totalRows, 0.0);

            double wRot = BuildResidualWeight(weights.rotationWeight, layout.rotationRows, weights.normalizeWeights);
            double wReg = BuildResidualWeight(weights.regularizationWeight, layout.regularizationRows, weights.normalizeWeights);
            double wCon = BuildResidualWeight(weights.constraintWeight, layout.constraintRows, weights.normalizeWeights);
            double wClearance = BuildResidualWeight(weights.clearanceWeight, layout.clearanceRows, weights.normalizeWeights);
            double wSlope = BuildResidualWeight(weights.slopeWeight, layout.slopeRows, weights.normalizeWeights);
            double wOrientation = BuildResidualWeight(weights.orientationWeight, layout.orientationRows, weights.normalizeWeights);
            double wSegmentLength = BuildResidualWeight(weights.segmentLengthWeight, layout.segmentLengthRows, weights.normalizeWeights);
            double wLinkAngle = BuildResidualWeight(weights.linkAngleWeight, layout.linkAngleRows, weights.normalizeWeights);
            double wTerminalOrientation = BuildResidualWeight(weights.terminalOrientationWeight, layout.terminalOrientationRows, weights.normalizeWeights);
            double wTerminalScale = BuildResidualWeight(weights.terminalScaleWeight, layout.terminalScaleRows, weights.normalizeWeights);

            int row = 0;

            // -------------------------------------------------------------
            // 1) Rotation residuals
            //    Same as NavMesh path, with the difference that it accepts scaling on terminal nodes
            // -------------------------------------------------------------
            for (int i = 0; i < nodeCount; i++)
            {
                EDNode node = nodes[i];

                DVector3 right = state.TransformVector(i, node.restRight);
                DVector3 up = state.TransformVector(i, node.restUp);
                DVector3 forward = state.TransformVector(i, node.restForward);

                residual[row++] = wRot * DVector3.Dot(right, up);
                residual[row++] = wRot * DVector3.Dot(right, forward);
                residual[row++] = wRot * DVector3.Dot(up, forward);

                bool allowRightScale = HasTerminalScaleConstraint(i);

                // This row remains present for consistent row accounting,
                // but is disabled when scale is controlled by a terminal.
                residual[row++] = (allowRightScale) ? (0.0) : (wRot * (DVector3.Dot(right, right) - 1.0));
                residual[row++] = wRot * (DVector3.Dot(up, up) - 1.0);
                residual[row++] = wRot * (DVector3.Dot(forward, forward) - 1.0);
            }

            // -------------------------------------------------------------
            // 2) Regularization residuals
            //    Same as NavMesh path.
            // -------------------------------------------------------------
            for (int j = 0; j < nodeCount; j++)
            {
                EDNode nodeJ = nodes[j];
                DVector3 gj = nodeJ.restPosition;
                DVector3 tj = state.GetTranslation(j);

                foreach (int k in nodeJ.neighbors)
                {
                    EDNode nodeK = nodes[k];
                    DVector3 gk = nodeK.restPosition;
                    DVector3 tk = state.GetTranslation(k);

                    DVector3 diff = gk - gj;
                    DVector3 rotatedDiff = state.TransformVector(j, diff);

                    DVector3 r = rotatedDiff + gj + tj - (gk + tk);

                    residual[row++] = wReg * r.x;
                    residual[row++] = wReg * r.y;
                    residual[row++] = wReg * r.z;
                }
            }

            // -------------------------------------------------------------
            // 3) Terminal position constraints
            //
            //    deformed terminal node position - current handle position
            // -------------------------------------------------------------
            if (handleConstraints != null)
            {
                for (int c = 0; c < handleConstraints.Count; c++)
                {
                    EDHandleConstraint hc = handleConstraints[c];

                    int handleRowCount = hc.isTerminal ? 6 : 3;

                    if (!TryGetStructureHandleNodeIndex(hc, out int nodeIndex))
                    {
                        row += handleRowCount;
                        continue;
                    }

                    EDNode node = nodes[nodeIndex];

                    if (hc.isTerminal)
                    {
                        GetStructureHandleBarPoints(hc, out DVector3 restLeft, out DVector3 restRight, out DVector3 targetLeft, out DVector3 targetRight);

                        DVector3 deformedLeft = state.DeformVertex(nodeIndex, restLeft, node.restPosition);
                        DVector3 deformedRight = state.DeformVertex(nodeIndex, restRight, node.restPosition);
                        DVector3 leftError = deformedLeft - targetLeft;
                        DVector3 rightError = deformedRight - targetRight;

                        residual[row++] = wCon * leftError.x;
                        residual[row++] = wCon * leftError.y;
                        residual[row++] = wCon * leftError.z;

                        residual[row++] = wCon * rightError.x;
                        residual[row++] = wCon * rightError.y;
                        residual[row++] = wCon * rightError.z;
                    }
                    else
                    {
                        DVector3 restPosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();

                        DVector3 targetPosition = hc.currentHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();

                        DVector3 deformedPosition = state.DeformVertex(nodeIndex, restPosition, node.restPosition);

                        DVector3 error = deformedPosition - targetPosition;

                        residual[row++] = wCon * error.x;
                        residual[row++] = wCon * error.y;
                        residual[row++] = wCon * error.z;
                    }
                }
            }

            if (wClearance > 0)
            {
                for (int i = 0; i < structure.Count; i++)
                {
                    var originalClearance = restState.GetClearance(i);
                    var currentClearance = state.GetClearance(i);

                    residual[row++] = wClearance * ComputeClearanceLoss(originalClearance, currentClearance);
                }
            }

            if (wSlope > 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    residual[row++] = EvaluateSingleNodeSlopeResidualStructure(state, i, wSlope);
                }
            }

            if (wOrientation > 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    DVector3 r = EvaluateSingleNodeOrientationResidualStructure(state, i, wOrientation);

                    residual[row++] = r.x;
                    residual[row++] = r.y;
                    residual[row++] = r.z;
                }
            }

            if (wSegmentLength > 0)
            {
                for (int i = 0; i < structure.Count; i++)
                {
                    residual[row++] = EvaluateSingleSegmentLengthResidualStructure(state, i, wSegmentLength);
                }
            }

            if (wLinkAngle > 0.0)
            {
                for (int i = 0; i < linkAngleConstraints.Count; i++)
                {
                    EvaluateSingleLinkAngleResidual(state, i, wLinkAngle, out double cosineResidual, out double sineResidual);

                    residual[row++] = cosineResidual;
                    residual[row++] = sineResidual;
                }
            }

            if (wTerminalOrientation > 0.0)
            {
                for (int i = 0; i < terminalConstraints.Count; i++)
                {
                    DVector3 r = EvaluateSingleTerminalOrientationResidual(state, i, wTerminalOrientation);

                    residual[row++] = r.x;
                    residual[row++] = r.y;
                    residual[row++] = r.z;
                }
            }

            if (wTerminalScale > 0.0)
            {
                for (int i = 0; i < terminalConstraints.Count; i++)
                {
                    residual[row++] = EvaluateSingleTerminalScaleResidual(state, i, wTerminalScale);
                }
            }

            DebugProfiler.DebugMark(timeResidualEvaluate);

            if (row != layout.totalRows)
            {
                throw new InvalidOperationException($"Structure residual row count mismatch: used={row}, expected={layout.totalRows}");
            }

            return residual;
        }

        Matrix<double> BuildJacobianStructure(EDState state, out double jNorm, WeightConfig weights)
        {
            DebugProfiler.DebugMark(timeJacobianBuild);

            jNorm = 0.0;

            int nodeCount = nodes.Count;

            EDResidualLayout layout = BuildResidualLayoutStructure(weights);

            int rowCount = layout.totalRows;
            int colCount = 12 * nodeCount;

            var J = DenseMatrix.Create(rowCount, colCount, 0.0);

            double wRot = BuildResidualWeight(weights.rotationWeight, layout.rotationRows, weights.normalizeWeights);
            double wReg = BuildResidualWeight(weights.regularizationWeight, layout.regularizationRows, weights.normalizeWeights);
            double wCon = BuildResidualWeight(weights.constraintWeight, layout.constraintRows, weights.normalizeWeights);
            double wClearance = BuildResidualWeight(weights.clearanceWeight, layout.clearanceRows, weights.normalizeWeights);
            double wSlope = BuildResidualWeight(weights.slopeWeight, layout.slopeRows, weights.normalizeWeights);
            double wOrientation = BuildResidualWeight(weights.orientationWeight, layout.orientationRows, weights.normalizeWeights);
            double wSegmentLength = BuildResidualWeight(weights.segmentLengthWeight, layout.segmentLengthRows, weights.normalizeWeights);
            double wLinkAngle = BuildResidualWeight(weights.linkAngleWeight, layout.linkAngleRows,weights.normalizeWeights);
            double wTerminalOrientation = BuildResidualWeight(weights.terminalOrientationWeight, layout.terminalOrientationRows, weights.normalizeWeights);
            double wTerminalScale = BuildResidualWeight(weights.terminalScaleWeight, layout.terminalScaleRows, weights.normalizeWeights);

            int row = 0;

            EDStateView stateView = new EDStateView(state);

            // -------------------------------------------------------------
            // 1) Rotation Jacobian
            //    Same block as NavMesh path, except for allowing for scaling on terminal nodes
            // -------------------------------------------------------------
            for (int i = 0; i < nodeCount; i++)
            {
                bool allowRightScale = (HasTerminalScaleConstraint(i));

                row = FillRotationJacobianBlockStructure(stateView, J, row, i, wRot, allowRightScale, ref jNorm);
            }

            // -------------------------------------------------------------
            // 2) Regularization Jacobian
            //    Same block as NavMesh path.
            // -------------------------------------------------------------
            for (int j = 0; j < nodeCount; j++)
            {
                foreach (int k in nodes[j].neighbors)
                {
                    row = FillRegularizationJacobianBlock(stateView, J, row, j, k, wReg, ref jNorm);
                }
            }

            // -------------------------------------------------------------
            // 3) Terminal position Jacobian
            // -------------------------------------------------------------
            // -------------------------------------------------------------
            // 3) Structure handle position Jacobian
            // -------------------------------------------------------------
            if (handleConstraints != null)
            {
                for (int c = 0; c < handleConstraints.Count; c++)
                {
                    EDHandleConstraint hc = handleConstraints[c];

                    int handleRowCount = hc.isTerminal ? 6 : 3;

                    if (!TryGetStructureHandleNodeIndex(hc, out int nodeIndex))
                    {
                        row += handleRowCount;
                        continue;
                    }

                    if (hc.isTerminal)
                    {
                        GetStructureHandleBarPoints(hc, out DVector3 restLeft, out DVector3 restRight, out _, out _);

                        row = FillNodePositionJacobianBlockStructure(J, row, nodeIndex, restLeft, wCon, ref jNorm);
                        row = FillNodePositionJacobianBlockStructure(J, row, nodeIndex, restRight, wCon, ref jNorm);
                    }
                    else
                    {
                        DVector3 restPosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();
                        row = FillNodePositionJacobianBlockStructure(J, row, nodeIndex, restPosition, wCon, ref jNorm);
                    }
                }
            }

            if (wClearance > 0)
            {
                int clearanceStartRow = row;

                // One slot per segment, summed serially below - see the matching comment
                // in BuildJacobianNavMesh.
                double[] clearanceJNorm = new double[structure.Count];

                int scratchCapacity = Mathf.Min(nodes.Count, 8 * deformationField.maxInfluencesPerCell);

                // Construct the base frames once for the entire Jacobian.
                List<FullDeformationField.Frame> baseNodeFrames = BuildNodeFrames(new EDStateView(state));

                DebugProfiler.DebugMark(timeJacobianBuildClearance);

                Parallel.For(
                    0,
                    structure.Count,
                    EDDiagnostics.parallelOptions,

                    // Each worker receives its own mutable frame list and
                    // trilinear scratch buffers.
                    () => new ClearanceThreadScratch(
                        scratchCapacity,
                        baseNodeFrames
                    ),

                    (segmentIndex, loopState, scratch) =>
                    {
                        int clearanceRow = clearanceStartRow + segmentIndex;

                        clearanceJNorm[segmentIndex] = FillClearanceJacobianRow(state, J, clearanceRow, segmentIndex, wClearance, scratch.nodeFrames);

                        return scratch;
                    },

                    scratch =>
                    {
                        // Nothing to merge - the norms are accumulated per segment above.
                    }
                );

                DebugProfiler.DebugMark(timeJacobianBuildClearance);

                for (int i = 0; i < clearanceJNorm.Length; i++)
                    jNorm += clearanceJNorm[i];

                row += structure.Count;
            }

            if (wSlope > 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    row = FillSlopeJacobianBlockStructure(state, J, row, i, wSlope, ref jNorm);
                }
            }

            if (wOrientation > 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    row = FillOrientationJacobianBlockStructure(state, J, row, i, wOrientation, ref jNorm);
                }
            }

            if (wSegmentLength > 0)
            {
                for (int i = 0; i < structure.Count; i++)
                {
                    row = FillSegmentLengthJacobianBlockStructure(state, J, row, i, wSegmentLength, ref jNorm);
                }
            }

            if (wLinkAngle > 0.0)
            {
                for (int i = 0; i < linkAngleConstraints.Count; i++)
                {
                    row = FillLinkAngleJacobianBlock(state, J, row, i, wLinkAngle, ref jNorm);
                }
            }

            if (wTerminalOrientation > 0.0)
            {
                for (int i = 0; i < terminalConstraints.Count; i++)
                {
                    row = FillTerminalOrientationJacobianBlock(state, J, row, i, wTerminalOrientation, ref jNorm);
                }
            }

            if (wTerminalScale > 0.0)
            {
                for (int i = 0; i < terminalConstraints.Count; i++)
                {
                    row = FillTerminalScaleJacobianBlock(stateView, J, row, i, wTerminalScale, ref jNorm);
                }
            }

            jNorm = Math.Sqrt(jNorm);

            DebugProfiler.DebugMark(timeJacobianBuild);

            if (row != layout.totalRows)
            {
                throw new InvalidOperationException($"Jacobian row count mismatch: used={row}, expected={layout.totalRows}");
            }

            return J;
        }

        #endregion

#endif
    }
}
#endif
