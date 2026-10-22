using System;
using MathNet.Numerics.LinearAlgebra;

#if UC_ENABLE_ED
namespace UC.ED
{
    // EmbededDeformation.SolveED_Nav, moved here verbatim on 2026-10-22: the same statements in the
    // same order, each call into the deformation now the problem's (IEDSolverProblem says which
    // did what), plus the one report of why the solve ended. Goldens 4 to 7 run it.
    public partial class EDSolverNavLM
    {
        internal static void RunNavigation<TState>(IEDSolverProblem<TState> problem,
                                                   int maxIterations,
                                                   double lambda = 1e-3,
                                                   double residualTolerance = 1e-5,
                                                   double stepTolerance = 1e-6,
                                                   bool resetBeforeSolve = true,
                                                   bool adaptiveLambda = true,
                                                   bool choleskyFactorization = false,
                                                   double relativeEnergyStop = 0.0,
                                                   bool restartDamping = false,
                                                   bool sparseNormalEquations = false) where TState : class
        {
            if (resetBeforeSolve)
                problem.Reset();

            double currentLambda = lambda;

            // Row counts and weights are resolved once here rather than rebuilt inside every
            // residual evaluation, as the layout builder used to be. They are a function of the
            // graph and the weights, neither of which changes during a solve.
            //
            // The clearances are measured only for a model that reads them (2026-10-08). They used
            // to be measured on every reset, on the candidate of every attempt and again on the
            // state accepted, whatever the model held - a pass over every structure segment each
            // time, paid by a model with no clearance term for a number nothing in it read. With
            // such a term the passes below are the ones there always were, on the same states,
            // less the one after an acceptance, which measured again a state measured as a
            // candidate. Everything that reads them outside a term asks for itself, through
            // EnsureClearance. The entering state's are ensured here: a reset no longer measures
            // them, and a state this solve continues from may have been left by a solve under a
            // model that read none.
            problem.BeginSolve(true);

            int iter = 0;

            problem.Mark(EDSolverTimer.Iteration);

            double previousError = double.NaN;
            int flatIterations = 0;

            EDSolveEnd end = EDSolveEnd.RanItsIterations;
            int endIteration = maxIterations;

            for (iter = 0; iter < maxIterations; iter++)
            {
                problem.CountIteration();

                // The state entering the first iteration of a *continuing* solve is the previous
                // solve's accepted state, and the export already holds that row - reporting it
                // again is what made every accepted state appear twice under per-press driving
                // (Run Iteration), the twin-row pattern of 2026-08-25. A from-reset solve still
                // reports it: there it is the rest state, which nothing else records. The console
                // log below is deliberately unconditional - the debug window keeps showing the
                // entering state either way.
                bool reportEntry = (iter > 0) || (resetBeforeSolve);

                var f = problem.EnterIteration(reportEntry, out EDIterationInputs inputs, out bool entryReported);

                double error = f.L2Norm();

                problem.LogResidualEnergies(f, iter);

                if ((reportEntry) && (!entryReported))
                    problem.ReportIteration(f);

                // The entering state's row has read the step that made it; what follows is this
                // iteration's own.
                problem.ClearStepReport();

                problem.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    problem.Log(EDSolverLogLevel.Error, $"[ED] Residual became non-finite after {iter} iterations.");
                    problem.ReportEnd(EDSolveEnd.NonFiniteResidual, iter);
                    return;
                }

                // Close enough ends the solve - unless the inputs are still moving, where "solved"
                // describes a request that is about to change.
                if ((error < residualTolerance) && (!inputs.pending))
                {
                    end = EDSolveEnd.ResidualTolerance;
                    endIteration = iter;
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
                if ((!inputs.pending) && (EDSolver.ShouldStopOnDeltaEnergy(relativeEnergyStop, previousError, error, ref flatIterations)))
                {
                    end = EDSolveEnd.EnergyStop;
                    endIteration = iter;
                    break;
                }

                previousError = error;

                problem.BuildJacobian(out double jNorm);

                problem.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                {
                    // Nothing to step along. With the inputs still moving the next iteration is
                    // another problem, so the solve goes on to it rather than ending here.
                    if (inputs.pending) continue;

                    end = EDSolveEnd.NoDescentDirection;
                    endIteration = iter;
                    break;
                }

                problem.Mark(EDSolverTimer.NormalEquations);

                // H is the approximate Hessian, g the gradient term; the dense path is the
                // transpose and two products the loop always made, the sparse one the option's.
                problem.FormNormalEquations(f, sparseNormalEquations, out Matrix<double> H, out Vector<double> g);

                problem.Mark(EDSolverTimer.NormalEquations);

                Vector<double> delta = null;
                TState acceptedState = null;
                bool solved = false;

                // With the restart on, every iteration's attempts begin at the solver's initial
                // damping, as a solve of one iteration does - so a solve of N iterations takes the
                // steps N presses take. Off, the damping is carried from the iteration before.
                if (restartDamping) currentLambda = lambda;

                // The damping as this iteration found it, before its attempts raise it - what it
                // goes back to if they all fail while the inputs are still moving, see below.
                double lambdaAtEntry = currentLambda;

                // What the attempts are measured against: the state they depart from.
                problem.ReportAttempt(-1, EDAttemptOutcome.Entry, double.NaN, double.NaN, f, problem.current);

                // Try current lambda, optionally increasing it if solve or step is bad.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    problem.Mark(EDSolverTimer.Solve);

                    if (choleskyFactorization)
                    {
                        if (!EDSolver.TrySolveCholeskyWithDamping(H, g, currentLambda, out delta, out double usedLambda))
                        {
                            currentLambda = usedLambda;

                            problem.ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, null);

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

                    problem.Mark(EDSolverTimer.Solve);

                    if (delta == null)
                    {
                        problem.ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    double stepNorm = delta.L2Norm();

                    if (!double.IsFinite(stepNorm))
                    {
                        problem.ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    TState candidateState;

                    try
                    {
                        candidateState = problem.MakeCandidate(delta, 1.0);

                        problem.RefreshCandidate(candidateState);
                    }
                    catch
                    {
                        problem.ReportAttempt(attempt, EDAttemptOutcome.NoState, currentLambda, stepNorm, null, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    var fCandidate = problem.EvaluateResidual(candidateState);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        problem.ReportAttempt(attempt, EDAttemptOutcome.NonFinite, currentLambda, stepNorm, null, null);

                        currentLambda *= 10.0;
                        continue;
                    }

                    // Accept only if it improves the residual.
                    if (candidateError <= error)
                    {
                        // Recorded before the damping is lowered for the next iteration: this is
                        // the value the step was solved with.
                        problem.ReportStepTaken(attempt, currentLambda, stepNorm);
                        problem.ReportAttempt(attempt, EDAttemptOutcome.Accepted, currentLambda, stepNorm, fCandidate, candidateState);

                        problem.Trace($"[iter {iter}] accepted attempt {attempt} lambda {EDDiagnostics.F(currentLambda)} step {EDDiagnostics.F(stepNorm)} candidateError {EDDiagnostics.F(candidateError)}");

                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        // A step this small ends the solve - unless the inputs are still moving,
                        // where it is taken and the solve goes on.
                        if ((stepNorm < stepTolerance) && (!inputs.pending))
                        {
                            problem.Accept(acceptedState);

                            problem.Mark(EDSolverTimer.Iteration);

                            problem.Log(EDSolverLogLevel.Info, $"Ran {iter} iterations...");

                            problem.ReportEnd(EDSolveEnd.StepTolerance, iter);

                            return;
                        }

                        break;
                    }

                    problem.ReportAttempt(attempt, EDAttemptOutcome.Worse, currentLambda, stepNorm, fCandidate, candidateState);

                    currentLambda *= 10.0;
                }

                if (!solved)
                {
                    // Every attempt was refused: the row that follows is a stall's.
                    problem.ReportStepRefused(8);

                    if (!inputs.pending)
                    {
                        problem.Log(EDSolverLogLevel.Warning, "[ED] LM could not find an improving step.");

                        end = EDSolveEnd.NoImprovingStep;
                        endIteration = iter;
                        break;
                    }

                    // The inputs move again at the next iteration, so the solve goes on to it: a
                    // stall is a fact about this objective, and the next one is another. The
                    // damping goes back to what it was on entering this iteration - the attempts
                    // raised it by eight factors of ten against an objective that is about to be
                    // replaced, and carried over it would hold the next iterations to steps too
                    // small to follow the inputs.
                    problem.Log(EDSolverLogLevel.Warning, "[ED] LM could not find an improving step; the inputs are still moving, so the solve goes on with them.");

                    currentLambda = lambdaAtEntry;

                    continue;
                }

                // The accepted state is the candidate, whose clearances were measured when it was
                // tried, if the model reads any.
                problem.Accept(acceptedState);
            }

            problem.Mark(EDSolverTimer.Iteration);

            var acceptedResidual = problem.EvaluateResidual(problem.current);

            problem.LogResidualEnergies(acceptedResidual, iter);
            problem.ReportIteration(acceptedResidual);

            problem.ReportEnd(end, endIteration);
        }
    }
}
#endif
