using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Plain Gauss-Newton: solve J d = -f by QR each iteration and take the step with fixed damping.
    ///
    /// There is no acceptance test, so a step that makes the residual worse is taken anyway, and
    /// the clearance cache is not refreshed between iterations. Superseded in practice by the
    /// Levenberg-Marquardt solvers, and kept as a comparison baseline rather than for production
    /// deformation.
    /// </summary>
    [CreateAssetMenu(fileName = "EDSolverGaussNewton", menuName = "Unity Common/ED/Solver/Gauss-Newton")]
    public class EDSolverGaussNewton : EDSolver
    {
        [SerializeField, Tooltip("Fraction of the computed step that is applied each iteration.")]
        private float stepDamping = 1.0f;
        [SerializeField]
        private float residualTolerance = 1e-5f;
        [SerializeField]
        private float stepTolerance = 1e-6f;

        public override string modeLabel => "FullED_GN";
        public override float damping => stepDamping;

        public override Instance NewInstance(EmbededDeformation deformation) => new GaussNewtonInstance(this, deformation);

        public class GaussNewtonInstance : Instance
        {
            public GaussNewtonInstance(EDSolverGaussNewton solver, EmbededDeformation deformation)
                : base(solver, deformation)
            {
            }

            public override void Solve(EDEnergyModel.Instance energy, int iterations, bool resetBeforeSolve)
            {
                var def = (EDSolverGaussNewton)solver;

                deformation.SolveED_GN(iterations,
                                       energy,
                                       def.stepDamping,
                                       def.residualTolerance,
                                       def.stepTolerance,
                                       resetBeforeSolve,
                                       def.relativeEnergyStop);
            }
        }
    }
}
#endif
