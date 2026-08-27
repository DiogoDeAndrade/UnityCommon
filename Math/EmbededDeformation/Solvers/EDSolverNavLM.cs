using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The navigation-aware Levenberg-Marquardt solver, and the one the nav-aware deformation
    /// actually uses.
    ///
    /// Differs from the plain LM in exactly five ways, all of which matter once navigation energies
    /// are involved: it initialises the Math.NET provider, recomputes the clearance cache for the
    /// current state and for every candidate step so the clearance term is never evaluated against
    /// stale data, optionally factorises with Cholesky instead of a general solve, logs the
    /// per-energy residual breakdown each iteration, and reports the profiler timings.
    ///
    /// The clearance refresh is the important one: pairing a clearance-weighted configuration with
    /// the plain LM silently measures clearance against the previous state.
    /// </summary>
    [CreateAssetMenu(fileName = "EDSolverNavLM", menuName = "Unity Common/ED/Solver/Levenberg-Marquardt (Nav-Aware)")]
    public class EDSolverNavLM : EDSolverLM
    {
        [SerializeField, Tooltip("Faster, but fails on an indefinite Hessian and falls back to raising lambda.")]
        private bool cholesky = false;

        public override string modeLabel => "NavED";
        public override bool choleskyFactorization => cholesky;

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
                                        def.relativeEnergyStop);
            }
        }
    }
}
#endif
