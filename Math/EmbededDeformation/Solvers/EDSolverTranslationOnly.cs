using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Direct linear least squares for per-node translations, with rotations held at identity.
    ///
    /// Not an iterative solver and not a special case of the others: it solves x, y and z
    /// independently and uses only the point constraints and a smoothness term, never touching the
    /// residual and Jacobian machinery. Kept because it is the natural translation-only baseline to
    /// compare the embedded deformation solvers against.
    /// </summary>
    [CreateAssetMenu(fileName = "EDSolverTranslationOnly", menuName = "Unity Common/ED/Solver/Translation Only")]
    public class EDSolverTranslationOnly : EDSolver
    {
        [SerializeField, Range(0.01f, 1.0f), Tooltip("Weight of the term pulling neighbouring node translations together.")]
        private float smoothness = 0.1f;

        public override string modeLabel => "TranslationOnly";
        public override float smoothnessWeight => smoothness;

        public override Instance NewInstance(EmbededDeformation deformation) => new TranslationOnlyInstance(this, deformation);

        public class TranslationOnlyInstance : Instance
        {
            public TranslationOnlyInstance(EDSolverTranslationOnly solver, EmbededDeformation deformation)
                : base(solver, deformation)
            {
            }

            public override void Solve(WeightConfig weights, int iterations, bool resetBeforeSolve)
            {
                // The smoothness weight belongs to this solver, so it is applied here rather than
                // being carried in from the caller's weight configuration.
                weights.smoothnessWeight = ((EDSolverTranslationOnly)solver).smoothness;

                deformation.SolveTranslationsOnly(weights, resetBeforeSolve);
            }
        }
    }
}
#endif
