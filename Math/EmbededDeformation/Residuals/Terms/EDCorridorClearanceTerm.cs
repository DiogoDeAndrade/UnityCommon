using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Keeps the navigable corridor either side of each structure node from closing up. The
    /// corridor form of clearance, designed 2026-09-08 (docs/corridor-clearance-design.md): where
    /// EDRadialClearanceTerm reads a radius - the distance from a segment to the nearest
    /// non-opening wall, which conflates the wall ahead with the wall beside and is satisfied by a
    /// path hugging one side of a wide corridor - this one fires the corridor probe both ways
    /// across the node, along the node's own right axis, and holds each side's extent above a
    /// fraction of what it was at rest. Only the walls beside the node are seen, so the wide curve
    /// through a bend, which the radial term charges for the wall ahead, is free here.
    ///
    /// The measurement is EmbededDeformation.TryMeasureCorridor, the probe the field seeding
    /// already uses at rest, given the deformed state: the walls are the navmesh's non-opening
    /// boundary edges, carried through the field (or the bindings) at their vertices, and the
    /// navmesh is already inset by the agent radius, so this is the navigable width. Rest extents
    /// are measured by this instance's own Reset from the built graph - nothing is stored on the
    /// deformation and nothing reaches the golden dump beyond this term's own line.
    ///
    /// Two options are experiment axes rather than decisions, per the design:
    /// - rowForm: PerSide gives two rows per node, one per wall, keeping which side pinched and
    ///   handing Gauss-Newton two directions; Symmetric gives one row on 2 * min(L+, L-), the
    ///   width the seeding uses.
    /// - probeOrigin: ThroughField carries the node's rest position the way the walls are carried,
    ///   so the width holds no blend offset; NodeTransform fires from the node's own map applied
    ///   exactly, the position the graph terms see.
    ///
    /// Like the radial term it has no analytic derivative: the Jacobian row is one finite
    /// difference per parameter, with the same early-out on an unpinched row, the same per-worker
    /// blender and the same override discipline. Unlike the radial term it reads nothing cached on
    /// the state - every evaluation measures, so the residual is a pure function of the state.
    /// Junction nodes (more than two neighbours) contribute no rows: they have no across.
    /// </summary>
    [Serializable]
    [PolymorphicName("Clearance (Corridor)")]
    public class EDCorridorClearanceTerm : EDResidualTerm
    {
        public enum RowForm
        {
            PerSide,
            Symmetric
        }

        public enum ProbeOrigin
        {
            ThroughField,
            NodeTransform
        }

        [SerializeField, Min(0.0f), Tooltip("A side's extent (or the symmetric width) may shrink to this fraction of its rest value before the term objects.")]
        private float minRatio = 0.85f;

        [SerializeField, Tooltip("PerSide: two rows per node, one per wall. Symmetric: one row per node on twice the nearer wall, the width the field seeding uses.")]
        private RowForm rowForm = RowForm.PerSide;

        [SerializeField, Tooltip("ThroughField: the probe starts from the node's rest position carried the way the navmesh walls are carried. NodeTransform: from the node's own transform applied exactly.")]
        private ProbeOrigin probeOrigin = ProbeOrigin.ThroughField;

        [SerializeField, Range(0.0f, 89.0f), Tooltip("How fast the probe's height band opens with distance - a filter on which boundary crossings count as walls. The term's own; it need not equal the builder's or any slope limit.")]
        private float coneAngleDegrees = 45.0f;

        public override string name => "corridorClearance";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new CorridorInstance(this, deformation);

        public class CorridorInstance : Instance
        {
            private readonly EDCorridorClearanceTerm corridorTerm;

            // One entry per row. side is +1 / -1 under PerSide and 0 under Symmetric; rest is the
            // side's rest extent, or the rest symmetric width.
            private int[] rowNode = Array.Empty<int>();
            private int[] rowSide = Array.Empty<int>();
            private double[] rowRest = Array.Empty<double>();

            // The binding a node's rest position deforms through on the binding path (the field
            // path ignores it). Taken from the structure segment that names the node.
            private EDVertexBinding[] nodeBindings = Array.Empty<EDVertexBinding>();
            private bool[] nodeHasBinding = Array.Empty<bool>();

            private int measuredNodes;
            private int skippedJunctions;
            private int unmeasuredNodes;
            private int restUnboundedSides;

            private bool warnedNoBinding;

            public CorridorInstance(EDCorridorClearanceTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                corridorTerm = term;
            }

            /// <summary>
            /// Measures every node's rest corridor and lays out the rows. Runs on construction and
            /// whenever the graph is rebuilt - after BuildNavigationData on every path that reaches
            /// it, which is what makes the rest measurement possible here.
            /// </summary>
            public override void Reset()
            {
                measuredNodes = 0;
                skippedJunctions = 0;
                unmeasuredNodes = 0;
                restUnboundedSides = 0;

                var nodesOut = new List<int>();
                var sidesOut = new List<int>();
                var restOut = new List<double>();

                var nodes = deformation.nodes;
                int nodeCount = (nodes != null) ? (nodes.Count) : (0);

                nodeBindings = new EDVertexBinding[nodeCount];
                nodeHasBinding = new bool[nodeCount];

                if (deformation.structure != null)
                {
                    foreach (var segment in deformation.structure)
                    {
                        if ((segment.node1 >= 0) && (segment.node1 < nodeCount) && (!nodeHasBinding[segment.node1]))
                        {
                            nodeBindings[segment.node1] = segment.bind1;
                            nodeHasBinding[segment.node1] = true;
                        }

                        if ((segment.node2 >= 0) && (segment.node2 < nodeCount) && (!nodeHasBinding[segment.node2]))
                        {
                            nodeBindings[segment.node2] = segment.bind2;
                            nodeHasBinding[segment.node2] = true;
                        }
                    }
                }

                double cone = corridorTerm.coneAngleDegrees;

                if (deformation.isNavConfigured)
                {
                    for (int i = 0; i < nodeCount; i++)
                    {
                        EDNode node = nodes[i];

                        // A junction has no across: restForward there is the average of the outgoing
                        // edges and points along none of them. Same rule as the field seeding.
                        if ((node.neighbors != null) && (node.neighbors.Count > 2))
                        {
                            skippedJunctions++;
                            continue;
                        }

                        if (!deformation.TryMeasureCorridor(node.restPosition, node.restRight, node.restUp, null, null, cone, out EDCorridorExtent rest))
                        {
                            unmeasuredNodes++;
                            continue;
                        }

                        if (!rest.hasPositive) restUnboundedSides++;
                        if (!rest.hasNegative) restUnboundedSides++;

                        if (corridorTerm.rowForm == RowForm.Symmetric)
                        {
                            // An unbounded side drops out of the min; isMeasured guarantees one is
                            // bounded, so this is finite.
                            nodesOut.Add(i);
                            sidesOut.Add(0);
                            restOut.Add(SymmetricWidth(rest));
                        }
                        else
                        {
                            // A side unbounded at rest is not scored: there is no rest extent to hold
                            // a fraction of.
                            if (rest.hasPositive)
                            {
                                nodesOut.Add(i);
                                sidesOut.Add(+1);
                                restOut.Add(rest.positive);
                            }

                            if (rest.hasNegative)
                            {
                                nodesOut.Add(i);
                                sidesOut.Add(-1);
                                restOut.Add(rest.negative);
                            }
                        }

                        measuredNodes++;
                    }
                }

                rowNode = nodesOut.ToArray();
                rowSide = sidesOut.ToArray();
                rowRest = restOut.ToArray();

                if (deformation.isNavConfigured)
                {
                    Debug.Log($"[ED] {term.name}: {rowNode.Length} rows ({corridorTerm.rowForm}, origin {corridorTerm.probeOrigin}) over {measuredNodes} of {nodeCount} nodes - {skippedJunctions} junctions skipped, {unmeasuredNodes} unmeasured, {restUnboundedSides} rest sides unbounded, cone {cone:F1}.");
                }
            }

            /// <summary>
            /// Gated on the navigation data, as the radial term is: without the navmesh there is
            /// nothing to probe, and the term contributes no rows rather than rows of zeros.
            /// </summary>
            protected override int ComputeRowCount()
            {
                if (!deformation.isNavConfigured) return 0;

                return rowNode.Length;
            }

            private static double SymmetricWidth(EDCorridorExtent extent)
            {
                if (!extent.isMeasured) return double.MaxValue;

                return 2.0 * Math.Min(extent.positive, extent.negative);
            }

            /// <summary>
            /// The radial term's loss, unchanged, so the two terms price a pinch the same way and a
            /// weight carries across: a fraction of the rest value lost, with two floors of which
            /// the more permissive wins, and MaxValue on either end scoring zero - "measured
            /// nothing" is not "infinitely narrow".
            /// </summary>
            private double ComputeLoss(double original, double current)
            {
                if ((original == double.MaxValue) || (current == double.MaxValue))
                    return 0.0;

                const double epsilon = 1e-3;
                const double absoluteSlack = 0.05;

                double allowedByRatio = original * corridorTerm.minRatio;
                double allowedBySlack = Math.Max(0.0, original - absoluteSlack);

                double allowed = Math.Min(allowedByRatio, allowedBySlack);

                double loss = (allowed - current) / (original + epsilon);

                return Math.Max(0.0, loss);
            }

            /// <summary>
            /// Where the probe starts at this state. ThroughField carries the rest position along
            /// the same route as the walls it is measured against; NodeTransform applies the node's
            /// own map. On the binding path with no binding for the node - a graph with no structure
            /// segments - the node transform is the only route, and it says so once.
            /// </summary>
            private DVector3 ProbeCenter(int nodeIndex, EDStateView state, FullDeformationField.TransformBlender blender)
            {
                EDNode node = deformation.nodes[nodeIndex];

                if (corridorTerm.probeOrigin == ProbeOrigin.NodeTransform)
                    return state.DeformNodePosition(nodeIndex, node.restPosition);

                if ((blender == null) && (!nodeHasBinding[nodeIndex]))
                {
                    if (!warnedNoBinding)
                    {
                        warnedNoBinding = true;
                        Debug.LogWarning($"[ED] {term.name}: no deformation field and no structure binding for node {nodeIndex}, so the probe origin falls back to the node's own transform for such nodes.");
                    }

                    return state.DeformNodePosition(nodeIndex, node.restPosition);
                }

                return deformation.DeformClearancePoint(node.restPosition, nodeBindings[nodeIndex], state, blender);
            }

            /// <summary>
            /// The deformed value a row compares against its rest value: one side's extent, or the
            /// symmetric width. MaxValue when the probe finds no wall there - the wall went away,
            /// which is not a pinch.
            /// </summary>
            private double MeasureCurrent(int row, EDStateView state, FullDeformationField.TransformBlender blender)
            {
                int nodeIndex = rowNode[row];
                EDNode node = deformation.nodes[nodeIndex];

                DVector3 center = ProbeCenter(nodeIndex, state, blender);
                DVector3 across = state.TransformDirection(nodeIndex, node.restRight);
                DVector3 up = state.TransformDirection(nodeIndex, node.restUp);

                if (!deformation.TryMeasureCorridor(center, across, up, state, blender, corridorTerm.coneAngleDegrees, out EDCorridorExtent extent))
                    return double.MaxValue;

                switch (rowSide[row])
                {
                    case +1: return extent.positive;
                    case -1: return extent.negative;
                    default: return SymmetricWidth(extent);
                }
            }

            private double EvaluateRow(int row, EDStateView state, double w, FullDeformationField.TransformBlender blender)
                => w * ComputeLoss(rowRest[row], MeasureCurrent(row, state, blender));

            private FullDeformationField.TransformBlender BlenderFor(EDStateView state)
                => (deformation.UseDeformationFieldForClearance) ? (deformation.CreateFieldBlender(state)) : (null);

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = residualWeight;

                // One blender for the whole pass: it freezes the frames once and is read-only after.
                var blender = BlenderFor(state);

                for (int i = 0; i < rowNode.Length; i++)
                    residual[rowOffset + i] = EvaluateRow(i, state, w, blender);
            }

            public override bool supportsParallelRows => true;

            /// <summary>
            /// One blender per worker - the finite-difference path overrides the perturbed node's
            /// frame and measures, so workers sharing one would overwrite each other's
            /// perturbations. Null when nothing is carried through the field.
            /// </summary>
            public override object CreateRowScratch(EDState state)
            {
                if (!deformation.UseDeformationFieldForClearance) return null;

                return deformation.CreateFieldBlender(new EDStateView(state));
            }

            public override double FillJacobianRow(EDState state, DenseMatrix jacobian, int rowOffset, int localIndex, object scratch)
                => FillRow(state, jacobian, rowOffset + localIndex, localIndex, residualWeight, scratch as FullDeformationField.TransformBlender);

            /// <summary>
            /// One row by one finite difference per parameter, every parameter: the walls are carried
            /// by the whole graph, so no column can be argued to be zero in advance. The early-out on
            /// an unpinched row is what makes it affordable - on a solved piece most rows cost one
            /// measurement. The override is cleared in a finally, as in the radial term: clearing is
            /// total, a Set left standing would poison every column after it.
            /// </summary>
            private double FillRow(EDState state, DenseMatrix J, int row, int localIndex, double w, FullDeformationField.TransformBlender blender)
            {
                var baseView = new EDStateView(state);

                if ((deformation.UseDeformationFieldForClearance) && (blender == null))
                    blender = deformation.CreateFieldBlender(baseView);

                double r0 = EvaluateRow(localIndex, baseView, w, blender);

                if (Math.Abs(r0) <= 1e-12) return 0.0;

                double localJNorm = 0.0;

                for (int col = 0; col < state.Count; col++)
                {
                    double originalParameter = state.Get(col);

                    double eps = 1e-6 * Math.Max(1.0, Math.Abs(originalParameter));

                    var modifiedState = new EDStateView(state, col, eps);

                    if (blender != null)
                        blender.SetNodeOverride(col / 12, deformation.GetNodeFrame(col / 12, modifiedState));

                    double r1;

                    try
                    {
                        r1 = EvaluateRow(localIndex, modifiedState, w, blender);
                    }
                    finally
                    {
                        blender?.ClearNodeOverride();
                    }

                    double value = (r1 - r0) / eps;

                    J[row, col] = value;
                    localJNorm += value * value;
                }

                return localJNorm;
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                object scratch = CreateRowScratch(state);

                for (int i = 0; i < rowCount; i++)
                    jacobianNormSq += FillJacobianRow(state, jacobian, rowOffset, i, scratch);
            }

            public override string[] DescribeHeader()
                => new[] { "nodesMeasured", "sidesPinched", "worstWidthRatio", "unboundedSides" };

            /// <summary>
            /// Re-measures every row at the state: how many rows are below their floor, the smallest
            /// deformed-over-rest ratio (how far the narrowest place has closed), and how many rows
            /// found no wall at all. Costs one residual evaluation.
            /// </summary>
            public override string[] Describe(EDStateView state)
            {
                int pinched = 0;
                int unbounded = 0;
                double worstRatio = double.PositiveInfinity;

                var blender = BlenderFor(state);

                for (int i = 0; i < rowNode.Length; i++)
                {
                    double current = MeasureCurrent(i, state, blender);

                    if (current == double.MaxValue)
                    {
                        unbounded++;
                        continue;
                    }

                    if (ComputeLoss(rowRest[i], current) > 0.0) pinched++;

                    double ratio = current / Math.Max(rowRest[i], 1e-12);

                    if (ratio < worstRatio) worstRatio = ratio;
                }

                return new[]
                {
                    measuredNodes.ToString(CultureInfo.InvariantCulture),
                    pinched.ToString(CultureInfo.InvariantCulture),
                    (double.IsPositiveInfinity(worstRatio)) ? ("") : (worstRatio.ToString("F4", CultureInfo.InvariantCulture)),
                    unbounded.ToString(CultureInfo.InvariantCulture)
                };
            }
        }
#endif
    }
}
#endif
