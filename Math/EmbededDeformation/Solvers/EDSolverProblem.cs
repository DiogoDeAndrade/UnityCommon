using MathNet.Numerics.LinearAlgebra;

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
        /// <summary>The candidate was evaluated and its total is higher than the entry's - under LBFGS, not lower by the line search's sufficient-decrease margin.</summary>
        Worse,
        /// <summary>No step came out of the linear solve, or the one that did is not finite.</summary>
        NoStep,
        /// <summary>The step could not be applied, or the candidate state could not be prepared.</summary>
        NoState,
        /// <summary>The candidate's residual is not finite.</summary>
        NonFinite,
    }

    /// <summary>The spans a solver marks on the problem's timers.</summary>
    public enum EDSolverTimer
    {
        Iteration,
        NormalEquations,
        Solve,
    }

    public enum EDSolverLogLevel
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>Why a solve ended - reported once per solve, with the iteration it ended in.</summary>
    public enum EDSolveEnd
    {
        /// <summary>Every iteration it was given ran.</summary>
        RanItsIterations,
        /// <summary>The residual at an iteration's entry was not finite.</summary>
        NonFiniteResidual,
        /// <summary>The residual norm was under the tolerance at an iteration's entry.</summary>
        ResidualTolerance,
        /// <summary>The total improved by under the threshold twice running (the DeltaEnergy stop).</summary>
        EnergyStop,
        /// <summary>Nothing to step along: the Jacobian, or the gradient, is zero or not finite.</summary>
        NoDescentDirection,
        /// <summary>The step taken was shorter than the tolerance.</summary>
        StepTolerance,
        /// <summary>Every attempt of an iteration was refused (a damped solver), or the line search found no decrease (LBFGS).</summary>
        NoImprovingStep,
        /// <summary>The linear solve threw, or gave a step that is not finite (Gauss-Newton).</summary>
        LinearSolveFailed,
    }

    /// <summary>
    /// What a solver works on: the minimisation of the squared norm of a residual vector over a
    /// parameter state, with everything else a solve does around it - the per-iteration inputs, the
    /// records, the traces, the timers - asked of the problem rather than reached into.
    ///
    /// Built 2026-10-22 (Diogo's call) so the solver objects own their loops instead of
    /// <see cref="EmbededDeformation"/>, and any solver runs on any problem: an EmbededDeformation
    /// (<see cref="EmbededDeformation.CreateSolverProblem"/>) and the meshless method alike. The
    /// calls are exactly the ones the loops made on the deformation, in the order they made them, so
    /// a loop moved behind this interface does on a deformation what it did inside it.
    ///
    /// The problem holds the current state and the linearisation at it. The state type is the
    /// problem's own; a solver only creates candidates from steps, evaluates them and accepts one.
    /// Every vector is in the problem's parameter order.
    /// </summary>
    public interface IEDSolverProblem<TState> where TState : class
    {
        /// <summary>The state the solve stands on.</summary>
        TState current { get; }

        /// <summary>Back to rest, the iteration count with it.</summary>
        void Reset();

        /// <summary>
        /// Everything a solve prepares before its first iteration. With refreshesCandidates the
        /// problem also prepares what <see cref="RefreshCandidate"/> keeps current - on a
        /// deformation, the clearances, when the model reads them.
        /// </summary>
        void BeginSolve(bool refreshesCandidates);

        /// <summary>Counted once per iteration, for the problem's own reports.</summary>
        void CountIteration();

        /// <summary>
        /// The start of an iteration: asks the driver for the inputs it runs under, and returns the
        /// current state's residual against them. With reportEntry, and someone moving the inputs,
        /// the entering state is reported before they move (entryReported says so).
        /// </summary>
        Vector<double> EnterIteration(bool reportEntry, out EDIterationInputs inputs, out bool entryReported);

        /// <summary>The current state's row, from a residual already evaluated at it.</summary>
        void ReportIteration(Vector<double> residual);

        /// <summary>The per-term breakdown of a residual, to the console.</summary>
        void LogResidualEnergies(Vector<double> residual, int iteration);

        /// <summary>The row that follows describes no step yet.</summary>
        void ClearStepReport();

        /// <summary>
        /// Linearises at the current state, kept until the next call. The norm is the Jacobian's
        /// Frobenius norm, zero when nothing can be stepped along.
        /// </summary>
        void BuildJacobian(out double jacobianNorm);

        /// <summary>H = JᵀJ and g = Jᵀf of the last linearisation and the residual given.</summary>
        void FormNormalEquations(Vector<double> residual, bool sparse, out Matrix<double> H, out Vector<double> g);

        /// <summary>The undamped least-squares step, J d = -f; may throw.</summary>
        Vector<double> SolveLinearizedLeastSquares(Vector<double> residual);

        /// <summary>Jᵀf: half the gradient of the squared residual norm.</summary>
        Vector<double> HalfGradient(Vector<double> residual);

        /// <summary>A new state, the current one plus scale times the step; may throw.</summary>
        TState MakeCandidate(Vector<double> step, double scale);

        /// <summary>Brings a candidate's derived data up to date - what BeginSolve(true) prepared; may throw.</summary>
        void RefreshCandidate(TState candidate);

        /// <summary>The residual of any state, current or candidate.</summary>
        Vector<double> EvaluateResidual(TState state);

        /// <summary>The candidate becomes the current state.</summary>
        void Accept(TState candidate);

        /// <summary>The current state moved by scale times the step, in place.</summary>
        void ApplyStep(Vector<double> step, double scale);

        /// <summary>One attempt, or the state the attempts depart from; the state is the one the residual was evaluated at, null where there was none.</summary>
        void ReportAttempt(int attempt, EDAttemptOutcome outcome, double damping, double stepNorm, Vector<double> residual, TState state);

        /// <summary>The step that produced the next row's state.</summary>
        void ReportStepTaken(int attempt, double damping, double stepNorm);

        /// <summary>The next row's state was produced by no step: every attempt was refused.</summary>
        void ReportStepRefused(int attempts);

        /// <summary>Why the solve ended, once per solve.</summary>
        void ReportEnd(EDSolveEnd end, int iteration);

        /// <summary>A line of the verification trace.</summary>
        void Trace(string line);

        void Log(EDSolverLogLevel level, string message);

        /// <summary>Opens or closes a span on one of the problem's timers.</summary>
        void Mark(EDSolverTimer timer);
    }
}
#endif
