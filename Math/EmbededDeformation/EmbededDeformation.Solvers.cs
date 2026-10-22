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
    // The term solvers' loops were here until 2026-10-22 - SolveED_GN, SolveED_LM, SolveED_Nav and
    // their two helpers - and are now the solver objects' own (EDSolverGaussNewton.Loop.cs,
    // EDSolverLM.Loop.cs, EDSolverNavLM.Loop.cs, EDSolver.Loop.cs), running on this deformation
    // through CreateSolverProblem (EmbededDeformation.SolverProblem.cs). What stays is what the
    // problem calls.
    public partial class EmbededDeformation
    {
        #region Solver

#if MATH_NET_AVAILABLE
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

        // The configuration itself lives on EDDiagnostics, because verification mode is what decides
        // it and because these providers are process-global: whoever sets them last wins for the
        // rest of the session. Kept as a named call at the top of each solver so it is visible that
        // a solve configures its own environment rather than trusting what it finds.
        static void InitMathNet() => EDDiagnostics.ApplyMathNetProviders();

        #endregion
    }
}
#endif
