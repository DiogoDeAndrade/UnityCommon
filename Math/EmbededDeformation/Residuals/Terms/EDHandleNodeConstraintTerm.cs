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
    /// The structure-graph counterpart of the vertex constraint. There is no mesh to bind to here,
    /// so a handle drives the nearest structure node directly rather than a set of navmesh vertices.
    ///
    /// The row count is the one thing about this term that is not a multiple of anything: a centre
    /// handle constrains its single point, but a terminal handle constrains both ends of its bar, so
    /// that its width and its orientation are pinned along with its position. Three rows per
    /// constrained *point*, not per constraint.
    ///
    /// A handle whose rest position finds no node still occupies its rows, left at zero. Dropping
    /// them instead would make the layout depend on how well the handles happen to line up with the
    /// graph, which is exactly the kind of quiet coupling the row count should not have.
    /// </summary>
    [Serializable]
    [PolymorphicName("Terminal Position (Structure Nodes)")]
    public class EDHandleNodeConstraintTerm : EDResidualTerm
    {
        // Renamed from "constraint" on 2026-08-27, in step with the navmesh form - the two
        // share the name because they are the same row in the two layouts. The class name
        // deliberately did not move: [SerializeReference] persists it, and a renamed class
        // detaches every energy asset that carries the term.
        public override string name => "terminalPosition";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new HandleNodeConstraintInstance(this, deformation);

        public class HandleNodeConstraintInstance : Instance
        {
            public HandleNodeConstraintInstance(EDHandleNodeConstraintTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            /// <summary>
            /// Three rows per constrained *point*, and a terminal handle constrains two of them -
            /// both ends of its bar, so that the connector's width and its orientation are pinned
            /// along with its position. A centre handle constrains one.
            /// </summary>
            protected override int ComputeRowCount()
            {
                if (deformation.handleConstraints == null)
                    return 0;

                int pointCount = 0;

                for (int i = 0; i < deformation.handleConstraints.Count; i++)
                {
                    // Root/centre handles constrain one point.
                    // Terminal handles constrain both ends of the handle bar.
                    pointCount += deformation.handleConstraints[i].isTerminal ? 2 : 1;
                }

                return 3 * pointCount;
            }

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                if (deformation.handleConstraints == null) return;

                double w = residualWeight;
                int row = rowOffset;

                for (int c = 0; c < deformation.handleConstraints.Count; c++)
                {
                    EDHandleConstraint hc = deformation.handleConstraints[c];

                    int handleRowCount = (hc.isTerminal) ? (6) : (3);

                    if (!TryGetHandleNodeIndex(hc, out int nodeIndex))
                    {
                        row += handleRowCount;
                        continue;
                    }

                    EDNode node = deformation.nodes[nodeIndex];

                    if (hc.isTerminal)
                    {
                        GetHandleBarPoints(hc, out DVector3 restLeft, out DVector3 restRight, out DVector3 targetLeft, out DVector3 targetRight);

                        DVector3 deformedLeft = state.DeformVertex(nodeIndex, restLeft, node.restPosition);
                        DVector3 deformedRight = state.DeformVertex(nodeIndex, restRight, node.restPosition);

                        DVector3 leftError = deformedLeft - targetLeft;
                        DVector3 rightError = deformedRight - targetRight;

                        residual[row++] = w * leftError.x;
                        residual[row++] = w * leftError.y;
                        residual[row++] = w * leftError.z;

                        residual[row++] = w * rightError.x;
                        residual[row++] = w * rightError.y;
                        residual[row++] = w * rightError.z;
                    }
                    else
                    {
                        DVector3 restPosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();
                        DVector3 targetPosition = hc.currentHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();

                        DVector3 deformedPosition = state.DeformVertex(nodeIndex, restPosition, node.restPosition);

                        DVector3 error = deformedPosition - targetPosition;

                        residual[row++] = w * error.x;
                        residual[row++] = w * error.y;
                        residual[row++] = w * error.z;
                    }
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                if (deformation.handleConstraints == null) return;

                int row = rowOffset;

                for (int c = 0; c < deformation.handleConstraints.Count; c++)
                {
                    EDHandleConstraint hc = deformation.handleConstraints[c];

                    int handleRowCount = (hc.isTerminal) ? (6) : (3);

                    if (!TryGetHandleNodeIndex(hc, out int nodeIndex))
                    {
                        row += handleRowCount;
                        continue;
                    }

                    if (hc.isTerminal)
                    {
                        GetHandleBarPoints(hc, out DVector3 restLeft, out DVector3 restRight, out _, out _);

                        row = FillJacobianBlock(jacobian, row, nodeIndex, restLeft, residualWeight, ref jacobianNormSq);
                        row = FillJacobianBlock(jacobian, row, nodeIndex, restRight, residualWeight, ref jacobianNormSq);
                    }
                    else
                    {
                        DVector3 restPosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero).ToDVector3();

                        row = FillJacobianBlock(jacobian, row, nodeIndex, restPosition, residualWeight, ref jacobianNormSq);
                    }
                }
            }

            /// <summary>
            /// Which graph node a handle drives. A connector handle takes the nearest *leaf*, since
            /// a terminal is what a connector attaches to; a centre handle takes the nearest node of
            /// any kind.
            ///
            /// Resolved on every call rather than cached. The matching rule has changed underneath a
            /// cache of exactly this before and left a configuration holding stale links - and the
            /// walk is cheaper than the bug was.
            /// </summary>
            private bool TryGetHandleNodeIndex(EDHandleConstraint hc, out int nodeIndex)
            {
                nodeIndex = -1;

                if ((deformation.nodes == null) || (deformation.nodes.Count == 0))
                    return false;

                Vector3 restHandlePosition = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero);

                if (hc.isTerminal)
                {
                    // Connector handles target terminal structure endpoints.
                    nodeIndex = deformation.GetClosestLeafNodeIndex(restHandlePosition);
                }
                else
                {
                    // Centre/root handles target the corresponding structure node.
                    nodeIndex = deformation.GetClosestDebugNodeIndex(restHandlePosition);
                }

                return (nodeIndex >= 0) && (nodeIndex < deformation.nodes.Count);
            }

            /// <summary>
            /// The two ends of a terminal handle's bar, at rest and where the handle now asks them
            /// to be.
            ///
            /// The requested width comes from the handle's X scale relative to its rest scale rather
            /// than from the target matrix's own width, which is what lets a connector be widened by
            /// scaling the handle rather than by editing a number. A degenerate rest axis falls back
            /// to world right, and a degenerate target axis to the rest direction, so a collapsed
            /// handle still produces a bar rather than two coincident points.
            /// </summary>
            private static void GetHandleBarPoints(EDHandleConstraint hc, out DVector3 restLeft, out DVector3 restRight, out DVector3 targetLeft, out DVector3 targetRight)
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

            /// <summary>
            /// The three rows for one constrained point, analytically. The point is carried by one
            /// node alone rather than by a binding, so unlike the vertex constraint's block this
            /// assigns rather than accumulates - there is nothing else that can write these columns.
            ///
            /// A point whose node index is out of range advances three rows and writes nothing,
            /// matching the residual, which leaves the same rows at zero.
            /// </summary>
            private int FillJacobianBlock(DenseMatrix J, int row, int nodeIndex, DVector3 restPoint, double wCon, ref double jNormRunningTotalSq)
            {
                if ((nodeIndex < 0) || (nodeIndex >= deformation.nodes.Count))
                {
                    return row + 3;
                }

                int p = EDStateView.ParamBase(nodeIndex);

                DVector3 localOffset = restPoint - deformation.nodes[nodeIndex].restPosition;

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
        }
#endif
    }
}
#endif
