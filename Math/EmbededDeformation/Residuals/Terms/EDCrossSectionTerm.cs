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
    /// Keeps neighbouring cross-sections of the corridor from closing on each other. Diogo's
    /// design of 2026-09-12 (docs/experiment-queue.md §15), after the bend compression term (§14)
    /// showed that the inner wall the eye sees folding is not the offset band of the centreline:
    /// each node's seed bar - the corridor width the field's weights were seeded along - is the
    /// node's cross-section, and where two neighbouring bars cross, the wall between them has
    /// length zero. So the row is a bar *end*, carried to the state, measured as a signed distance
    /// against the neighbouring node's cross-section *plane* (through its centre, spanned by its
    /// bar and its up, the normal oriented toward this node at rest) - positive on this node's side,
    /// zero on the plane, negative once crossed - held above a fraction of what it was at rest.
    ///
    /// The plane, not the neighbouring bar's segment or line, for two reasons: a point-to-line
    /// distance in three dimensions has no sign, and the plane reads both ways the corridor fails.
    /// Bars fanning against each other bring the inner end through the plane; bars staying parallel
    /// while the path shears bring the *whole* section toward the plane, since the distance is the
    /// path segment projected on the bar's normal, which vanishes as the shear reaches a right
    /// angle. Menger curvature of the node positions (§14) reads neither, because it never looks at
    /// the frames.
    ///
    /// Four rows per link between two barred nodes: each bar's two ends against the other bar's
    /// plane. A hinge is zero when satisfied, so this costs nothing where nothing is wrong and
    /// hands Gauss-Newton every direction rather than the minimum's one. An interior node therefore
    /// carries four rows, a terminal two (one link), a junction none (no bar - a junction has no
    /// across, the same rule as the field seeding and the corridor terms).
    ///
    /// The hinge is normalised by the row's rest distance (about the node spacing on a straight
    /// corridor), has its floor at minRatio of it rather than at zero - a hinge at zero fires only
    /// once the crossing exists, which is why the bend term arrived after the fold - and ramps in
    /// quadratically over softBand of the rest distance, so the residual is C1 at the floor. The
    /// bend term at weight 1000 stalled LM with rows sitting exactly on its kink; a Gauss-Newton
    /// model built on one side of a kink predicts a descent the step does not deliver.
    ///
    /// endpointCarrier is an experiment axis, as probeOrigin is on the corridor term: ThroughField
    /// carries the bar ends and the neighbour's centre and bar through the deformation field - where
    /// the geometry between the nodes actually goes - and NodeTransform applies each node's own map
    /// exactly, which is affine in the node's parameters. The two differ by the blend's drift, which
    /// the graph-width gizmos draw. The field form's Jacobian is the corridor term's: one finite
    /// difference per parameter with the early-out on a satisfied row; the node form's touches only
    /// the two nodes' columns. The bar lengths are read off the field's seed nodes, never
    /// recomputed, times the builder's corridor width scale.
    /// </summary>
    [Serializable]
    [PolymorphicName("Cross-Section Spacing")]
    public class EDCrossSectionTerm : EDResidualTerm
    {
        public enum EndpointCarrier
        {
            ThroughField,
            NodeTransform
        }

        [SerializeField, Range(0.0f, 1.0f), Tooltip("A bar end's signed distance to the neighbouring cross-section plane may fall to this fraction of its rest distance before the term objects. Zero is the crossing itself; the floor sits above it so the term acts before the wall is gone.")]
        private float minRatio = 0.5f;

        [SerializeField, Range(0.0f, 1.0f), Tooltip("Width of the quadratic ramp below the floor, as a fraction of the rest distance, so the residual is smooth where the hinge opens. Zero is a plain hinge - and a plain hinge with rows sitting on it stalls LM, which is what the bend term did at weight 1000.")]
        private float softBand = 0.1f;

        [SerializeField, Tooltip("ThroughField: bar ends and the neighbour's centre and bar are carried through the deformation field, where the geometry between the nodes goes. NodeTransform: each node's own map applied exactly - cheaper, affine in the node's parameters, and blind to the blend's drift.")]
        private EndpointCarrier endpointCarrier = EndpointCarrier.ThroughField;

        [SerializeField, Min(1e-9f), Tooltip("Relative step of the finite-difference Jacobian. 1e-3, as the quality terms use, not the corridor term's 1e-6: through the field the carried points are float, and at 1e-6 a point a few units from its node moves by about one ulp, which is quantisation rather than a derivative. The first run at weight 1000 stalled LM with the term nearly satisfied, which is what a noisy Jacobian at a large weight looks like.")]
        private float finiteDifferenceStep = 1e-3f;

        public override string name => "crossSection";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new CrossSectionInstance(this, deformation);

        public class CrossSectionInstance : Instance
        {
            private readonly EDCrossSectionTerm crossTerm;

            // One entry per row: the node whose bar end is measured, which end (+1 / -1 along the
            // node's rest right), the neighbour whose plane it is measured against, the rest signed
            // distance, and the sign that orients the neighbour's normal toward this node at rest.
            private int[]    rowNode      = Array.Empty<int>();
            private int[]    rowEnd       = Array.Empty<int>();
            private int[]    rowNeighbour = Array.Empty<int>();
            private double[] rowRest      = Array.Empty<double>();
            private double[] rowSign      = Array.Empty<double>();

            // Per node: the seed bar's half-length times the width scale; zero means no bar.
            private double[] nodeHalf = Array.Empty<double>();

            private bool hasField;
            private int  barredNodes;
            private int  pointNodes;
            private int  skippedJunctionLinks;
            private int  droppedAtRest;

            private bool warnedNoField;

            public CrossSectionInstance(EDCrossSectionTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                crossTerm = term;
            }

            /// <summary>
            /// Reads every node's bar off the field and lays out the rows. Runs on construction and
            /// whenever the graph is rebuilt - after the field is built on every path that reaches
            /// it, which is what makes the bar lengths readable here.
            /// </summary>
            public override void Reset()
            {
                barredNodes = 0;
                pointNodes = 0;
                skippedJunctionLinks = 0;
                droppedAtRest = 0;

                var nodes = deformation.nodes;
                int nodeCount = (nodes != null) ? (nodes.Count) : (0);
                var field = deformation.GetDeformationField();

                hasField = (field != null) && (field.deformationNodeCount >= nodeCount) && (nodeCount > 0);

                nodeHalf = new double[nodeCount];

                var nodesOut = new List<int>();
                var endsOut = new List<int>();
                var neighboursOut = new List<int>();
                var restOut = new List<double>();
                var signOut = new List<double>();

                if (hasField)
                {
                    double scale = deformation.effectiveCorridorWidthScale;

                    for (int i = 0; i < nodeCount; i++)
                    {
                        EDNode node = nodes[i];

                        if ((node.neighbors != null) && (node.neighbors.Count > 2)) continue;   // a junction: no across, no bar

                        float sourceLength = field.GetDeformationNode(i).sourceLength;

                        if (sourceLength <= 0.0f)
                        {
                            pointNodes++;
                            continue;
                        }

                        nodeHalf[i] = 0.5 * sourceLength * scale;
                        barredNodes++;
                    }

                    for (int i = 0; i < nodeCount; i++)
                    {
                        if (nodeHalf[i] <= 0.0) continue;

                        EDNode node = nodes[i];

                        if (node.neighbors == null) continue;

                        foreach (int j in node.neighbors)
                        {
                            if ((j < 0) || (j >= nodeCount) || (j == i)) continue;

                            EDNode other = nodes[j];

                            if ((other.neighbors != null) && (other.neighbors.Count > 2))
                            {
                                skippedJunctionLinks++;
                                continue;
                            }

                            if (nodeHalf[j] <= 0.0) continue;   // a point-seeded neighbour has no plane to measure against

                            // The neighbour's rest plane, its normal oriented toward this node.
                            DVector3 normal = RestNormal(j);
                            double toward = DVector3.Dot(node.restPosition - other.restPosition, normal);

                            if ((normal.sqrMagnitude < 0.5) || (Math.Abs(toward) <= 1e-9))
                            {
                                droppedAtRest += 2;
                                continue;
                            }

                            double sign = (toward > 0.0) ? (1.0) : (-1.0);

                            for (int end = 1; end >= -1; end -= 2)
                            {
                                double rest = sign * DVector3.Dot(RestEnd(i, end) - other.restPosition, normal);

                                // A bar end already on or past the neighbour's plane at rest is not a
                                // wall to hold: nothing to hold a fraction of.
                                if (rest <= 1e-6)
                                {
                                    droppedAtRest++;
                                    continue;
                                }

                                nodesOut.Add(i);
                                endsOut.Add(end);
                                neighboursOut.Add(j);
                                restOut.Add(rest);
                                signOut.Add(sign);
                            }
                        }
                    }
                }

                rowNode = nodesOut.ToArray();
                rowEnd = endsOut.ToArray();
                rowNeighbour = neighboursOut.ToArray();
                rowRest = restOut.ToArray();
                rowSign = signOut.ToArray();

                if (hasField)
                {
                    string scaleNote = (deformation.effectiveCorridorWidthScale != 1.0) ? ($", corridor width scaled x{deformation.effectiveCorridorWidthScale.ToString("F3", CultureInfo.InvariantCulture)}") : ("");

                    Debug.Log($"[ED] {term.name}: {rowNode.Length} rows over {barredNodes} barred nodes ({pointNodes} point-seeded nodes and {skippedJunctionLinks} junction links skipped, {droppedAtRest} rows dropped as crossing at rest); carrier {crossTerm.endpointCarrier}, floor {crossTerm.minRatio.ToString("F2", CultureInfo.InvariantCulture)} of rest, band {crossTerm.softBand.ToString("F2", CultureInfo.InvariantCulture)}, FD step {crossTerm.finiteDifferenceStep.ToString("G3", CultureInfo.InvariantCulture)}{scaleNote}.");
                }
                else if (nodeCount > 0)
                {
                    Debug.LogWarning($"[ED] {term.name}: no deformation field to read the seed bars off - the term contributes no rows on this configuration.");
                }
            }

            protected override int ComputeRowCount() => (hasField) ? (rowNode.Length) : (0);

            // ---------------------------------------------------------------- rest geometry

            private DVector3 RestEnd(int nodeIndex, int end)
            {
                EDNode node = deformation.nodes[nodeIndex];

                return node.restPosition + node.restRight.normalized * (end * nodeHalf[nodeIndex]);
            }

            private DVector3 RestNormal(int nodeIndex)
            {
                EDNode node = deformation.nodes[nodeIndex];
                DVector3 n = DVector3.Cross(node.restRight, node.restUp);

                return (n.sqrMagnitude < 1e-18) ? (DVector3.zero) : (n.normalized);
            }

            // ---------------------------------------------------------------- geometry at a state

            /// <summary>
            /// A rest point carried to the state: through the field when a blender is given, else by
            /// the node's own map - the rest offset from the node through its linear part, plus its
            /// translation, which is the debug view's construction of the bar.
            /// </summary>
            private DVector3 Carry(int nodeIndex, DVector3 restPoint, EDStateView state, FullDeformationField.TransformBlender blender)
            {
                if (blender != null)
                    return blender.DeformPosition(restPoint.ToVector3(), trilinear: true).ToDVector3();

                EDNode node = deformation.nodes[nodeIndex];

                return node.restPosition + state.TransformOffset(nodeIndex, restPoint - node.restPosition);
            }

            /// <summary>
            /// The neighbour's cross-section plane at the state: its centre and bar carried like the
            /// bar ends (so the plane is where the section's walls go), its up from the node's own
            /// map in either mode - the up only tilts the plane, and a point lifted above the floor
            /// has no business being carried through a field measured along the floor.
            /// </summary>
            private void PlaneAt(int nodeIndex, EDStateView state, FullDeformationField.TransformBlender blender, out DVector3 centre, out DVector3 normal, out DVector3 bar)
            {
                EDNode node = deformation.nodes[nodeIndex];
                DVector3 up = state.TransformDirection(nodeIndex, node.restUp);

                if (blender != null)
                {
                    centre = Carry(nodeIndex, node.restPosition, state, blender);
                    bar = Carry(nodeIndex, RestEnd(nodeIndex, +1), state, blender) - Carry(nodeIndex, RestEnd(nodeIndex, -1), state, blender);
                }
                else
                {
                    centre = state.DeformNodePosition(nodeIndex, node.restPosition);
                    bar = state.TransformDirection(nodeIndex, node.restRight);
                }

                DVector3 n = DVector3.Cross(bar, up);

                normal = (n.sqrMagnitude < 1e-18) ? (DVector3.zero) : (n.normalized);
            }

            /// <summary>
            /// The row's signed distance at the state: positive on this node's side of the
            /// neighbour's plane, negative once crossed. A collapsed plane reads as zero - a
            /// cross-section with no normal is a fold, not an open corridor.
            /// </summary>
            private double Distance(int row, EDStateView state, FullDeformationField.TransformBlender blender)
            {
                DVector3 end = Carry(rowNode[row], RestEnd(rowNode[row], rowEnd[row]), state, blender);

                PlaneAt(rowNeighbour[row], state, blender, out DVector3 centre, out DVector3 normal, out _);

                if (normal.sqrMagnitude < 0.5) return 0.0;

                return rowSign[row] * DVector3.Dot(end - centre, normal);
            }

            /// <summary>
            /// Zero at or above the floor; a quadratic ramp over the band below it; linear beyond.
            /// The value and its slope are continuous at the floor, which is the whole point of the
            /// band - see the class comment on the bend term's stall.
            /// </summary>
            private double Hinge(double distance, double rest)
            {
                double violation = crossTerm.minRatio - distance / rest;
                double band = crossTerm.softBand;

                if (violation <= 0.0) return 0.0;
                if (band <= 1e-9) return violation;
                if (violation < band) return violation * violation / (2.0 * band);

                return violation - 0.5 * band;
            }

            private double EvaluateRow(int row, EDStateView state, double w, FullDeformationField.TransformBlender blender)
                => w * Hinge(Distance(row, state, blender), rowRest[row]);

            private bool throughField => (crossTerm.endpointCarrier == EndpointCarrier.ThroughField) && (deformation.UseDeformationFieldForClearance);

            private FullDeformationField.TransformBlender BlenderFor(EDStateView state)
            {
                if (crossTerm.endpointCarrier == EndpointCarrier.ThroughField)
                {
                    if (deformation.UseDeformationFieldForClearance) return deformation.CreateFieldBlender(state);

                    if (!warnedNoField)
                    {
                        warnedNoField = true;
                        Debug.LogWarning($"[ED] {term.name}: ThroughField asked for but nothing deforms through the field here - the node transform carries the bars instead.");
                    }
                }

                return null;
            }

            // ---------------------------------------------------------------- residual and Jacobian

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = residualWeight;
                var blender = BlenderFor(state);

                for (int i = 0; i < rowNode.Length; i++)
                    residual[rowOffset + i] = EvaluateRow(i, state, w, blender);
            }

            public override bool supportsParallelRows => true;

            /// <summary>One blender per worker, as on the corridor term: the finite-difference path overrides a node's frame.</summary>
            public override object CreateRowScratch(EDState state)
            {
                if (!throughField) return null;

                return deformation.CreateFieldBlender(new EDStateView(state));
            }

            public override double FillJacobianRow(EDState state, DenseMatrix jacobian, int rowOffset, int localIndex, object scratch)
                => FillRow(state, jacobian, rowOffset + localIndex, localIndex, residualWeight, scratch as FullDeformationField.TransformBlender);

            /// <summary>
            /// One finite difference per parameter. Through the field every column can matter, since
            /// the carried points blend several nodes, and the early-out on a satisfied row is what
            /// makes that affordable; by the node transform only the two nodes' columns can, and the
            /// rest are skipped exactly. The override is cleared in a finally, as everywhere else.
            /// </summary>
            private double FillRow(EDState state, DenseMatrix J, int row, int localIndex, double w, FullDeformationField.TransformBlender blender)
            {
                var baseView = new EDStateView(state);

                if ((throughField) && (blender == null))
                    blender = deformation.CreateFieldBlender(baseView);

                double r0 = EvaluateRow(localIndex, baseView, w, blender);

                if (Math.Abs(r0) <= 1e-12) return 0.0;

                int nodeA = rowNode[localIndex];
                int nodeB = rowNeighbour[localIndex];
                double localJNorm = 0.0;

                for (int col = 0; col < state.Count; col++)
                {
                    int node = col / 12;

                    if ((blender == null) && (node != nodeA) && (node != nodeB)) continue;

                    double originalParameter = state.Get(col);
                    double eps = crossTerm.finiteDifferenceStep * Math.Max(1.0, Math.Abs(originalParameter));

                    var modifiedState = new EDStateView(state, col, eps);

                    if (blender != null)
                        blender.SetNodeOverride(node, deformation.GetNodeFrame(node, modifiedState));

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

            // ---------------------------------------------------------------- columns

            public override string[] DescribeHeader()
                => new[] { "rowsMeasured", "minDistanceRatio", "minRatioNode", "rowsBelowFloor", "rowsCrossed", "meanDistanceRatio" };

            /// <summary>
            /// Every row's deformed-over-rest signed distance at the state, whatever the weight: the
            /// smallest ratio and the node whose bar end it is (negative means that end has crossed
            /// its neighbour's plane), how many rows are below the floor, how many have crossed, and
            /// the mean. Costs one residual evaluation.
            /// </summary>
            public override string[] Describe(EDStateView state)
            {
                if ((!hasField) || (rowNode.Length == 0)) return Array.Empty<string>();

                var blender = BlenderFor(state);

                double min = double.PositiveInfinity, sum = 0.0;
                int minNode = -1, below = 0, crossed = 0;

                for (int i = 0; i < rowNode.Length; i++)
                {
                    double ratio = Distance(i, state, blender) / rowRest[i];

                    sum += ratio;

                    if (ratio < crossTerm.minRatio) below++;
                    if (ratio <= 0.0) crossed++;
                    if (ratio < min) { min = ratio; minNode = rowNode[i]; }
                }

                return new[]
                {
                    rowNode.Length.ToString(CultureInfo.InvariantCulture),
                    min.ToString("F4", CultureInfo.InvariantCulture),
                    minNode.ToString(CultureInfo.InvariantCulture),
                    below.ToString(CultureInfo.InvariantCulture),
                    crossed.ToString(CultureInfo.InvariantCulture),
                    (sum / rowNode.Length).ToString("F4", CultureInfo.InvariantCulture),
                };
            }

            // ---------------------------------------------------------------- per-row dump

            /// <summary>
            /// Every row at a state, one line each, tab-separated, worst ratio first - the column
            /// names a node and not a row, and the question the dump exists to answer is which of
            /// a node's rows is low and why. Per row: the owning node and which end (+1 along its
            /// rest right, -1 against it), the neighbour whose plane it is measured against, the
            /// rest distance, the distance at the state, their ratio, the unweighted hinge value,
            /// and then the anatomy of the distance: <c>pathPart</c> is the neighbour-to-node path
            /// vector projected on the neighbour's normal and <c>barPart</c> the rest, the bar's
            /// own half-length times the sine of its tilt out of the neighbour's plane - the
            /// component that grows with width. <c>restAlong</c> / <c>along</c> are where the end's
            /// perpendicular foot lands along the neighbour's bar, as a fraction of that bar's
            /// half-length (beyond +-1 the foot is past the neighbour's bar end, on the extended
            /// plane), at rest and at the state. Then both half-lengths, the node spacing at rest
            /// and at the state, and the owning node's rest position for finding it in the scene.
            /// The header line carries the totals and the term's settings. Costs one residual
            /// evaluation plus a few products per row.
            /// </summary>
            public bool TryDumpRows(EDStateView state, string path, out int written)
            {
                written = 0;

                if ((!hasField) || (rowNode.Length == 0)) return false;

                var blender = BlenderFor(state);
                var ci = CultureInfo.InvariantCulture;
                int n = rowNode.Length;

                var ratio    = new double[n];
                var distance = new double[n];
                var pathPart = new double[n];
                var restAlong = new double[n];
                var along    = new double[n];
                var spacing  = new double[n];
                var order    = new int[n];

                for (int i = 0; i < n; i++)
                {
                    int a = rowNode[i], b = rowNeighbour[i];
                    EDNode nodeA = deformation.nodes[a], nodeB = deformation.nodes[b];

                    DVector3 restEnd = RestEnd(a, rowEnd[i]);
                    DVector3 end = Carry(a, restEnd, state, blender);
                    DVector3 centreA = Carry(a, nodeA.restPosition, state, blender);

                    PlaneAt(b, state, blender, out DVector3 centreB, out DVector3 normal, out DVector3 bar);

                    bool collapsed = normal.sqrMagnitude < 0.5;

                    distance[i] = (collapsed) ? (0.0) : (rowSign[i] * DVector3.Dot(end - centreB, normal));
                    pathPart[i] = (collapsed) ? (0.0) : (rowSign[i] * DVector3.Dot(centreA - centreB, normal));
                    ratio[i]    = distance[i] / rowRest[i];
                    spacing[i]  = (centreA - centreB).magnitude;

                    double barLength = bar.magnitude;

                    along[i] = (barLength > 1e-12) ? (DVector3.Dot(end - centreB, bar) / barLength / (0.5 * barLength)) : (double.NaN);
                    restAlong[i] = DVector3.Dot(restEnd - nodeB.restPosition, nodeB.restRight.normalized) / nodeHalf[b];

                    order[i] = i;
                }

                Array.Sort(order, (x, y) => ratio[x].CompareTo(ratio[y]));

                int below = 0, crossed = 0;
                double min = double.PositiveInfinity, sum = 0.0;

                for (int i = 0; i < n; i++)
                {
                    sum += ratio[i];
                    if (ratio[i] < crossTerm.minRatio) below++;
                    if (ratio[i] <= 0.0) crossed++;
                    if (ratio[i] < min) min = ratio[i];
                }

                using (var w = new System.IO.StreamWriter(path, false))
                {
                    w.WriteLine($"# {term.name} rows: {n} minDistanceRatio {min.ToString("F4", ci)} meanDistanceRatio {(sum / n).ToString("F4", ci)} rowsBelowFloor {below} rowsCrossed {crossed} floor {crossTerm.minRatio.ToString("F3", ci)} band {crossTerm.softBand.ToString("F3", ci)} carrier {crossTerm.endpointCarrier}{((blender == null) && (crossTerm.endpointCarrier == EndpointCarrier.ThroughField) ? (" (no field: node transform)") : (""))} widthScale {deformation.effectiveCorridorWidthScale.ToString("F3", ci)}");
                    w.WriteLine("# end: +1 along the node's rest right, -1 against it. distance = pathPart + barPart. along: the end's foot along the neighbour's bar, in half-lengths - beyond +-1 the foot is past the bar end. Sorted by ratio, worst first.");
                    w.WriteLine("row\tnode\tend\tneighbour\trestDistance\tdistance\tratio\thinge\tpathPart\tbarPart\trestAlong\talong\thalfThis\thalfNeighbour\trestSpacing\tspacing\tnodeRestX\tnodeRestY\tnodeRestZ");

                    foreach (int i in order)
                    {
                        int a = rowNode[i], b = rowNeighbour[i];
                        DVector3 p = deformation.nodes[a].restPosition;

                        w.WriteLine(string.Join("\t", new[]
                        {
                            i.ToString(ci),
                            a.ToString(ci),
                            rowEnd[i].ToString(ci),
                            b.ToString(ci),
                            rowRest[i].ToString("F5", ci),
                            distance[i].ToString("F5", ci),
                            ratio[i].ToString("F4", ci),
                            Hinge(distance[i], rowRest[i]).ToString("F5", ci),
                            pathPart[i].ToString("F5", ci),
                            (distance[i] - pathPart[i]).ToString("F5", ci),
                            restAlong[i].ToString("F3", ci),
                            along[i].ToString("F3", ci),
                            nodeHalf[a].ToString("F4", ci),
                            nodeHalf[b].ToString("F4", ci),
                            (p - deformation.nodes[b].restPosition).magnitude.ToString("F4", ci),
                            spacing[i].ToString("F4", ci),
                            p.x.ToString("F4", ci),
                            p.y.ToString("F4", ci),
                            p.z.ToString("F4", ci),
                        }));

                        written++;
                    }
                }

                return true;
            }
        }
#endif
    }
}
#endif
