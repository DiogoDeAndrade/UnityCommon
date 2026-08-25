using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UC.DoubleMath;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// One term's contribution to the current energy, as a number rather than a log line.
    ///
    /// The numbers live in the fields; every rendering of them goes through Header/Describe, which
    /// hand the tracked values back as parallel label and value arrays. The console line, the golden
    /// harness and a CSV export are then different arrangements of the same strings, so none of them
    /// can drift from the others - which is the point of the mechanism, since the export exists to
    /// be graphed against what the console showed.
    /// </summary>
    public readonly struct EDTermEnergy
    {
        /// <summary>The term's name, as the energy model lists it.</summary>
        public readonly string  name;
        /// <summary>How many residual rows the term occupies. Zero means the term contributed nothing.</summary>
        public readonly int     rows;
        /// <summary>The term's authored weight, before the sqrt(weight / rows) row scaling.</summary>
        public readonly double  conceptualWeight;
        /// <summary>Sum of squared residuals over the term's rows, weighted as the solver weights them.</summary>
        public readonly double  energy;
        public readonly double  rms;
        public readonly double  maxAbs;
        /// <summary>What the term itself said about the measured state, or empty - see
        /// <see cref="EDResidualTerm.Instance.DescribeNotes"/>. A column like the others so every
        /// row of an export has the same shape, prose or no prose.</summary>
        public readonly string  notes;

        public EDTermEnergy(string name, int rows, double conceptualWeight, double energy, double rms, double maxAbs, string notes = "")
        {
            this.name = name;
            this.rows = rows;
            this.conceptualWeight = conceptualWeight;
            this.energy = energy;
            this.rms = rms;
            this.maxAbs = maxAbs;
            this.notes = notes ?? "";
        }

        /// <summary>
        /// What each entry of <see cref="Describe"/> is, in the same order. The name is not among
        /// them: it identifies the row, and a consumer pairs it with these values itself.
        /// </summary>
        public static string[] Header() => new[] { "rows", "weight", "energy", "rms", "max", "share", "notes" };

        /// <summary>
        /// The tracked values as bare strings, one per <see cref="Header"/> entry, carrying no
        /// labels of their own - the shape a CSV row or a plot series wants. Invariant culture,
        /// so a file written here reads the same on any machine.
        ///
        /// The share is against the total handed in rather than stored, because it is a fact about
        /// the whole breakdown and not about this term alone.
        /// </summary>
        public string[] Describe(double totalEnergy)
        {
            double share = (totalEnergy > 0.0) ? (100.0 * energy / totalEnergy) : (0.0);

            return new[]
            {
                rows.ToString(CultureInfo.InvariantCulture),
                conceptualWeight.ToString("G6", CultureInfo.InvariantCulture),
                energy.ToString("E6", CultureInfo.InvariantCulture),
                rms.ToString("E6", CultureInfo.InvariantCulture),
                maxAbs.ToString("E6", CultureInfo.InvariantCulture),
                share.ToString("F3", CultureInfo.InvariantCulture) + "%",
                notes
            };
        }

        // One padding per Describe column, sized so the existing console layout falls out of the
        // generic renderer unchanged: rows 7 and weight 8 as the old format strings had, 13 for the
        // three E6 columns (their natural width, so no padding in practice), 8 so the share matches
        // its old 7-wide number plus the percent sign. Notes are free text and get no padding.
        private static readonly int[] columnWidths = { 7, 8, 13, 13, 13, 8, 0 };

        /// <summary>
        /// The console line, assembled from <see cref="Header"/> and <see cref="Describe"/> rather
        /// than from the fields - so the log and any export are two renderings of the same array,
        /// and a column added to one cannot be missing from the other. An empty value is skipped
        /// rather than rendered as a bare label, which in practice is the notes column of every
        /// term that has none - the export keeps the empty cell, the eye is spared it.
        /// </summary>
        public string DescribeLine(double totalEnergy)
        {
            string[] header = Header();
            string[] values = Describe(totalEnergy);

            var sb = new StringBuilder();

            sb.Append($"{name,-20}");

            for (int i = 0; i < header.Length; i++)
            {
                if (string.IsNullOrEmpty(values[i])) continue;

                sb.Append($" {header[i]}={values[i].PadLeft(columnWidths[i])}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Read and write access to the solved node transforms, for editing a deformation by hand.
    ///
    /// Separate from the debug accessors above it because the difference is the point: those observe
    /// a solve, these replace its answer. Nothing in here is used by a solve, and nothing in here is
    /// reachable from one.
    ///
    /// **A hand edit cannot reach a golden.** CaptureDump always solves with reset = true, so the
    /// state these methods write is discarded before the dump is taken. The hazard runs the other
    /// way - a solve destroys the hand edit - and it is guarded on the tool's side, where the
    /// baseline it would destroy is held.
    /// </summary>
    public partial class EmbededDeformation
    {
        public int nodeCount => (nodes != null) ? (nodes.Count) : (0);

        /// <summary>
        /// Whether there is a solved state to edit, and one that matches the graph in front of it.
        ///
        /// The size test rather than a null test, and this is the recurring trap rather than
        /// pedantry: EDState is [Serializable] on a [SerializeField] member, so a graph rebuilt to a
        /// different node count comes back from serialization as a live state object of the previous
        /// size. Indexing it would read another node's parameters rather than throw.
        /// </summary>
        public bool hasEditableState => (nodeCount > 0) && (currentState != null) && (currentState.Count == (12 * nodeCount));

        private bool IsEditableNode(int nodeIndex) => (hasEditableState) && (nodeIndex >= 0) && (nodeIndex < nodeCount);

        /// <summary>
        /// A node's current transform, split the way the parameter vector stores it: the 3x3 linear
        /// part, and the translation that carries the node away from its rest position.
        ///
        /// The linear part acts on offsets from the rest position, not on world points - a node's
        /// own deformed position is restPosition + translation, with the linear part contributing
        /// nothing there because the offset is zero.
        /// </summary>
        public bool TryGetNodeTransform(int nodeIndex, out Matrix3x3 linear, out Vector3 translation)
        {
            linear = Matrix3x3.identity;
            translation = Vector3.zero;

            if (!IsEditableNode(nodeIndex)) return false;

            int o = nodeIndex * 12;

            linear.m00 = (float)currentState.Get(o + 0);
            linear.m01 = (float)currentState.Get(o + 1);
            linear.m02 = (float)currentState.Get(o + 2);

            linear.m10 = (float)currentState.Get(o + 4);
            linear.m11 = (float)currentState.Get(o + 5);
            linear.m12 = (float)currentState.Get(o + 6);

            linear.m20 = (float)currentState.Get(o + 8);
            linear.m21 = (float)currentState.Get(o + 9);
            linear.m22 = (float)currentState.Get(o + 10);

            translation = new Vector3((float)currentState.Get(o + 3),
                                      (float)currentState.Get(o + 7),
                                      (float)currentState.Get(o + 11));

            return true;
        }

        /// <summary>
        /// Replaces one node's transform, leaving every other node's parameters exactly as the solver
        /// left them.
        ///
        /// One node at a time rather than a whole-state write, and that is a precision decision
        /// rather than an interface preference. The parameters are doubles and the editor's handles
        /// are floats, so a value that makes this trip loses the bottom of its mantissa. Writing only
        /// the nodes that were actually touched keeps that loss where the edit is, instead of
        /// spreading it over a graph the user did not move.
        /// </summary>
        public bool TrySetNodeTransform(int nodeIndex, Matrix3x3 linear, Vector3 translation)
        {
            if (!IsEditableNode(nodeIndex)) return false;

            int o = nodeIndex * 12;

            currentState.Set(o + 0, linear.m00);
            currentState.Set(o + 1, linear.m01);
            currentState.Set(o + 2, linear.m02);
            currentState.Set(o + 3, translation.x);

            currentState.Set(o + 4, linear.m10);
            currentState.Set(o + 5, linear.m11);
            currentState.Set(o + 6, linear.m12);
            currentState.Set(o + 7, translation.y);

            currentState.Set(o + 8, linear.m20);
            currentState.Set(o + 9, linear.m21);
            currentState.Set(o + 10, linear.m22);
            currentState.Set(o + 11, translation.z);

            return true;
        }

        /// <summary>
        /// A node's twelve parameters as the state stores them, at full precision.
        ///
        /// For recording a baseline rather than for editing: a revert has to put back exactly what
        /// the solver produced, and going out through TryGetNodeTransform and back in would return a
        /// float-rounded version of it. A node that was selected and not moved must come back
        /// bit-identical, or "revert" quietly means "nearly".
        /// </summary>
        public bool TryGetNodeParameters(int nodeIndex, double[] destination)
        {
            if (!IsEditableNode(nodeIndex)) return false;
            if ((destination == null) || (destination.Length < 12)) return false;

            int o = nodeIndex * 12;

            for (int i = 0; i < 12; i++)
                destination[i] = currentState.Get(o + i);

            return true;
        }

        public bool TrySetNodeParameters(int nodeIndex, double[] source)
        {
            if (!IsEditableNode(nodeIndex)) return false;
            if ((source == null) || (source.Length < 12)) return false;

            int o = nodeIndex * 12;

            for (int i = 0; i < 12; i++)
                currentState.Set(o + i, source[i]);

            return true;
        }

        /// <summary>
        /// A node's rest pose: where it sits undeformed, and the orthonormal frame its energies are
        /// written against.
        ///
        /// The frame's columns are right, up and forward in that order, so that a transform's shear
        /// can be stated as the rotation between this and the stretch's own eigenframe.
        /// </summary>
        public bool TryGetNodeRest(int nodeIndex, out Vector3 restPosition, out Matrix3x3 restFrame)
        {
            restPosition = Vector3.zero;
            restFrame = Matrix3x3.identity;

            if ((nodes == null) || (nodeIndex < 0) || (nodeIndex >= nodes.Count)) return false;

            EDNode node = nodes[nodeIndex];

            restPosition = node.restPosition.ToVector3();

            Vector3 right = node.restRight.ToVector3();
            Vector3 up = node.restUp.ToVector3();
            Vector3 forward = node.restForward.ToVector3();

            restFrame.m00 = right.x; restFrame.m01 = up.x; restFrame.m02 = forward.x;
            restFrame.m10 = right.y; restFrame.m11 = up.y; restFrame.m12 = forward.y;
            restFrame.m20 = right.z; restFrame.m21 = up.z; restFrame.m22 = forward.z;

            return true;
        }

        public IReadOnlyList<int> GetNodeNeighbors(int nodeIndex)
        {
            if ((nodes == null) || (nodeIndex < 0) || (nodeIndex >= nodes.Count)) return null;

            return nodes[nodeIndex].neighbors;
        }

        /// <summary>
        /// A copy of the whole solved state, for putting back later. Null when there is nothing to
        /// copy, which the caller has to treat as "no baseline" rather than as an empty one.
        /// </summary>
        public EDState CaptureEditableState()
        {
            if (!hasEditableState) return null;

            return currentState.Clone();
        }

        /// <summary>
        /// Puts a captured state back, refusing one that was taken from a different graph.
        ///
        /// Refusing rather than resizing: a state of the wrong length is not a baseline that needs
        /// adjusting, it is a baseline from another build, and there is no correspondence between its
        /// nodes and these ones to salvage.
        /// </summary>
        public bool TryRestoreEditableState(EDState snapshot)
        {
            if ((snapshot == null) || (nodeCount == 0)) return false;
            if (snapshot.Count != (12 * nodeCount)) return false;

            currentState = snapshot.Clone();

            return true;
        }

        /// <summary>
        /// Re-measures the per-segment clearances against whatever the state now holds.
        ///
        /// A hand edit moves the geometry the clearance term reads, and the cached values it reads
        /// are the ones the solve last computed. Without this the clearance rows of any energy
        /// readout describe the configuration before the edit while every other row describes the one
        /// after it, which is exactly the mixed-measure diagnostic this codebase has been caught by
        /// before.
        ///
        /// No-op in the modes that carry no navigation data - ComputeClearance already answers "no
        /// clearance" for every segment there.
        /// </summary>
        public void RecomputeClearanceForCurrentState()
        {
            if (!hasEditableState) return;

            ComputeClearance(currentState);
        }

        /// <summary>
        /// The energy of the state as it stands, broken down by term, without solving.
        ///
        /// This is the measurement a hand-edited deformation exists to produce: build the
        /// configuration you believe is good, and read which terms object to it. A term that scores
        /// worse on the better-looking configuration is a term that is asking for the wrong thing,
        /// and that is a statement about the objective rather than about the solver.
        ///
        /// Weighted exactly as the solver weights it, because an unweighted breakdown compares
        /// quantities in different units and answers a question nobody asked. The authored weight is
        /// carried alongside so the two can still be told apart.
        /// </summary>
        public IReadOnlyList<EDTermEnergy> MeasureTermEnergies(EDEnergyModel.Instance energy)
        {
#if MATH_NET_AVAILABLE
            if (!hasEditableState) return null;
            if (energy == null) return null;

            InitMathNet();

            energy.Resolve();

            EDStateView stateView = new EDStateView(currentState);

            Vector<double> f = energy.EvaluateResidual(stateView);

            return MeasureTermEnergies(f, energy, stateView);
#else
            return null;
#endif
        }

#if MATH_NET_AVAILABLE
        /// <summary>
        /// The same breakdown from a residual vector that already exists - the per-iteration case,
        /// where the solver has just evaluated one and measuring it again would be a second full
        /// residual pass per iteration. Static because it reads nothing but its arguments; the
        /// instance method above is this after evaluating the residual at the current state.
        ///
        /// The state is what fills the notes column: a term's DescribeNotes may re-measure at
        /// residual-evaluation cost, which a breakdown pays once and the per-iteration solver log
        /// must not pay at all - so the per-iteration caller passes null and its notes stay empty.
        /// </summary>
        public static IReadOnlyList<EDTermEnergy> MeasureTermEnergies(Vector<double> f, EDEnergyModel.Instance energy, EDStateView? state = null)
        {
            if ((f == null) || (energy == null)) return null;

            var layout = energy.DescribeLayout();
            var measured = new List<EDTermEnergy>();

            for (int i = 0; i < layout.Count; i++)
            {
                int offset = layout[i].offset;
                int rows = layout[i].rows;

                double blockEnergy = 0.0;
                double maxAbs = 0.0;

                for (int r = 0; r < rows; r++)
                {
                    double v = f[offset + r];

                    blockEnergy += v * v;
                    maxAbs = System.Math.Max(maxAbs, System.Math.Abs(v));
                }

                double rms = (rows > 0) ? (System.Math.Sqrt(blockEnergy / rows)) : (0.0);

                // The layout is termInstances in order, one entry each, so index i is the term
                // whose rows these are.
                string notes = ((state != null) && (rows > 0))
                               ? (energy.termInstances[i].DescribeNotes(state.Value))
                               : ("");

                measured.Add(new EDTermEnergy(layout[i].name,
                                              rows,
                                              energy.GetConceptualWeight(layout[i].name),
                                              blockEnergy,
                                              rms,
                                              maxAbs,
                                              notes));
            }

            return measured;
        }
#endif
    }
}
#endif
