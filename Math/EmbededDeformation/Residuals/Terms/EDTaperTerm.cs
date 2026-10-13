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
    /// Limits how fast the corridor's cross-section width changes from one node to the next -
    /// what the slope terms do for the floor, done for the scale. Built 2026-10-13 as queue §44,
    /// from §43's reading: a terminal widened 2.5x carries the whole widening on its own link, the
    /// node behind it at rest width, because nothing in the objective reads a neighbour's across
    /// width - regularization couples positions along the skeleton, the field term is indifferent
    /// to a widening, and rotation pulls every neighbour back to orthonormal - so the door widens
    /// at the floor, where the terminal's map dominates the blend, and not at the arch, where the
    /// blend mixes it with three maps at rest. A hand taper over the three nodes behind the terminal
    /// fixed the door by eye; this is that taper as a limit the solver finds for itself.
    ///
    /// One row per link. A node's across width is the length of its map applied to its rest right
    /// (1 at rest); a link's taper rate is the absolute log ratio of its two nodes' widths over the
    /// link's rest length - scale-free, symmetric between widening and narrowing, zero at rest. The
    /// residual is a hinge on the rate over maxRate with a C1 onset across a soft band below the
    /// limit (the cross-section and floor slope terms' shape, for the reason they have it: a hinge
    /// that starts exactly where every solve begins has a one-sided derivative there). A limit
    /// rather than a smoothness energy: it is zero wherever the width changes gently enough, so it
    /// argues with nothing where there is nothing to argue about, and the limit is the designer's
    /// number - how much a corridor may widen per world unit - with the taper's length following
    /// from it.
    ///
    /// Pure graph space: no field, no navmesh, no mesh. The Jacobian is exact and sparse - a row
    /// depends on the nine linear entries of its two nodes and on nothing else, and the derivative
    /// of |A r| in A is the outer product of A r over its length with r - so the columns are filled
    /// in closed form and the row costs nothing. Rows under the band's start are identically zero in
    /// a neighbourhood and are left at zero.
    /// </summary>
    [Serializable]
    [PolymorphicName("Taper")]
    public class EDTaperTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("How fast the across width may change along the corridor before the term objects: the absolute log ratio of two neighbouring nodes' widths per world unit of rest link length. 0.3 lets a width change by a factor of e^0.3, about 35%, per world unit, so a 2.5x widening is spread over about three world units.")]
        private float maxRate = 0.3f;

        [SerializeField, Range(0.0f, 1.0f), Tooltip("The fraction of the limit over which the hinge rises from zero to its slope: the penalty is zero below (1 - band) times the limit, quadratic across the band, linear beyond the limit with slope one per limit's worth of rate. C1 at its onset, so a row that starts on the band's edge has a derivative.")]
        private float softBand = 0.25f;

        public override string name => "taper";

        /// <summary>The limit as the term holds it, for a driver that states one per run and restores it after.</summary>
        public float rateLimit => maxRate;

        /// <summary>The one sanctioned write onto the limit, for the schedule runner's per-run override; equivalent to editing the inspector field.</summary>
        public void SetRateLimit(float value) => maxRate = Mathf.Max(0.0f, value);

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new TaperInstance(this, deformation);

        public class TaperInstance : Instance
        {
            private readonly EDTaperTerm taperTerm;

            // Per link: its two nodes (lower index first) and its rest length.
            private int[]       linkA = Array.Empty<int>();
            private int[]       linkB = Array.Empty<int>();
            private double[]    linkRestLength = Array.Empty<double>();

            public TaperInstance(EDTaperTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                taperTerm = term;
            }

            /// <summary>Lays out one row per link, every link once. Runs on construction and whenever the graph is rebuilt.</summary>
            public override void Reset()
            {
                var a = new List<int>();
                var b = new List<int>();
                var length = new List<double>();

                var nodes = deformation.nodes;
                int nodeCount = (nodes != null) ? (nodes.Count) : (0);

                for (int i = 0; i < nodeCount; i++)
                {
                    var neighbours = nodes[i].neighbors;

                    if (neighbours == null) continue;

                    foreach (int j in neighbours)
                    {
                        if ((j <= i) || (j >= nodeCount)) continue;

                        double restLength = (nodes[j].restPosition - nodes[i].restPosition).magnitude;

                        if (restLength <= 1e-9) continue;

                        a.Add(i);
                        b.Add(j);
                        length.Add(restLength);
                    }
                }

                linkA = a.ToArray();
                linkB = b.ToArray();
                linkRestLength = length.ToArray();

                Debug.Log($"[ED] {term.name}: {linkA.Length} rows (one per link); limit {taperTerm.maxRate.ToString("F3", CultureInfo.InvariantCulture)} per world unit, band {taperTerm.softBand.ToString("F2", CultureInfo.InvariantCulture)} of it; every rate 0 at rest.");
            }

            protected override int ComputeRowCount() => linkA.Length;

            // ---------------------------------------------------------------- the row

            /// <summary>The node's map on its rest right, unnormalized - the across width, 1 at rest.</summary>
            private DVector3 Across(int nodeIndex, EDStateView state)
                => state.TransformVector(nodeIndex, deformation.nodes[nodeIndex].restRight);

            /// <summary>
            /// The link's taper rate at the state, with the pieces the Jacobian needs: the two across
            /// vectors, their lengths, and the sign of the log ratio.
            /// </summary>
            private double Rate(int k, EDStateView state, out DVector3 tA, out DVector3 tB, out double wA, out double wB, out double sign)
            {
                tA = Across(linkA[k], state);
                tB = Across(linkB[k], state);

                wA = Math.Max(1e-9, tA.magnitude);
                wB = Math.Max(1e-9, tB.magnitude);

                double logRatio = Math.Log(wA / wB);

                sign = (logRatio >= 0.0) ? (1.0) : (-1.0);

                return Math.Abs(logRatio) / linkRestLength[k];
            }

            private double Rate(int k, EDStateView state) => Rate(k, state, out _, out _, out _, out _, out _);

            /// <summary>
            /// The hinge on v = rate / limit, C1 at its onset: zero up to (1 - band), then e^2 / 2b
            /// across the band where e = v - (1 - band), then e - b/2 beyond the limit - value b/2
            /// and slope 1 at the limit from both sides, and slope 1 per limit's worth of rate
            /// beyond it whatever the band, so the weight means the same thing at every band. With
            /// the band at zero it is the plain hinge max(0, v - 1).
            /// </summary>
            private double Penalty(double rate, out double derivative)
            {
                double limit = Math.Max(1e-9, taperTerm.maxRate);
                double band = Math.Clamp(taperTerm.softBand, 0.0, 1.0);

                double e = rate / limit - (1.0 - band);

                if (e <= 0.0)
                {
                    derivative = 0.0;
                    return 0.0;
                }

                if (e < band)
                {
                    derivative = (e / band) / limit;
                    return 0.5 * e * e / band;
                }

                derivative = 1.0 / limit;
                return e - 0.5 * band;
            }

            private double EvaluateRow(int k, EDStateView state, double w)
                => w * Penalty(Rate(k, state), out _);

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                for (int k = 0; k < rowCount; k++)
                    residual[rowOffset + k] = EvaluateRow(k, state, residualWeight);
            }

            // ---------------------------------------------------------------- the Jacobian

            /// <summary>
            /// Closed form. For the row's node A, d(rate)/dA = sign / (L w_A) times d(w_A)/dA, and
            /// d|A r|/dA_ab = (A r)_a r_b / |A r|; for node B the same with the opposite sign. The
            /// nine linear entries of each node's twelve are columns offset row*4 + col; the
            /// translation column moves no width and stays zero, as does every other node's column.
            /// A row below the band's onset has a zero derivative and is left at zero.
            /// </summary>
            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                var view = new EDStateView(state);

                for (int k = 0; k < rowCount; k++)
                {
                    double rate = Rate(k, view, out DVector3 tA, out DVector3 tB, out double wA, out double wB, out double sign);

                    Penalty(rate, out double dPenalty);

                    if (dPenalty <= 0.0) continue;

                    int row = rowOffset + k;
                    double common = residualWeight * dPenalty / linkRestLength[k];

                    FillNode(jacobian, row, linkA[k], tA, wA, common * sign, ref jacobianNormSq);
                    FillNode(jacobian, row, linkB[k], tB, wB, -common * sign, ref jacobianNormSq);
                }
            }

            private void FillNode(DenseMatrix jacobian, int row, int nodeIndex, DVector3 t, double w, double factor, ref double normSq)
            {
                DVector3 r = deformation.nodes[nodeIndex].restRight;
                int paramBase = EDStateView.ParamBase(nodeIndex);

                // factor already carries 1 / w for the log; the outer product carries the other.
                double scale = factor / (w * w);

                double[] ta = { t.x, t.y, t.z };
                double[] rb = { r.x, r.y, r.z };

                for (int a = 0; a < 3; a++)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        double value = scale * ta[a] * rb[b];
                        int col = paramBase + a * 4 + b;

                        jacobian[row, col] = value;
                        normSq += value * value;
                    }
                }
            }

            // ---------------------------------------------------------------- the columns

            public override string[] DescribeHeader()
                => new[] { "linksMeasured", "maxRate", "maxRateNodeA", "maxRateNodeB", "meanRate", "linksOverLimit", "linksInBand" };

            /// <summary>
            /// The rate at every link at the state, whatever the weight: the largest and its link,
            /// the mean, the links over the limit and the links inside the soft band under it. The
            /// same quantity the taper measure exports under its own name, so the two agreeing is a
            /// free check that the rows the solver sees are the measure's.
            /// </summary>
            public override string[] Describe(EDStateView state)
            {
                if (linkA.Length == 0) return Array.Empty<string>();

                double limit = Math.Max(1e-9, taperTerm.maxRate);
                double bandStart = limit * (1.0 - Math.Clamp(taperTerm.softBand, 0.0, 1.0));

                double max = 0.0, sum = 0.0;
                int maxLink = -1, over = 0, inBand = 0;

                for (int k = 0; k < linkA.Length; k++)
                {
                    double rate = Rate(k, state);

                    sum += rate;

                    if (rate > max) { max = rate; maxLink = k; }
                    if (rate > limit) over++;
                    else if (rate > bandStart) inBand++;
                }

                return new[]
                {
                    linkA.Length.ToString(CultureInfo.InvariantCulture),
                    max.ToString("F4", CultureInfo.InvariantCulture),
                    ((maxLink >= 0) ? (linkA[maxLink]) : (-1)).ToString(CultureInfo.InvariantCulture),
                    ((maxLink >= 0) ? (linkB[maxLink]) : (-1)).ToString(CultureInfo.InvariantCulture),
                    (sum / linkA.Length).ToString("F4", CultureInfo.InvariantCulture),
                    over.ToString(CultureInfo.InvariantCulture),
                    inBand.ToString(CultureInfo.InvariantCulture),
                };
            }
        }
#endif
    }
}
#endif
