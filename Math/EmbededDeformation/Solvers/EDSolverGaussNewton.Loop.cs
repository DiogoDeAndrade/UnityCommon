using System;
using MathNet.Numerics.LinearAlgebra;

#if UC_ENABLE_ED
namespace UC.ED
{
    // EmbededDeformation.SolveED_GN, moved here verbatim on 2026-10-22: the same statements in the
    // same order, each call into the deformation now the problem's (IEDSolverProblem says which
    // did what), plus the one report of why the solve ended. Goldens 2a, 2b and 2c run it.
    public partial class EDSolverGaussNewton
    {
        internal static void RunGaussNewton<TState>(IEDSolverProblem<TState> problem,
                                                    int maxIterations,
                                                    double damping = 1.0,
                                                    double residualTolerance = 1e-5,
                                                    double stepTolerance = 1e-6,
                                                    bool resetBeforeSolve = true,
                                                    double relativeEnergyStop = 0.0) where TState : class
        {
            if (resetBeforeSolve)
                problem.Reset();

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

                // Already solved / close enough - unless the inputs are still moving, where
                // "solved" describes a request that is about to change.
                if (!double.IsFinite(error) || ((error < residualTolerance) && (!inputs.pending)))
                {
                    end = (double.IsFinite(error)) ? (EDSolveEnd.ResidualTolerance) : (EDSolveEnd.NonFiniteResidual);
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

                if (!double.IsFinite(jNorm) || jNorm < 1e-12)
                {
                    // Nothing to step along. With the inputs still moving the next iteration is
                    // another problem, so the solve goes on to it rather than ending here.
                    if (inputs.pending) continue;

                    end = EDSolveEnd.NoDescentDirection;
                    endIteration = iter;
                    break;
                }

                Vector<double> delta;

                try
                {
                    problem.Mark(EDSolverTimer.Solve);

                    delta = problem.SolveLinearizedLeastSquares(f);

                    problem.Mark(EDSolverTimer.Solve);
                }
                catch (Exception ex)
                {
                    problem.Log(EDSolverLogLevel.Error, $"SolveED failed: {ex.Message}");
                    problem.ReportEnd(EDSolveEnd.LinearSolveFailed, iter);
                    return;
                }

                double stepNorm = delta.L2Norm();

                if (!double.IsFinite(stepNorm))
                {
                    problem.Log(EDSolverLogLevel.Error, "[ED] SolveED produced non-finite delta.");
                    problem.ReportEnd(EDSolveEnd.LinearSolveFailed, iter);
                    return;
                }

                // A step this small ends the solve - unless the inputs are still moving, where it
                // is taken and the solve goes on.
                if ((stepNorm < stepTolerance) && (!inputs.pending))
                {
                    end = EDSolveEnd.StepTolerance;
                    endIteration = iter;
                    break;
                }

                problem.ReportStepTaken(0, 0.0, stepNorm * Math.Abs(damping));

                problem.ApplyStep(delta, damping);
            }

            problem.ReportEnd(end, endIteration);

            problem.Mark(EDSolverTimer.Iteration);
        }
    }
}
#endif
