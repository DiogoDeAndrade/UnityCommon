using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Levenberg-Marquardt on the dense normal equations: form JtJ and Jtf, add lambda to the
    /// diagonal, and only accept a step that improves the residual, raising lambda and retrying
    /// when it does not.
    ///
    /// This is the plain variant. It does not refresh the clearance cache between steps, so a
    /// configuration with a clearance term should use the nav-aware subclass instead - see
    /// <see cref="EDSolverNavLM"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "EDSolverLM", menuName = "Unity Common/ED/Solver/Levenberg-Marquardt")]
    public class EDSolverLM : EDSolver
    {
        [SerializeField, Min(0.0f), Tooltip("Initial damping added to the diagonal of the normal equations.")]
        protected float initialLambda = 1e-3f;
        [SerializeField, Tooltip("Lower lambda after an accepted step and raise it after a rejected one.")]
        protected bool adaptive = true;
        [SerializeField]
        protected float residualTolerance = 1e-5f;
        [SerializeField]
        protected float stepTolerance = 1e-6f;

        public override string modeLabel => "FullED_LM";
        public override float lambda => initialLambda;
        public override bool adaptiveLambda => adaptive;

        public override Instance NewInstance(EmbededDeformation deformation) => new LMInstance(this, deformation);

        public class LMInstance : Instance
        {
            public LMInstance(EDSolverLM solver, EmbededDeformation deformation)
                : base(solver, deformation)
            {
            }

            public override void Solve(EDEnergyModel.Instance energy, int iterations, bool resetBeforeSolve)
            {
                var def = (EDSolverLM)solver;

                deformation.SolveED_LM(iterations,
                                       energy,
                                       def.initialLambda,
                                       def.residualTolerance,
                                       def.stepTolerance,
                                       resetBeforeSolve,
                                       def.adaptive);
            }
        }
    }
}
#endif
