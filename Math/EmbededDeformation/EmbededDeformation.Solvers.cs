using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    public partial class EmbededDeformation
    {
        #region Solver

#if MATH_NET_AVAILABLE
        /// <summary>
        /// The DeltaEnergy stop: true once the relative improvement of the total energy - the
        /// squared residual norm, the quantity the export's total column carries - has stayed
        /// below the threshold for two consecutive iterations. Two rather than one because a
        /// single near-flat iteration mid-descent exists in the sweep data and a repeated one
        /// never resumed (2026-08-27, zero resume events over 26 runs). A threshold of zero
        /// disables the check entirely, which is the MaxIterations criteria and the default -
        /// the path every golden runs.
        /// </summary>
        private static bool ShouldStopOnDeltaEnergy(double threshold, double previousError, double error, ref int flatIterations)
        {
            if ((threshold <= 0.0) || (!double.IsFinite(previousError))) return false;

            double before = previousError * previousError;
            double after = error * error;

            double improvement = (before > 0.0) ? ((before - after) / before) : (0.0);

            if (improvement < threshold) flatIterations++;
            else flatIterations = 0;

            return flatIterations >= 2;
        }

        /// <summary>
        /// The start of an iteration: the entering state's row, the inputs the iteration runs
        /// under, and the residual it solves with.
        ///
        /// With nobody moving the inputs this is one residual evaluation, which the caller reports
        /// exactly as it always did. With a listener, the entering state is measured and reported
        /// *before* the inputs move: it is the state the previous iteration produced, and the
        /// targets still in force are the ones it was solved against. Measured after the move its
        /// row would be scored against a request it has never been solved for, where a press's row
        /// - written after the press's step - is scored against the one it has, and the two routes
        /// would export different quantities under the same column names. The residual is then
        /// evaluated again only when the inputs did move, so the second evaluation is paid per
        /// moved iteration rather than per iteration, and not at all when nobody is listening for
        /// rows.
        /// </summary>
        private Vector<double> EnterIteration(EDEnergyModel.Instance energy, EDStateView stateView, bool reportEntry, out EDIterationInputs inputs, out bool entryReported)
        {
            Vector<double> f = null;

            entryReported = false;

            if ((onIterationBegin != null) && (onIterationMeasured != null) && (reportEntry))
            {
                f = energy.EvaluateResidual(stateView);

                ReportIteration(f, energy, stateView);

                entryReported = true;
            }

            inputs = BeginIteration();

            if ((f == null) || (inputs.changed))
                f = energy.EvaluateResidual(stateView);

            return f;
        }
#endif

        public void SolveED_GN(int maxIterations, EDEnergyModel.Instance energy,
                               double damping = 1.0,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true,
                               double relativeEnergyStop = 0.0)
        {
            if (resetBeforeSolve)
                ResetDeformation();

            InitMathNet();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            // Row counts and weights are resolved once here rather than rebuilt inside every
            // residual evaluation, as the layout builder used to be. They are a function of the
            // graph and the weights, neither of which changes during a solve.
            energy.Resolve();

            TraceResidualLayout(energy);

            DebugProfiler.DebugMark(timeIteration);

            double previousError = double.NaN;
            int flatIterations = 0;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                CountSolveIteration();

                var stateView = new EDStateView(currentState);

                var f = EnterIteration(energy, stateView, true, out EDIterationInputs inputs, out bool entryReported);

                double error = f.L2Norm();

                if (!entryReported)
                    ReportIteration(f, energy, stateView);

                // The entering state's row has read the step that made it; what follows is this
                // iteration's own.
                ClearStepReport();

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                // Already solved / close enough - unless the inputs are still moving, where
                // "solved" describes a request that is about to change.
                if (!double.IsFinite(error) || ((error < residualTolerance) && (!inputs.pending)))
                {
                    break;
                }

                // Totals either side of a moved input belong to different objectives.
                if (inputs.changed)
                {
                    previousError = double.NaN;
                    flatIterations = 0;
                }

                // Checked at entry, before the Jacobian - the expensive half of an iteration that
                // would improve nothing.
                if ((!inputs.pending) && (ShouldStopOnDeltaEnergy(relativeEnergyStop, previousError, error, ref flatIterations)))
                {
                    break;
                }

                previousError = error;

                var J = energy.BuildJacobian(currentState, out double jNorm);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if (!double.IsFinite(jNorm) || jNorm < 1e-12)
                {
                    // Nothing to step along. With the inputs still moving the next iteration is
                    // another problem, so the solve goes on to it rather than ending here.
                    if (inputs.pending) continue;

                    break;
                }

                Vector<double> delta;

                try
                {
                    DebugProfiler.DebugMark(timeSolve);

                    var qr = J.QR();
                    delta = qr.Solve(-f);

                    DebugProfiler.DebugMark(timeSolve);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"SolveED failed: {ex.Message}");
                    return;
                }

                double stepNorm = delta.L2Norm();

                if (!double.IsFinite(stepNorm))
                {
                    Debug.LogError("[ED] SolveED produced non-finite delta.");
                    return;
                }

                // A step this small ends the solve - unless the inputs are still moving, where it
                // is taken and the solve goes on.
                if ((stepNorm < stepTolerance) && (!inputs.pending))
                {
                    break;
                }

                ReportStepTaken(0, 0.0, stepNorm * Math.Abs(damping));

                currentState.Apply(delta, damping);
            }

            DebugProfiler.DebugMark(timeIteration);
