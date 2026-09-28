using System;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// What the solver did at the iteration that produced the state it reports next: whether a
    /// step was taken, at which attempt and with what damping, how long the step was, and how many
    /// attempts were refused on the way.
    ///
    /// It is a record and nothing reads it back into the solve. It exists because the exported
    /// rows said what a state was and never how the solver got to it, so the damping a press was
    /// accepted at had to be read off the terminal node's shortfall (2026-09-28, queue section
    /// 30) - to four decimals, blind to the first two attempts, and blind altogether once the
    /// node had arrived.
    ///
    /// The default value is "no step was tried", which is what the rest state's row carries, and
    /// what a row carries when the iteration ended before its attempts.
    /// </summary>
    public struct EDStepReport
    {
        /// <summary>A step was accepted and applied.</summary>
        public bool taken;

        /// <summary>The attempt the step was accepted at, counted from 0. Meaningful only when taken.</summary>
        public int attempt;

        /// <summary>
        /// The attempts refused in the iteration: those before the accepted one, or all of them
        /// when none was accepted, which is a stall.
        /// </summary>
        public int refused;

        /// <summary>
        /// The damping on the diagonal of the normal equations at the accepted attempt. It is the
        /// value the step was solved with, so under the Cholesky path it includes what that path
        /// added on its own to factorise. Zero for Gauss-Newton, which has none. Meaningful only
        /// when taken.
        /// </summary>
        public double damping;

        /// <summary>The length of the step applied, over every parameter. Meaningful only when taken.</summary>
        public double stepNorm;

        /// <summary>The iteration tried at least one attempt, whatever came of it.</summary>
        public bool attempted => (taken) || (refused > 0);
    }

    public partial class EmbededDeformation
    {
        // Not serialized: it describes a step, and a step is reported in the solve that takes it.
        [NonSerialized]
        private EDStepReport stepReport;

        /// <summary>
        /// The step that produced the state the solver reports next. Valid inside
        /// <see cref="onIterationMeasured"/>: every solver path clears it once the entering state
        /// has been reported and writes it when its attempts are over, so a row reads the step
        /// that made its state and never the one that follows it.
        /// </summary>
        public EDStepReport lastStep => stepReport;

        private void ClearStepReport()
        {
            stepReport = default;
        }

        private void ReportStepTaken(int attempt, double damping, double stepNorm)
        {
            stepReport = new EDStepReport { taken = true, attempt = attempt, refused = attempt, damping = damping, stepNorm = stepNorm };
        }

        private void ReportStepRefused(int attempts)
        {
            stepReport = new EDStepReport { taken = false, attempt = -1, refused = attempts, damping = double.NaN, stepNorm = double.NaN };
        }
    }
}
#endif
