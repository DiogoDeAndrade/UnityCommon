using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// A way of driving the deformation towards its constraints, and the parameters that way needs.
    ///
    /// The asset is the definition: shared, and never written to while solving. Each owner asks for
    /// an <see cref="Instance"/>, which holds everything mutable a run needs, so two components
    /// referencing the same solver asset cannot interfere with one another. This mirrors the
    /// Ability/Ability.Instance split used elsewhere in these projects.
    /// </summary>
    public abstract class EDSolver : ScriptableObject
    {
        [SerializeField, Min(1)]
        protected int maxIterations = 10;

        public int defaultIterationCount => maxIterations;

        /// <summary>
        /// Short name for this solver in diagnostic dumps.
        /// </summary>
        public abstract string modeLabel { get; }

        // Reported in the diagnostic dump so a golden file records the solver settings it was
        // produced with, whichever solver that was. Subclasses override the ones they actually
        // have; the defaults keep the dump's shape stable across solver types.
        public virtual float damping => 1.0f;
        public virtual float lambda => 1e-3f;
        public virtual bool adaptiveLambda => true;
        public virtual bool choleskyFactorization => false;
        public virtual float smoothnessWeight => 0.1f;

        public abstract Instance NewInstance(EmbededDeformation deformation);

        public abstract class Instance
        {
            public EDSolver solver { get; private set; }
            public EmbededDeformation deformation { get; private set; }

            protected Instance(EDSolver solver, EmbededDeformation deformation)
            {
                this.solver = solver;
                this.deformation = deformation;
            }

            /// <summary>
            /// Runs the solve. Iteration count and reset are passed per call rather than read from
            /// the asset, because single-stepping and a full solve share the same solver.
            /// </summary>
            public abstract void Solve(WeightConfig weights, int iterations, bool resetBeforeSolve);
        }
    }
}
#endif