#else
    throw new NotImplementedException();
#endif
        }

        public void SolveED_LM(int maxIterations,
                               EDEnergyModel.Instance energy,
                               double lambda = 1e-3,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true,
                               bool adaptiveLambda = true,
                               double relativeEnergyStop = 0.0,
                               bool restartDamping = false)
        {
            if (resetBeforeSolve)
                ResetDeformation();

            InitMathNet();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            double currentLambda = lambda;

            // Row counts and weights are resolved once here rather than rebuilt inside every
            // residual evaluation, as the layout builder used to be. They are a function of the
            // graph and the weights, neither of which changes during a solve.
            energy.Resolve();

            TraceResidualLayout(energy);

            DebugProfiler.DebugMark(timeIteration);

            double previousError = double.NaN;
            int flatIterations = 0;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                CountSolveIteration();

                var stateView = new EDStateView(currentState);

                var f = EnterIteration(energy, stateView, true, out EDIterationInputs inputs, out bool entryReported);

                double error = f.L2Norm();

                if (!entryReported)
                    ReportIteration(f, energy, stateView);

                // The entering state's row has read the step that made it; what follows is this
                // iteration's own.
                ClearStepReport();

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    Debug.LogError("[ED] Residual became non-finite.");
                    return;
                }

                // Close enough ends the solve - unless the inputs are still moving, where "solved"
                // describes a request that is about to change.
                if ((error < residualTolerance) && (!inputs.pending))
                    break;

                // Totals either side of a moved input belong to different objectives.
                if (inputs.changed)
                {
                    previousError = double.NaN;
                    flatIterations = 0;
                }

                // Checked at entry, before the Jacobian - the expensive half of an iteration that
                // would improve nothing.
                if ((!inputs.pending) && (ShouldStopOnDeltaEnergy(relativeEnergyStop, previousError, error, ref flatIterations)))
                    break;

                previousError = error;

                var J = energy.BuildJacobian(currentState, out double jNorm);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                {
                    // Nothing to step along. With the inputs still moving the next iteration is
                    // another problem, so the solve goes on to it rather than ending here.
                    if (inputs.pending) continue;

                    break;
                }

                var JT = J.Transpose();
                var H = JT * J;
                var g = JT * f;

                Vector<double> delta = null;
                EDState acceptedState = null;
                bool solved = false;

                // With the restart on, every iteration's attempts begin at the solver's initial
                // damping, as a solve of one iteration does - so a solve of N iterations takes the
                // steps N presses take. Off, the damping is carried from the iteration before.
                if (restartDamping) currentLambda = lambda;

                // The damping as this iteration found it, before its attempts raise it - what it
                // goes back to if they all fail while the inputs are still moving, see below.
                double lambdaAtEntry = currentLambda;

                // What the attempts are measured against: the state they depart from.
                ReportAttempt(-1, EDAttemptOutcome.Entry, double.NaN, double.NaN, f, energy, stateView);

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var Hlm = H.Clone();

                    for (int i = 0; i < Hlm.RowCount; i++)
                        Hlm[i, i] += currentLambda;

                    try
                    {
                        DebugProfiler.DebugMark(timeSolve);

                        delta = Hlm.Solve(-g);

                        DebugProfiler.DebugMark(timeSolve);
                    }
                    catch
                    {
                        delta = null;
                    }

                    if (delta == null)
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    double stepNorm = delta.L2Norm();

                    if (!double.IsFinite(stepNorm))
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    EDState candidateState;

                    try
                    {
                        candidateState = currentState.CloneAndApply(delta, 1.0);
                    }
                    catch
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NoState, currentLambda, stepNorm, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    var candidateView = new EDStateView(candidateState);

                    var fCandidate = energy.EvaluateResidual(candidateView);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NonFinite, currentLambda, stepNorm, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    if (candidateError <= error)
                    {
                        // Recorded before the damping is lowered for the next iteration: this is
                        // the value the step was solved with.
                        ReportStepTaken(attempt, currentLambda, stepNorm);
                        ReportAttempt(attempt, EDAttemptOutcome.Accepted, currentLambda, stepNorm, fCandidate, energy, candidateView);

                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        // A step this small ends the solve - unless the inputs are still moving,
                        // where it is taken and the solve goes on.
                        if ((stepNorm < stepTolerance) && (!inputs.pending))
                        {
                            currentState = acceptedState;

                            DebugProfiler.DebugMark(timeIteration);

                            return;
                        }

                        break;
                    }

                    ReportAttempt(attempt, EDAttemptOutcome.Worse, currentLambda, stepNorm, fCandidate, energy, candidateView);

                    currentLambda *= 10.0;
                }

                if (!solved)
                {
                    // Every attempt was refused: the row that follows is a stall's.
                    ReportStepRefused(8);

                    if (!inputs.pending)
                    {
                        Debug.LogWarning("[ED] LM could not find an improving step.");
                        break;
                    }

                    // The inputs move again at the next iteration, so the solve goes on to it: a
                    // stall is a fact about this objective, and the next one is another. The
                    // damping goes back to what it was on entering this iteration - the attempts
                    // raised it by eight factors of ten against an objective that is about to be
                    // replaced, and carried over it would hold the next iterations to steps too
                    // small to follow the inputs.
                    Debug.LogWarning("[ED] LM could not find an improving step; the inputs are still moving, so the solve goes on with them.");

                    currentLambda = lambdaAtEntry;

                    continue;
                }

                currentState = acceptedState;
            }

            DebugProfiler.DebugMark(timeIteration);
