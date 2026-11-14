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

            // The RemainingDisplacement stop's geometry (2026-11-12): the rest vertices - the
            // topology the graph was sampled from, which is the mesh itself on a benchmark and
            // the navigation mesh on a level piece - carried the way the output is, through the
            // field when it carries the geometry and through the bindings otherwise
            // (DeformVertices), at the state last given. One deformation of those vertices a
            // call, on one thread; only a solve under that stop makes any.
            //
            // The states measured, newest last. One is kept - the last, for the next step - until
            // the RecentDisplacement stop asks how far the geometry is from where it was some
            // steps back (GeometryTravel), and from then on that many more.
            private readonly System.Collections.Generic.List<Vector3[]> measuredGeometry = new System.Collections.Generic.List<Vector3[]>();
            private int geometryKept = 1;

            // The largest distance between two measurements, over the rest size; zero where there
            // is nothing to compare with, infinite where a vertex is not finite - no measurement,
            // and the stop forgets its steps on it.
            private double LargestDistance(Vector3[] moved, Vector3[] before)
            {
                double largest = 0.0;
                bool finite = true;

                if ((before != null) && (before.Length == moved.Length))
                {
                    for (int i = 0; i < moved.Length; i++)
                    {
                        double dx = (double)moved[i].x - before[i].x;
                        double dy = (double)moved[i].y - before[i].y;
                        double dz = (double)moved[i].z - before[i].z;
                        double squared = (dx * dx) + (dy * dy) + (dz * dz);

                        if (!double.IsFinite(squared)) finite = false;
                        else if (squared > largest) largest = squared;
                    }
                }

                return (finite) ? (System.Math.Sqrt(largest) / deformation.restSize) : (double.PositiveInfinity);
            }

            public double GeometryStep(EDState state)
            {
                if ((state == null) || (deformation.restVertices == null) || (deformation.restVertices.Length == 0)) return double.NaN;

                Vector3[] moved = deformation.DeformVertices(new EDStateView(state));

                double step = LargestDistance(moved, (measuredGeometry.Count > 0) ? (measuredGeometry[measuredGeometry.Count - 1]) : (null));

                measuredGeometry.Add(moved);

                while (measuredGeometry.Count > geometryKept) measuredGeometry.RemoveAt(0);

                return step;
            }

            public double GeometryTravel(int steps)
            {
                if (steps < 1) return double.NaN;

                if (geometryKept < steps + 1) geometryKept = steps + 1;

                if (measuredGeometry.Count <= steps) return double.NaN;

                return LargestDistance(measuredGeometry[measuredGeometry.Count - 1], measuredGeometry[measuredGeometry.Count - 1 - steps]);
            }

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
