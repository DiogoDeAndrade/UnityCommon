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
    /// Keeps the skeleton's path from bending tighter than the corridor's width allows. Designed
    /// 2026-09-12 as queue §14, from what §13 found by accident: the corridor clearance term on a
    /// coarsely sampled navmesh removed the fold at the bend because a long boundary edge along the
    /// outer wall is a chord that cuts inside the arc in proportion to the bend's curvature - a
    /// curvature penalty carried by sampling error, which the same term lost the moment the width
    /// was read correctly. This is that preference written honestly, on the skeleton alone.
    ///
    /// Per node with exactly two neighbours (junctions and terminals contribute nothing, as in the
    /// corridor terms), the path's local curvature is the Menger curvature of the three deformed node
    /// positions - the reciprocal of their circumradius - and the node's half-width is a rest
    /// constant: the nearer half-extent of the rest corridor along the node's across axis, from the
    /// same probe the field seeds its bars with, measured once in Reset. Their product is the
    /// inner-wall compression, the fraction by which the inner wall's arc is shorter than the
    /// centreline's (zero length at 1, folded before that). The residual is a hinge above the
    /// node's limit, one row per measured node; nothing on the navmesh is touched at solve time, so
    /// the term is pure graph space and reads the same on any navmesh sampling.
    ///
    /// The limit is per node: the larger of maxCompression and the node's compression at rest. The
    /// first Build on Corridor5 read a rest maximum of 1.26 at one node - a half-width measured into
    /// a room beside a kink in the skeleton, so the number says nothing about a corridor there - and
    /// an absolute limit would charge that node in every state including rest. What the term can
    /// honestly ask is that no bend gets tighter than its width allows *or* than the piece was
    /// modelled with, whichever is looser. The columns report both the absolute maximum and the
    /// maximum excess over rest, since the second is the one that separates states.
    ///
    /// Not a smoothness energy. With the terminal tangents pinned the total turn between two
    /// connectors is fixed, so a limit on local curvature cannot straighten the path - it can only
    /// spread the turn, which lengthens the path, the shape the hand-posed wide curve has. And as a
    /// limit it is zero wherever the bend is gentle enough for its width, so it argues with nothing
    /// where there is nothing to argue about.
    ///
    /// The Jacobian is exact and sparse: a row depends on nine parameters, the translation entries
    /// of its three nodes (a node's position is its rest position plus its translation column, and
    /// nothing else in the twelve moves it), so those nine columns are filled by finite differences
    /// and every other column is identically zero. The early-out on a row inside the limit is exact
    /// for the same reason the segment term's is: the hinge is identically zero in a neighbourhood.
    /// </summary>
    [Serializable]
    [PolymorphicName("Bend Compression")]
    public class EDBendCompressionTerm : EDResidualTerm
    {
        [SerializeField, Range(0.0f, 1.0f), Tooltip("The inner-wall compression a bend may reach before the term objects: half-width times curvature, the fraction of the inner wall's length lost to the bend. 1 is an inner wall of zero length. Per node the limit is the larger of this and the node's rest compression, so a bend the piece was modelled with is never charged; the Build line prints the rest maximum.")]
        private float maxCompression = 0.5f;

        [SerializeField, Range(0.0f, 89.0f), Tooltip("The corridor probe's height band when measuring each node's rest half-width - the same filter on which boundary crossings count as walls the corridor clearance term uses.")]
        private float coneAngleDegrees = 30.0f;

        public override string name => "bendCompression";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new BendInstance(this, deformation);

        public class BendInstance : Instance
        {
            private readonly EDBendCompressionTerm bendTerm;

            // Per measured node: its index, its two neighbours, its rest half-width, and its rest
            // compression - the bend the piece was modelled with, reported so the limit is set above it.
            private int[]       rowNode = Array.Empty<int>();
            private int[]       rowA = Array.Empty<int>();
            private int[]       rowB = Array.Empty<int>();
            private double[]    rowHalfWidth = Array.Empty<double>();
            private double[]    rowRestCompression = Array.Empty<double>();

            private int         skippedJunctionsOrTerminals;
            private int         skippedUnbounded;

            public BendInstance(EDBendCompressionTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                bendTerm = term;
            }

            /// <summary>
            /// Lays out the rows and measures each node's rest half-width and rest compression. Runs
            /// on construction and whenever the graph is rebuilt - after BuildNavigationData, which
            /// is what makes the rest probe possible here.
            /// </summary>
            public override void Reset()
            {
                var nodesOut = new List<int>();
                var aOut = new List<int>();
                var bOut = new List<int>();
                var widthOut = new List<double>();
                var restOut = new List<double>();

                skippedJunctionsOrTerminals = 0;
                skippedUnbounded = 0;

                var nodes = deformation.nodes;
                int nodeCount = (nodes != null) ? (nodes.Count) : (0);

                for (int i = 0; i < nodeCount; i++)
                {
                    EDNode node = nodes[i];

                    if ((node.neighbors == null) || (node.neighbors.Count != 2))
                    {
                        skippedJunctionsOrTerminals++;
                        continue;
                    }

                    int a = node.neighbors[0];
                    int b = node.neighbors[1];

                    if ((a < 0) || (b < 0) || (a >= nodeCount) || (b >= nodeCount) || (a == b))
                    {
                        skippedJunctionsOrTerminals++;
                        continue;
                    }

                    // The nearer wall at rest, along the node's own across axis - the width the field
                    // seeds with. A side unbounded at rest leaves no width to compress: no row.
                    if ((!deformation.TryMeasureCorridor(node.restPosition, node.restRight, node.restUp, null, null, bendTerm.coneAngleDegrees, out EDCorridorExtent extent)) ||
                        (extent.positive == double.MaxValue) || (extent.negative == double.MaxValue))
                    {
                        skippedUnbounded++;
                        continue;
                    }

                    double halfWidth = Math.Min(extent.positive, extent.negative);

                    if (halfWidth <= 1e-9)
                    {
                        skippedUnbounded++;
                        continue;
                    }

                    nodesOut.Add(i);
                    aOut.Add(a);
                    bOut.Add(b);
                    widthOut.Add(halfWidth);
                    restOut.Add(halfWidth * MengerCurvature(nodes[a].restPosition, node.restPosition, nodes[b].restPosition));
                }

                rowNode = nodesOut.ToArray();
                rowA = aOut.ToArray();
                rowB = bOut.ToArray();
                rowHalfWidth = widthOut.ToArray();
                rowRestCompression = restOut.ToArray();

                double restMax = 0.0;
                int restMaxNode = -1;

                for (int i = 0; i < rowNode.Length; i++)
                    if (rowRestCompression[i] > restMax) { restMax = rowRestCompression[i]; restMaxNode = rowNode[i]; }

                Debug.Log($"[ED] {term.name}: {rowNode.Length} rows ({skippedJunctionsOrTerminals} junctions or terminals skipped, {skippedUnbounded} unbounded at rest); rest compression max {restMax.ToString("F4", CultureInfo.InvariantCulture)} at node {restMaxNode}, limit {bendTerm.maxCompression.ToString("F3", CultureInfo.InvariantCulture)}.");
            }

            protected override int ComputeRowCount()
            {
                if (!deformation.isNavConfigured) return 0;

                return rowNode.Length;
            }

            /// <summary>
            /// The curvature of the circle through three points: 4 x area over the product of the
            /// three side lengths, the reciprocal of the circumradius. Zero for collinear points and
            /// for a degenerate triangle.
            /// </summary>
            private static double MengerCurvature(DVector3 a, DVector3 p, DVector3 b)
            {
                DVector3 ap = p - a;
                DVector3 pb = b - p;
                DVector3 ab = b - a;

                double lengths = ap.magnitude * pb.magnitude * ab.magnitude;

                if (lengths < 1e-18) return 0.0;

                // 4 x area = 2 x |cross| , since area = |cross| / 2.
                return 2.0 * DVector3.Cross(ap, ab).magnitude / lengths;
            }

            private double Compression(int i, EDStateView state)
            {
                var nodes = deformation.nodes;

                DVector3 a = state.DeformNodePosition(rowA[i], nodes[rowA[i]].restPosition);
                DVector3 p = state.DeformNodePosition(rowNode[i], nodes[rowNode[i]].restPosition);
                DVector3 b = state.DeformNodePosition(rowB[i], nodes[rowB[i]].restPosition);

                return rowHalfWidth[i] * MengerCurvature(a, p, b);
            }

            /// <summary>The node's limit: the term's, or the bend it was modelled with if that is looser.</summary>
            private double Limit(int i) => Math.Max(bendTerm.maxCompression, rowRestCompression[i]);

            private double EvaluateRow(int i, EDStateView state, double w)
                => w * Math.Max(0.0, Compression(i, state) - Limit(i));

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < rowCount; i++)
                    residual[row++] = EvaluateRow(i, state, residualWeight);
            }

            /// <summary>
            /// One row by nine finite differences - the translation entries of the node and its two
            /// neighbours, offsets 3, 7 and 11 of each node's twelve - and zeros everywhere else,
            /// which is exact rather than sparse-by-approximation: no other parameter moves a node's
            /// position. Rows inside the limit are left at zero after one evaluation.
            /// </summary>
            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                var baseView = new EDStateView(state);

                for (int i = 0; i < rowCount; i++)
                {
                    int row = rowOffset + i;

                    double r0 = EvaluateRow(i, baseView, residualWeight);

                    if (Math.Abs(r0) <= 1e-12) continue;

                    foreach (int nodeIndex in new[] { rowNode[i], rowA[i], rowB[i] })
                    {
                        int paramBase = EDStateView.ParamBase(nodeIndex);

                        foreach (int offset in translationOffsets)
                        {
                            int col = paramBase + offset;

                            double original = state.Get(col);
                            double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                            var modifiedState = new EDStateView(state, col, eps);

                            double value = (EvaluateRow(i, modifiedState, residualWeight) - r0) / eps;

                            jacobian[row, col] = value;
                            jacobianNormSq += value * value;
                        }
                    }
                }
            }

            private static readonly int[] translationOffsets = { 3, 7, 11 };

            public override string[] DescribeHeader()
                => new[] { "nodesMeasured", "maxCompression", "meanCompression", "nodesOverLimit", "maxCompressionNode", "maxExcessOverRest", "maxExcessNode" };

            /// <summary>
            /// The compression at every measured node at the state, whatever the weight - the column
            /// §14 reads before the term gets one: the absolute maximum and its node, the mean, the
            /// nodes over their limit, and the maximum excess over the node's own rest compression
            /// with its node - the number that says where the deformation bent the piece tighter
            /// than it was modelled. Costs one evaluation per row.
            /// </summary>
            public override string[] Describe(EDStateView state)
            {
                if (rowNode.Length == 0) return Array.Empty<string>();

                double max = 0.0, sum = 0.0, maxExcess = 0.0;
                int maxNode = -1, over = 0, maxExcessNode = -1;

                for (int i = 0; i < rowNode.Length; i++)
                {
                    double c = Compression(i, state);
                    double excess = c - rowRestCompression[i];

                    sum += c;

                    if (c > Limit(i)) over++;
                    if (c > max) { max = c; maxNode = rowNode[i]; }
                    if (excess > maxExcess) { maxExcess = excess; maxExcessNode = rowNode[i]; }
                }

                return new[]
                {
                    rowNode.Length.ToString(CultureInfo.InvariantCulture),
                    max.ToString("F4", CultureInfo.InvariantCulture),
                    (sum / rowNode.Length).ToString("F4", CultureInfo.InvariantCulture),
                    over.ToString(CultureInfo.InvariantCulture),
                    maxNode.ToString(CultureInfo.InvariantCulture),
                    maxExcess.ToString("F4", CultureInfo.InvariantCulture),
                    maxExcessNode.ToString(CultureInfo.InvariantCulture),
                };
            }
        }
#endif
    }
}
#endif