#else
    throw new NotImplementedException();
#endif
        }

        // The configuration itself lives on EDDiagnostics, because verification mode is what decides
        // it and because these providers are process-global: whoever sets them last wins for the
        // rest of the session. Kept as a named call at the top of each solver so it is visible that
        // a solve configures its own environment rather than trusting what it finds.
        static void InitMathNet() => EDDiagnostics.ApplyMathNetProviders();

        public void SolveED_Nav(int maxIterations,
                                EDEnergyModel.Instance energy,
                                double lambda = 1e-3,
                                double residualTolerance = 1e-5,
                                double stepTolerance = 1e-6,
                                bool resetBeforeSolve = true,
                                bool adaptiveLambda = true,
                                bool choleskyFactorization = false,
                                double relativeEnergyStop = 0.0,
                                bool restartDamping = false)
        {
            if (resetBeforeSolve)
                ResetDeformation();

            InitMathNet();

#if MATH_NET_AVAILABLE
            if (currentState == null)
            {
                currentState = new EDState(nodes.Count);
                ComputeClearance(currentState);
            }

            double currentLambda = lambda;
            // Row counts and weights are resolved once here rather than rebuilt inside every
            // residual evaluation, as the layout builder used to be. They are a function of the
            // graph and the weights, neither of which changes during a solve.
            energy.Resolve();

            TraceResidualLayout(energy);

            int iter = 0;

            DebugProfiler.DebugMark(timeIteration);

            double previousError = double.NaN;
            int flatIterations = 0;

            for (iter = 0; iter < maxIterations; iter++)
            {
                CountSolveIteration();

                var stateView = new EDStateView(currentState);

                // The state entering the first iteration of a *continuing* solve is the previous
                // solve's accepted state, and the export already holds that row - reporting it
                // again is what made every accepted state appear twice under per-press driving
                // (Run Iteration), the twin-row pattern of 2026-08-25. A from-reset solve still
                // reports it: there it is the rest state, which nothing else records. The console
                // log below is deliberately unconditional - the debug window keeps showing the
                // entering state either way.
                bool reportEntry = (iter > 0) || (resetBeforeSolve);

                var f = EnterIteration(energy, stateView, reportEntry, out EDIterationInputs inputs, out bool entryReported);

                double error = f.L2Norm();

                LogResidualEnergies(f, energy, iter);

                if ((reportEntry) && (!entryReported))
                    ReportIteration(f, energy, stateView);

                // The entering state's row has read the step that made it; what follows is this
                // iteration's own.
                ClearStepReport();

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    Debug.LogError($"[ED] Residual became non-finite after {iter} iterations.");
                    return;
                }

                // Close enough ends the solve - unless the inputs are still moving, where "solved"
                // describes a request that is about to change.
                if ((error < residualTolerance) && (!inputs.pending))
                {
                    break;
                }

                // Totals either side of a moved input belong to different objectives.
                if (inputs.changed)
                {
                    previousError = double.NaN;
                    flatIterations = 0;
                }

                // Checked at entry, before the Jacobian - the expensive half of an iteration that
                // would improve nothing.
                if ((!inputs.pending) && (ShouldStopOnDeltaEnergy(relativeEnergyStop, previousError, error, ref flatIterations)))
                {
                    break;
                }

                previousError = error;

                var J = energy.BuildJacobian(currentState, out double jNorm);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                {
                    // Nothing to step along. With the inputs still moving the next iteration is
                    // another problem, so the solve goes on to it rather than ending here.
                    if (inputs.pending) continue;

                    break;
                }

                var JT = J.Transpose();

                var H = JT * J; // approximate Hessian
                var g = JT * f; // gradient term

                Vector<double> delta = null;
                EDState acceptedState = null;
                bool solved = false;

                // With the restart on, every iteration's attempts begin at the solver's initial
                // damping, as a solve of one iteration does - so a solve of N iterations takes the
                // steps N presses take. Off, the damping is carried from the iteration before.
                if (restartDamping) currentLambda = lambda;

                // The damping as this iteration found it, before its attempts raise it - what it
                // goes back to if they all fail while the inputs are still moving, see below.
                double lambdaAtEntry = currentLambda;

                // What the attempts are measured against: the state they depart from.
                ReportAttempt(-1, EDAttemptOutcome.Entry, double.NaN, double.NaN, f, energy, stateView);

                // Try current lambda, optionally increasing it if solve or step is bad.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    DebugProfiler.DebugMark(timeSolve);

                    if (choleskyFactorization)
                    {
                        if (!TrySolveCholeskyWithDamping(H, g, currentLambda, out delta, out double usedLambda))
                        {
                            currentLambda = usedLambda;

                            ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, energy, null);

                            continue;
                        }

                        currentLambda = usedLambda;
                    }
                    else
                    {
                        var Hlm = H.Clone();

                        for (int i = 0; i < Hlm.RowCount; i++)
                            Hlm[i, i] += currentLambda;

                        try
                        {
                            delta = Hlm.Solve(-g);
                        }
                        catch
                        {
                            delta = null;
                        }
                    }

                    DebugProfiler.DebugMark(timeSolve);

                    if (delta == null)
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    double stepNorm = delta.L2Norm();

                    if (!double.IsFinite(stepNorm))
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    EDState candidateState;

                    try
                    {
                        candidateState = currentState.CloneAndApply(delta, 1.0);
                        ComputeClearance(candidateState);
                    }
                    catch
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NoState, currentLambda, stepNorm, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    var candidateView = new EDStateView(candidateState);

                    var fCandidate = energy.EvaluateResidual(candidateView);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        ReportAttempt(attempt, EDAttemptOutcome.NonFinite, currentLambda, stepNorm, null, energy, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    // Accept only if it improves the residual.
                    if (candidateError <= error)
                    {
                        // Recorded before the damping is lowered for the next iteration: this is
                        // the value the step was solved with.
                        ReportStepTaken(attempt, currentLambda, stepNorm);
                        ReportAttempt(attempt, EDAttemptOutcome.Accepted, currentLambda, stepNorm, fCandidate, energy, candidateView);

                        EDDiagnostics.Trace($"[iter {iter}] accepted attempt {attempt} lambda {EDDiagnostics.F(currentLambda)} step {EDDiagnostics.F(stepNorm)} candidateError {EDDiagnostics.F(candidateError)}");

                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        // A step this small ends the solve - unless the inputs are still moving,
                        // where it is taken and the solve goes on.
                        if ((stepNorm < stepTolerance) && (!inputs.pending))
                        {
                            currentState = acceptedState;

                            DebugProfiler.DebugMark(timeIteration);

                            Debug.Log($"Ran {iter} iterations...");

                            return;
                        }

                        break;
                    }

                    ReportAttempt(attempt, EDAttemptOutcome.Worse, currentLambda, stepNorm, fCandidate, energy, candidateView);

                    currentLambda *= 10.0;
                }

                if (!solved)
                {
                    // Every attempt was refused: the row that follows is a stall's.
                    ReportStepRefused(8);

                    if (!inputs.pending)
                    {
                        Debug.LogWarning("[ED] LM could not find an improving step.");
                        break;
                    }

                    // The inputs move again at the next iteration, so the solve goes on to it: a
                    // stall is a fact about this objective, and the next one is another. The
                    // damping goes back to what it was on entering this iteration - the attempts
                    // raised it by eight factors of ten against an objective that is about to be
                    // replaced, and carried over it would hold the next iterations to steps too
                    // small to follow the inputs.
                    Debug.LogWarning("[ED] LM could not find an improving step; the inputs are still moving, so the solve goes on with them.");

                    currentLambda = lambdaAtEntry;

                    continue;
                }

                currentState = acceptedState;
                ComputeClearance(currentState);
            }

            DebugProfiler.DebugMark(timeIteration);

            var acceptedView = new EDStateView(currentState);
            var acceptedResidual = energy.EvaluateResidual(acceptedView);

            LogResidualEnergies(acceptedResidual, energy, iter);
            ReportIteration(acceptedResidual, energy, acceptedView);
#else
    throw new NotImplementedException();
#endif
        }

        bool TrySolveCholeskyWithDamping(Matrix<double> H, Vector<double> g, double initialLambda, out Vector<double> delta, out double usedLambda)
        {
            delta = null;
            usedLambda = initialLambda;

            const int maxAttempts = 8;
            const double lambdaMultiplier = 10.0;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var Hlm = H.Clone();

                for (int i = 0; i < Hlm.RowCount; i++)
                    Hlm[i, i] += usedLambda;

                try
                {
                    var chol = Hlm.Cholesky();
                    delta = chol.Solve(-g);
                    return delta.All(v => double.IsFinite(v));
                }
                catch
                {
                    usedLambda *= lambdaMultiplier;
                }
            }

            return false;
        }

        #endregion
    }
}
#endif
