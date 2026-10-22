using System;
using MathNet.Numerics.LinearAlgebra;

#if UC_ENABLE_ED
namespace UC.ED
{
    // EmbededDeformation.SolveED_LM, moved here verbatim on 2026-10-22: the same statements in the
    // same order, each call into the deformation now the problem's (IEDSolverProblem says which
    // did what), plus the one report of why the solve ended. Golden 3 runs it, and the meshless
    // method under any EDSolverLM asset.
    public partial class EDSolverLM
    {
        internal static void RunLevenbergMarquardt<TState>(IEDSolverProblem<TState> problem,
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
            problem.BeginSolve(false);

            problem.Mark(EDSolverTimer.Iteration);

            double previousError = double.NaN;
            int flatIterations = 0;

            EDSolveEnd end = EDSolveEnd.RanItsIterations;
            int endIteration = maxIterations;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                problem.CountIteration();

                var f = problem.EnterIteration(true, out EDIterationInputs inputs, out bool entryReported);

                double error = f.L2Norm();

                if (!entryReported)
                    problem.ReportIteration(f);

                // The entering state's row has read the step that made it; what follows is this
                // iteration's own.
                problem.ClearStepReport();

                problem.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    problem.Log(EDSolverLogLevel.Error, "[ED] Residual became non-finite.");
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

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    if (choleskyFactorization)
                    {
                        // The navigation loop's branch: the factorisation raises the damping itself
                        // until the matrix is positive definite, and the damping it ended at is the
                        // one this attempt is reported and carried on with.
                        problem.Mark(EDSolverTimer.Solve);

                        bool factorised = EDSolver.TrySolveCholeskyWithDamping(H, g, currentLambda, out delta, out double usedLambda);

                        problem.Mark(EDSolverTimer.Solve);

                        currentLambda = usedLambda;

                        if (!factorised)
                        {
                            problem.ReportAttempt(attempt, EDAttemptOutcome.NoStep, currentLambda, double.NaN, null, null);

                            continue;
                        }
                    }
                    else
                    {
                        var Hlm = H.Clone();

                        for (int i = 0; i < Hlm.RowCount; i++)
                            Hlm[i, i] += currentLambda;

                        try
                        {
                            problem.Mark(EDSolverTimer.Solve);

                            delta = Hlm.Solve(-g);

                            problem.Mark(EDSolverTimer.Solve);
                        }
                        catch
                        {
                            delta = null;
                        }
                    }

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

                    if (candidateError <= error)
                    {
                        // Recorded before the damping is lowered for the next iteration: this is
                        // the value the step was solved with.
                        problem.ReportStepTaken(attempt, currentLambda, stepNorm);
                        problem.ReportAttempt(attempt, EDAttemptOutcome.Accepted, currentLambda, stepNorm, fCandidate, candidateState);

                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        // A step this small ends the solve - unless the inputs are still moving,
                        // where it is taken and the solve goes on.
                        if ((stepNorm < stepTolerance) && (!inputs.pending))
                        {
                            problem.Accept(acceptedState);

                            problem.ReportEnd(EDSolveEnd.StepTolerance, iter);

                            problem.Mark(EDSolverTimer.Iteration);

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

                problem.Accept(acceptedState);
            }

            problem.ReportEnd(end, endIteration);

            problem.Mark(EDSolverTimer.Iteration);
        }
    }
}
#endif
