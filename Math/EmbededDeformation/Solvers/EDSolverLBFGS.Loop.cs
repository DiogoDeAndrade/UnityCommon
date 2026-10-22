using System;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;

#if UC_ENABLE_ED
namespace UC.ED
{
    // LBFGS on any IEDSolverProblem (2026-10-22): Nocedal's two-loop recursion on the gradient of
    // the total, 2 Jᵀf, and a backtracking line search on the total alone. A pair (s, y) is
    // completed one iteration late: the step is known when it is taken, the change in gradient
    // across it only when the next iteration has linearised at its end, which is where the
    // gradient is computed anyway - so a line-search trial costs one residual, never a Jacobian.
    public partial class EDSolverLBFGS
    {
        internal static void RunLBFGS<TState>(IEDSolverProblem<TState> problem,
                                              int maxIterations,
                                              int memory = 10,
                                              double sufficientDecrease = 1e-4,
                                              double backtrackFactor = 0.5,
                                              int maxLineSearchSteps = 40,
                                              double residualTolerance = 1e-5,
                                              double stepTolerance = 1e-6,
                                              bool resetBeforeSolve = true,
                                              double relativeEnergyStop = 0.0) where TState : class
        {
            if (resetBeforeSolve)
                problem.Reset();

            memory = Math.Max(1, memory);
            maxLineSearchSteps = Math.Max(1, maxLineSearchSteps);

            if (!((backtrackFactor > 0.0) && (backtrackFactor < 1.0))) backtrackFactor = 0.5;

            problem.BeginSolve(false);

            problem.Mark(EDSolverTimer.Iteration);

            var sHistory = new List<Vector<double>>();
            var yHistory = new List<Vector<double>>();
            var rhoHistory = new List<double>();
            var alphas = new double[memory];

            void ForgetHistory()
            {
                sHistory.Clear();
                yHistory.Clear();
                rhoHistory.Clear();
            }

            // The step the last iteration took and the gradient it departed from: the pair this
            // iteration completes, once it has the gradient at the step's end.
            Vector<double> lastStep = null;
            Vector<double> lastGradient = null;

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

                problem.ClearStepReport();

                problem.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    problem.Log(EDSolverLogLevel.Error, "[ED] Residual became non-finite.");
                    problem.ReportEnd(EDSolveEnd.NonFiniteResidual, iter);
                    return;
                }

                // Close enough ends the solve - unless the inputs are still moving.
                if ((error < residualTolerance) && (!inputs.pending))
                {
                    end = EDSolveEnd.ResidualTolerance;
                    endIteration = iter;
                    break;
                }

                // Totals either side of a moved input belong to different objectives, and so does
                // the curvature the history holds.
                if (inputs.changed)
                {
                    previousError = double.NaN;
                    flatIterations = 0;

                    ForgetHistory();

                    lastStep = null;
                    lastGradient = null;
                }

                if ((!inputs.pending) && (EDSolver.ShouldStopOnDeltaEnergy(relativeEnergyStop, previousError, error, ref flatIterations)))
                {
                    end = EDSolveEnd.EnergyStop;
                    endIteration = iter;
                    break;
                }

                previousError = error;

                problem.BuildJacobian(out double jNorm);

                problem.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                // The gradient of the total, |f|^2.
                Vector<double> gradient = ((double.IsFinite(jNorm)) && (jNorm >= 1e-12)) ? (problem.HalfGradient(f) * 2.0) : (null);

                double gradientNorm = (gradient != null) ? (gradient.L2Norm()) : (0.0);

                if ((!double.IsFinite(gradientNorm)) || (!(gradientNorm > 0.0)))
                {
                    // Nothing to step along; with the inputs still moving the next iteration is
                    // another problem.
                    lastStep = null;
                    lastGradient = null;

                    if (inputs.pending) continue;

                    end = EDSolveEnd.NoDescentDirection;
                    endIteration = iter;
                    break;
                }

                // The pair the last step began, completed now that the gradient at its end is known;
                // kept only with positive curvature, which is what keeps the approximation positive
                // definite.
                if (lastStep != null)
                {
                    var y = gradient - lastGradient;

                    double curvature = lastStep.DotProduct(y);

                    if (curvature > 1e-12 * lastStep.L2Norm() * y.L2Norm())
                    {
                        if (sHistory.Count == memory)
                        {
                            sHistory.RemoveAt(0);
                            yHistory.RemoveAt(0);
                            rhoHistory.RemoveAt(0);
                        }

                        sHistory.Add(lastStep);
                        yHistory.Add(y);
                        rhoHistory.Add(1.0 / curvature);
                    }
                }

