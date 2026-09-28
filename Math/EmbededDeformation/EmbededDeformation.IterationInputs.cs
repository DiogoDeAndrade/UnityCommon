using System;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// What the driver did to the problem's inputs when an iteration began - the answer to
    /// <see cref="EmbededDeformation.onIterationBegin"/>.
    ///
    /// The default value, both flags false, is "nothing moved and nothing will", which is what a
    /// solve with no listener gets: every solver path reads it and behaves exactly as it did before
    /// the callback existed.
    /// </summary>
    public struct EDIterationInputs
    {
        /// <summary>
        /// The inputs the residual reads - the handle targets - were moved by this call. The totals
        /// either side of it belong to different objectives, so the solver never compares them.
        /// </summary>
        public bool changed;

        /// <summary>
        /// They will move again at a later iteration. While this holds the solve is not allowed to
        /// finish: a state that stopped here would be solved for a request nobody made.
        /// </summary>
        public bool pending;
    }

    public partial class EmbededDeformation
    {
        /// <summary>
        /// Set by the driver to move the problem's inputs as a solve proceeds - the terminals'
        /// targets interpolated from rest to their end pose, which is what it was built for
        /// (2026-09-28). Called by the three term solvers at the start of every iteration, before
        /// the residual the iteration solves with is evaluated, with the number of iterations run
        /// since the state last started from rest. A press is a one-iteration solve, so presses and
        /// a single long solve follow the same schedule by construction.
        ///
        /// The callback owns the inputs and the solver owns nothing of them: it is asked only
        /// whether they moved and whether they will move again, and it is what it does with those
        /// two answers that keeps a solve honest - see <see cref="EDIterationInputs"/>.
        ///
        /// Null, the default, is the path every golden runs, and costs an integer increment.
        /// The translation-only solver has no iterations and never calls it.
        ///
        /// A delegate is not serialized, so a domain reload clears it - the driver re-assigns it on
        /// every solve rather than trusting what survived, as it does onIterationMeasured.
        /// </summary>
        [NonSerialized]
        public Func<int, EDIterationInputs> onIterationBegin;

        // Solver iterations run since the state last started from rest. Serialized with the state
        // it counts for: the state survives a domain reload, and a count that did not would restart
        // the inputs' schedule under a state that is already most of the way along it.
        //
        // Counts attempts rather than accepted steps. An iteration that finds no improving step is
        // still an iteration, and the schedule has to move on past it - a count that waited for a
        // step would hold the inputs where the stall happened, which is where it would stall again.
        [SerializeField, HideInInspector]
        private int iterationsSinceReset;

        /// <summary>How many solver iterations have run since the state last started from rest.</summary>
        public int iterationsSinceLastReset => iterationsSinceReset;

        /// <summary>
        /// The start of one solver iteration: counts it, and asks the driver what the inputs are
        /// for it. Counted whether or not anyone is listening, so a listener attached later finds
        /// the count where the state is rather than at zero.
        /// </summary>
        private EDIterationInputs BeginIteration()
        {
            int index = iterationsSinceReset;

            iterationsSinceReset++;

            return (onIterationBegin != null) ? (onIterationBegin(index)) : (default);
        }
    }
}
#endif
