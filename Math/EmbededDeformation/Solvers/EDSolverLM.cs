using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Levenberg-Marquardt on the normal equations: form JtJ and Jtf, add lambda to the diagonal,
    /// and only accept a step that improves the residual, raising lambda and retrying when it does
    /// not. The damped system is solved by a general solve, or by Cholesky when the option is on.
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
        // Off, the default, is what every solve did before the option existed: the damping an
        // iteration ends with, lowered or raised, is where the next one begins. A solve of 50
        // iterations and 50 presses then part ways, because a press is a solve of one iteration
        // and begins at the initial damping every time. On 2026-09-29 (queue section 31) that
        // difference was the whole of what separated a total of 1.64 from one of 0.0057: the
        // damping is added to every parameter alike, so at a damping in the hundreds the terminals
        // moved and the chain they pull did not, and the solve never tried the undamped step a
        // press from the same state took at its first attempt.
        [SerializeField, Tooltip("Begin every iteration's attempts at the initial damping, as a solve of one iteration does, so a solve of N iterations takes the steps N presses of Run Iteration take. Off, the damping is carried from one iteration to the next.")]
        protected bool restartDampingEachIteration = false;
        // Off, the default, forms the normal equations as every solve did: a transpose and two
        // dense Math.NET products, rows times the square of the columns whatever the Jacobian
        // holds - half of Corridor6's solve on 2026-10-15. On, EDNormalEquations.Sparse sums the
        // same products over each row's nonzero entries only, in row order. The set of rounded
        // additions is the dense product's; their order may not be, so H can differ in its last
        // bits and a damped step can be accepted where it was refused - which is why this is an
        // option that the dump names, and not a quiet replacement. Tools/NormalEquationsCheck
        // measures the bits and the cost on the pieces' own sparsity pattern.
        [SerializeField, Tooltip("Form the normal equations over each Jacobian row's nonzero entries only, instead of the dense transpose-and-multiply. The same products, summed in row order; hundreds of times cheaper on a large piece. Named in the dump when on.")]
        protected bool useSparseNormalEquations = false;
        // On the navigation solver until 2026-10-20, moved here so a geometry-sampled graph, which
        // the navigation solver is refused on, can have it too. Same field name, so the navigation
        // solver assets keep their value; off, the default, is the general solve every plain LM
        // asset has always made.
        [SerializeField, Tooltip("Factorise the damped normal equations by Cholesky instead of a general solve. Faster, but it fails on an indefinite matrix, and then raises the damping and retries.")]
        protected bool cholesky = false;
        [SerializeField]
        protected float residualTolerance = 1e-5f;
        [SerializeField]
        protected float stepTolerance = 1e-6f;

        public override string modeLabel => "FullED_LM";
        public override float lambda => initialLambda;
        public override bool adaptiveLambda => adaptive;
        public override bool restartsDamping => restartDampingEachIteration;
        public override bool sparseNormalEquations => useSparseNormalEquations;
        public override bool choleskyFactorization => cholesky;

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