                lastStep = null;
                lastGradient = null;

                // The two-loop recursion: direction = -H gradient, H the approximate inverse curvature.
                var direction = -gradient;

                int count = sHistory.Count;

                for (int h = count - 1; h >= 0; h--)
                {
                    double a = rhoHistory[h] * sHistory[h].DotProduct(direction);

                    alphas[h] = a;

                    direction = direction - (yHistory[h] * a);
                }

                // The initial inverse curvature: the last pair's scale, or with no history a first
                // step of unit length.
                double scale = (count > 0) ? (sHistory[count - 1].DotProduct(yHistory[count - 1]) / Math.Max(1e-300, yHistory[count - 1].DotProduct(yHistory[count - 1])))
                                           : (1.0 / gradientNorm);

                direction = direction * scale;

                for (int h = 0; h < count; h++)
                {
                    double b = rhoHistory[h] * yHistory[h].DotProduct(direction);

                    direction = direction + (sHistory[h] * (alphas[h] - b));
                }

                double slope = gradient.DotProduct(direction);

                if (!(slope < 0.0))
                {
                    // Not a descent direction: start the history again, along the gradient.
                    ForgetHistory();

                    direction = gradient * (-1.0 / gradientNorm);

                    slope = gradient.DotProduct(direction);
                }

                double directionNorm = direction.L2Norm();
                double total = error * error;

                // What the trials are measured against: the state they depart from.
                problem.ReportAttempt(-1, EDAttemptOutcome.Entry, double.NaN, double.NaN, f, problem.current);

                double stepLength = 1.0;
                TState acceptedState = null;
                double acceptedNorm = double.NaN;

                for (int attempt = 0; attempt < maxLineSearchSteps; attempt++)
                {
                    double stepNorm = stepLength * directionNorm;

                    TState candidateState;

                    try
                    {
                        candidateState = problem.MakeCandidate(direction, stepLength);
                    }
                    catch
                    {
                        problem.ReportAttempt(attempt, EDAttemptOutcome.NoState, stepLength, stepNorm, null, null);

                        stepLength *= backtrackFactor;
                        continue;
                    }

                    var fCandidate = problem.EvaluateResidual(candidateState);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        problem.ReportAttempt(attempt, EDAttemptOutcome.NonFinite, stepLength, stepNorm, null, null);

                        stepLength *= backtrackFactor;
                        continue;
                    }

                    // Sufficient decrease: the total falls by at least the fraction asked of what
                    // the gradient predicts for this step.
                    if ((candidateError * candidateError) <= total + (sufficientDecrease * stepLength * slope))
                    {
                        problem.ReportStepTaken(attempt, stepLength, stepNorm);
                        problem.ReportAttempt(attempt, EDAttemptOutcome.Accepted, stepLength, stepNorm, fCandidate, candidateState);

                        acceptedState = candidateState;
                        acceptedNorm = stepNorm;
                        break;
                    }

                    // Refused on the sufficient-decrease test, whether its total rose or fell too little.
                    problem.ReportAttempt(attempt, EDAttemptOutcome.Worse, stepLength, stepNorm, fCandidate, candidateState);

                    stepLength *= backtrackFactor;
                }

                if (acceptedState == null)
                {
                    // Every trial was refused: the row that follows is a stall's, and the history,
                    // which proposed a direction nothing along decreased, goes.
                    problem.ReportStepRefused(maxLineSearchSteps);

                    ForgetHistory();

                    if (!inputs.pending)
                    {
                        problem.Log(EDSolverLogLevel.Warning, "[ED] LBFGS: the line search found no decrease.");

                        end = EDSolveEnd.NoImprovingStep;
                        endIteration = iter;
                        break;
                    }

                    problem.Log(EDSolverLogLevel.Warning, "[ED] LBFGS: the line search found no decrease; the inputs are still moving, so the solve goes on with them.");

                    continue;
                }

                problem.Accept(acceptedState);

                lastStep = direction * stepLength;
                lastGradient = gradient;

                // A step this small ends the solve - unless the inputs are still moving.
                if ((acceptedNorm < stepTolerance) && (!inputs.pending))
                {
                    end = EDSolveEnd.StepTolerance;
                    endIteration = iter;
                    break;
                }
            }

            problem.ReportEnd(end, endIteration);

            problem.Mark(EDSolverTimer.Iteration);
        }
    }
}
#endif
