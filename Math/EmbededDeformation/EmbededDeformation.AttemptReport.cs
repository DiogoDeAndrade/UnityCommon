using System;
using System.Collections.Generic;
using UnityEngine;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// What became of one of an iteration's attempts, or that the record is of the state the
    /// iteration's attempts depart from.
    /// </summary>
    public enum EDAttemptOutcome
    {
        /// <summary>Not an attempt: the state the iteration departs from, scored against the inputs in force for it.</summary>
        Entry,
        /// <summary>The candidate's total is no higher than the entry's, and its step is the one taken.</summary>
        Accepted,
        /// <summary>The candidate was evaluated and its total is higher than the entry's.</summary>
        Worse,
        /// <summary>No step came out of the linear solve, or the one that did is not finite.</summary>
        NoStep,
        /// <summary>The step could not be applied, or the candidate state could not be prepared.</summary>
        NoState,
        /// <summary>The candidate's residual is not finite.</summary>
        NonFinite,
    }

    /// <summary>
    /// One of the damped solver's attempts, refused or accepted, or the state they depart from.
    ///
    /// The exported rows are the path the solve took, and the step report beside them says at
    /// which attempt each step was accepted; neither says what a refused attempt was refused on.
    /// At presses 10 to 15 of the K 10 run every attempt under a damping of 1,000 was refused, the
    /// undamped one included, and at press 16 the undamped one was accepted (2026-09-29, queue
    /// section 31), and nothing on file could say which term had risen. This is that record.
    ///
    /// It is a record and nothing reads it back into the solve.
    /// </summary>
    public struct EDAttemptReport
    {
        /// <summary>The attempt, counted from 0; -1 for the entry state.</summary>
        public int attempt;

        public EDAttemptOutcome outcome;

        /// <summary>
        /// The damping the attempt was solved with, which under the Cholesky path includes what
        /// that path added on its own to factorise. NaN for the entry state.
        /// </summary>
        public double damping;

        /// <summary>The length of the attempt's step, over every parameter. NaN where there was none.</summary>
        public double stepNorm;

        /// <summary>
        /// The total energy of the state the record describes - the squared residual norm, the
        /// quantity the export's total column carries. NaN where no residual was evaluated.
        /// </summary>
        public double total;

        /// <summary>
        /// That total by term. A term's own columns are there only for the terms that say they
        /// describe attempts (<see cref="EDResidualTerm.Instance.describesAttempts"/>): for the
        /// others they are a second measurement of the state, and an attempt is not worth one.
        /// Null where no residual was evaluated.
        /// </summary>
        public IReadOnlyList<EDTermEnergy> energies;
    }

    public partial class EmbededDeformation
    {
        /// <summary>
        /// Set by the driver to receive every attempt of the damped solvers, and before them the
        /// state they depart from. Null, the default, is the path every golden runs and costs a
        /// comparison per attempt. Gauss-Newton makes no attempts and the translation-only solver
        /// has no iterations; neither calls it.
        ///
        /// A delegate is not serialized, so a domain reload clears it - the driver re-assigns it on
        /// every solve rather than trusting what survived, as it does onIterationMeasured.
        /// </summary>
        [NonSerialized]
        public Action<EDAttemptReport> onAttemptMeasured;

#if MATH_NET_AVAILABLE
        private static readonly Predicate<EDResidualTerm.Instance> describesAttempts = (instance) => instance.describesAttempts;

        /// <summary>
        /// Hands one record to the listener. The residual is one the solver has already evaluated,
        /// so what is paid here is a sum over it and the own columns of the terms that describe
        /// attempts; timed with the rest of the export, so the report shows what the record costs.
        /// The state is the one the residual was evaluated at, null where there was none.
        /// </summary>
        private void ReportAttempt(int attempt, EDAttemptOutcome outcome, double damping, double stepNorm, Vector<double> residual, EDEnergyModel.Instance energy, EDStateView? state)
        {
            if (onAttemptMeasured == null) return;

            DebugProfiler.DebugMark(timeIterationExport);

            double total = double.NaN;

            if (residual != null)
            {
                double norm = residual.L2Norm();

                total = norm * norm;
            }

            onAttemptMeasured(new EDAttemptReport
            {
                attempt = attempt,
                outcome = outcome,
                damping = damping,
                stepNorm = stepNorm,
                total = total,
                energies = (residual != null) ? (MeasureTermEnergies(residual, energy, state, describesAttempts)) : (null),
            });

            DebugProfiler.DebugMark(timeIterationExport);
        }
#endif
    }
}
#endif
