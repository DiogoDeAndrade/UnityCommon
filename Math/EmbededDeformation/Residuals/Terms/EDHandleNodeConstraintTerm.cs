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
    ///
    /// With Pin Terminal Frame on (2026-10-01, off by default) a terminal handle constrains
    /// four points and not two: the bar's ends, and its centre moved along the handle's up and
    /// along its forward. Four points in the handle's own frame are twelve linear rows for the
    /// node's twelve parameters, so the node's map is the handle's - turned as the handle is,
    /// scaled along the bar as the handle asks, and nothing else. The terminal orientation and
    /// scale terms then have nothing left to hold. It was built after those two were each found
    /// asking for the same thing as these rows along a slightly different axis (queue section 31
    /// to 34): the scale term read the node's own right column, 4 degrees off the bar, and the
    /// orientation term reads directions and leaves the columns' lengths free.
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

        // Off is what every run made before 2026-10-01 solved under.
        [SerializeField, Tooltip("Pin each terminal node's whole frame through these rows: beside the two ends of its bar, a terminal handle constrains a point above the bar's centre and a point ahead of it, all four in the handle's own frame. The node's map is then the handle's - turned as the handle is, scaled along the bar as the handle asks - and the terminal orientation and scale terms have nothing left to hold: set them to 1e-12. Read at every solve, so it needs no Build.")]
        protected bool pinTerminalFrame = false;

        public bool pinsTerminalFrame => pinTerminalFrame;

        /// <summary>
        /// Sets Pin Terminal Frame, exactly as ticking the field in the inspector would. The row
        /// count is resolved at the start of every solve, so the next one runs with it. For
        /// experiment drivers such as the schedule runner, as SetConceptualWeight is.
        /// </summary>
        public void SetPinTerminalFrame(bool value) => pinTerminalFrame = value;

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new HandleNodeConstraintInstance(this, deformation);

        public class HandleNodeConstraintInstance : Instance
        {
            // The term as its own type, for the one option it carries beside its weight. Read
            // where it is used and never copied: the option can change between two solves.
            private readonly EDHandleNodeConstraintTerm positionTerm;

            public HandleNodeConstraintInstance(EDHandleNodeConstraintTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                positionTerm = term;
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
                    pointCount += PointCount(deformation.handleConstraints[i]);

                return 3 * pointCount;
            }

            /// <summary>
            /// Root/centre handles constrain one point. Terminal handles constrain both ends of
            /// the handle bar, and two more when they pin the frame.
            /// </summary>
            private int PointCount(EDHandleConstraint hc)
            {
                if (!hc.isTerminal) return 1;

                return (positionTerm.pinTerminalFrame) ? (4) : (2);
            }

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                if (deformation.handleConstraints == null) return;

                double w = residualWeight;
                int row = rowOffset;

                for (int c = 0; c < deformation.handleConstraints.Count; c++)
                {
                    EDHandleConstraint hc = deformation.handleConstraints[c];

                    int handleRowCount = 3 * PointCount(hc);

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

                        if (positionTerm.pinTerminalFrame)
                        {
                            GetHandleFramePoints(hc, out DVector3 restAbove, out DVector3 restAhead, out DVector3 targetAbove, out DVector3 targetAhead);

                            DVector3 aboveError = state.DeformVertex(nodeIndex, restAbove, node.restPosition) - targetAbove;
                            DVector3 aheadError = state.DeformVertex(nodeIndex, restAhead, node.restPosition) - targetAhead;

                            residual[row++] = w * aboveError.x;
                            residual[row++] = w * aboveError.y;
                            residual[row++] = w * aboveError.z;

                            residual[row++] = w * aheadError.x;
                            residual[row++] = w * aheadError.y;
                            residual[row++] = w * aheadError.z;
                        }
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

                    int handleRowCount = 3 * PointCount(hc);

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

                        if (positionTerm.pinTerminalFrame)
                        {
                            GetHandleFramePoints(hc, out DVector3 restAbove, out DVector3 restAhead, out _, out _);

                            row = FillJacobianBlock(jacobian, row, nodeIndex, restAbove, residualWeight, ref jacobianNormSq);
                            row = FillJacobianBlock(jacobian, row, nodeIndex, restAhead, residualWeight, ref jacobianNormSq);
                        }
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
            /// The two points that pin the rest of a terminal's frame, at rest and where the handle
            /// now asks them to be: the bar's centre moved along the handle's up, and along its
            /// forward. With the bar's two ends they are four points that are not in one plane,
            /// which is what it takes to fix an affine map.
            ///
            /// Both are half the bar's rest width from the centre, the distance the bar's ends are
            /// at, so a turn about any of the three axes moves a point by the same amount and the
            /// one weight holds the three alike; a handle without a width uses one unit. The
            /// distance is the same at rest and at the target: the handle's X scale is the width
            /// it asks for and is read by the bar, and its Y and Z scales ask for nothing, so up
            /// and forward keep their length. A degenerate rest axis falls back to the world's,
            /// and a degenerate target axis to the rest direction, as the bar's do.
            /// </summary>
            private static void GetHandleFramePoints(EDHandleConstraint hc, out DVector3 restAbove, out DVector3 restAhead, out DVector3 targetAbove, out DVector3 targetAhead)
            {
                const float epsilon = 1e-8f;

                Vector3 restCenter = hc.restHandleMatrix.MultiplyPoint3x4(Vector3.zero);

                Vector3 targetCenter = hc.currentHandleMatrix.MultiplyPoint3x4(Vector3.zero);

                Vector3 restUp = hc.restHandleMatrix.MultiplyVector(Vector3.up);
                Vector3 restForward = hc.restHandleMatrix.MultiplyVector(Vector3.forward);

                Vector3 targetUp = hc.currentHandleMatrix.MultiplyVector(Vector3.up);
                Vector3 targetForward = hc.currentHandleMatrix.MultiplyVector(Vector3.forward);

                Vector3 restUpDirection = (restUp.magnitude > epsilon) ? (restUp.normalized) : (Vector3.up);
                Vector3 restForwardDirection = (restForward.magnitude > epsilon) ? (restForward.normalized) : (Vector3.forward);

                Vector3 targetUpDirection = (targetUp.magnitude > epsilon) ? (targetUp.normalized) : (restUpDirection);
                Vector3 targetForwardDirection = (targetForward.magnitude > epsilon) ? (targetForward.normalized) : (restForwardDirection);

                float reach = 0.5f * Mathf.Abs(hc.width);

                if (reach <= epsilon) reach = 1.0f;

                restAbove = (restCenter + restUpDirection * reach).ToDVector3();

                restAhead = (restCenter + restForwardDirection * reach).ToDVector3();

                targetAbove = (targetCenter + targetUpDirection * reach).ToDVector3();

                targetAhead = (targetCenter + targetForwardDirection * reach).ToDVector3();
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
