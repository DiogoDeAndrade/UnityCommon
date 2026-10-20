using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The navigation-aware Levenberg-Marquardt solver, and the one the nav-aware deformation
    /// actually uses.
    ///
    /// The same loop as the plain LM - the damping rules, the restart, the sparse normal equations,
    /// the Cholesky option, the energy stop and the per-iteration hook are shared - and it differs
    /// in what it records and in one measurement. It measures the clearances of every candidate
    /// step, and of the state it enters with, when the model holds a term that reads them
    /// (2026-10-08; with no such term it measures none). It logs the per-energy breakdown at every
    /// iteration and at the end. And it writes the export's rows differently: a continuing solve
    /// does not report the state it entered with, which the previous solve's last row already
    /// holds, and every solve reports its final state.
    ///
    /// The clearance refresh is the important one: pairing a clearance-weighted configuration with
    /// the plain LM silently measures clearance against the previous state. The owner also reads
    /// the solver's type: a GenerateED with this solver builds the navigation data.
    /// </summary>
    [CreateAssetMenu(fileName = "EDSolverNavLM", menuName = "Unity Common/ED/Solver/Levenberg-Marquardt (Nav-Aware)")]
    public class EDSolverNavLM : EDSolverLM
    {
        // The Cholesky option is the plain LM's since 2026-10-20, under the same field name, so the
        // assets of this class kept their value.

        public override string modeLabel => "NavED";

        public override Instance NewInstance(EmbededDeformation deformation) => new NavLMInstance(this, deformation);

        public class NavLMInstance : LMInstance
        {
            public NavLMInstance(EDSolverNavLM solver, EmbededDeformation deformation)
                : base(solver, deformation)
            {
            }

            public override void Solve(EDEnergyModel.Instance energy, int iterations, bool resetBeforeSolve)
            {
                var def = (EDSolverNavLM)solver;

                deformation.SolveED_Nav(iterations,
                                        energy,
                                        def.initialLambda,
                                        def.residualTolerance,
                                        def.stepTolerance,
                                        resetBeforeSolve,
                                        def.adaptive,
                                        def.cholesky,
                                        def.relativeEnergyStop,
                                        def.restartDampingEachIteration,
                                        def.useSparseNormalEquations);
            }
        }
    }
}
#endif
