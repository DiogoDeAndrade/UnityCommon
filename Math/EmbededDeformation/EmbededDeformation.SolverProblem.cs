using UnityEngine;
using MathNet.Numerics.LinearAlgebra;

#if UC_ENABLE_ED
namespace UC.ED
{
    public partial class EmbededDeformation
    {
        /// <summary>
        /// Why the last term solve ended, and in which iteration - the solver's report of it.
        /// Not serialized: a record of a run, never an input to one.
        /// </summary>
        public EDSolveEnd lastSolveEnd { get; private set; }
        public int lastSolveEndIteration { get; private set; }

        /// <summary>
        /// This deformation and an energy as the problem a solver works on (2026-10-22, when the
        /// loops moved into the solver objects). Each member is the call the loops made on the
        /// deformation before, so a solver does here what SolveED_GN, SolveED_LM and SolveED_Nav did.
        /// </summary>
        public IEDSolverProblem<EDState> CreateSolverProblem(EDEnergyModel.Instance energy) => new SolverProblem(this, energy);

        private sealed class SolverProblem : IEDSolverProblem<EDState>
        {
            private readonly EmbededDeformation deformation;
            private readonly EDEnergyModel.Instance energy;

            private Matrix<double> jacobian;
            private bool readsClearance;

            public SolverProblem(EmbededDeformation deformation, EDEnergyModel.Instance energy)
            {
                this.deformation = deformation;
                this.energy = energy;
            }

            public EDState current => deformation.currentState;

            public void Reset() => deformation.ResetDeformation();

            public void BeginSolve(bool refreshesCandidates)
            {
                InitMathNet();

                if (deformation.currentState == null)
                    deformation.currentState = new EDState(deformation.nodes.Count);

                energy.Resolve();

                // The navigation solver's: the clearances, measured only for a model that reads them,
                // and the entering state's ensured before the first iteration.
                if (refreshesCandidates)
                {
                    readsClearance = energy.readsClearanceCache;

                    if (readsClearance) deformation.EnsureClearance(deformation.currentState);
                }

                deformation.TraceResidualLayout(energy);
            }

            public void CountIteration() => deformation.CountSolveIteration();

            public Vector<double> EnterIteration(bool reportEntry, out EDIterationInputs inputs, out bool entryReported)
                => deformation.EnterIteration(energy, new EDStateView(deformation.currentState), reportEntry, out inputs, out entryReported);

            public void ReportIteration(Vector<double> residual) => deformation.ReportIteration(residual, energy, new EDStateView(deformation.currentState));

            public void LogResidualEnergies(Vector<double> residual, int iteration) => deformation.LogResidualEnergies(residual, energy, iteration);

            public void ClearStepReport() => deformation.ClearStepReport();

            public void BuildJacobian(out double jacobianNorm) => jacobian = energy.BuildJacobian(deformation.currentState, out jacobianNorm);

            public void FormNormalEquations(Vector<double> residual, bool sparse, out Matrix<double> H, out Vector<double> g)
                => EDNormalEquations.Build(jacobian, residual, sparse, out H, out g);

            public Vector<double> SolveLinearizedLeastSquares(Vector<double> residual)
            {
                var qr = jacobian.QR();

                return qr.Solve(-residual);
            }

            public Vector<double> HalfGradient(Vector<double> residual) => jacobian.TransposeThisAndMultiply(residual);

            public EDState MakeCandidate(Vector<double> step, double scale) => deformation.currentState.CloneAndApply(step, scale);

            public void RefreshCandidate(EDState candidate)
            {
                if (readsClearance) deformation.ComputeClearance(candidate);
            }

            public Vector<double> EvaluateResidual(EDState state) => energy.EvaluateResidual(new EDStateView(state));

            public void Accept(EDState candidate) => deformation.currentState = candidate;

            public void ApplyStep(Vector<double> step, double scale) => deformation.currentState.Apply(step, scale);

            public void ReportAttempt(int attempt, EDAttemptOutcome outcome, double damping, double stepNorm, Vector<double> residual, EDState state)
                => deformation.ReportAttempt(attempt, outcome, damping, stepNorm, residual, energy, (state != null) ? (new EDStateView(state)) : ((EDStateView?)null));

            public void ReportStepTaken(int attempt, double damping, double stepNorm) => deformation.ReportStepTaken(attempt, damping, stepNorm);

            public void ReportStepRefused(int attempts) => deformation.ReportStepRefused(attempts);

            public void ReportEnd(EDSolveEnd end, int iteration)
            {
                deformation.lastSolveEnd = end;
                deformation.lastSolveEndIteration = iteration;
            }

            public void Trace(string line) => EDDiagnostics.Trace(line);

            public void Log(EDSolverLogLevel level, string message)
            {
                switch (level)
                {
                    case EDSolverLogLevel.Error: Debug.LogError(message); break;
                    case EDSolverLogLevel.Warning: Debug.LogWarning(message); break;
                    default: Debug.Log(message); break;
                }
            }

            public void Mark(EDSolverTimer timer)
            {
                switch (timer)
                {
                    case EDSolverTimer.Iteration: DebugProfiler.DebugMark(deformation.timeIteration); break;
                    case EDSolverTimer.NormalEquations: DebugProfiler.DebugMark(deformation.timeNormalEquations); break;
                    case EDSolverTimer.Solve: DebugProfiler.DebugMark(deformation.timeSolve); break;
                }
            }
        }
    }
}
#endif
