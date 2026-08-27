using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// When a multi-iteration solve stops: at the iteration budget, or as soon as the total energy
    /// has stopped improving. DeltaEnergy is still bounded by the budget - it can only stop
    /// earlier, never run longer.
    /// </summary>
    public enum EDStopCriteria
    {
        MaxIterations,
        DeltaEnergy,
    }

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

        [SerializeField, Tooltip("MaxIterations runs the full budget. DeltaEnergy stops early once the relative energy improvement stays below the threshold for two consecutive iterations - two rather than one because a single near-flat iteration mid-descent exists in the data and a repeated one never resumes (2026-08-27 sweep). Term solvers only; the translation-only solver has no energy to watch.")]
        protected EDStopCriteria stopCriteria = EDStopCriteria.MaxIterations;

        [SerializeField, Range(0.0f, 1.0f), Tooltip("The relative energy improvement per iteration below which DeltaEnergy stops, as a fraction - 0.01 is 1%. Measured on the total energy (the squared residual norm), the same quantity the iteration export's total column carries.")]
        protected float deltaEnergyThreshold = 0.01f;

        public int defaultIterationCount => maxIterations;

        /// <summary>
        /// The relative-improvement threshold a solve should stop at, or zero when the criteria is
        /// MaxIterations - one number, so the solve loops need no knowledge of the enum.
        /// </summary>
        public double relativeEnergyStop => (stopCriteria == EDStopCriteria.DeltaEnergy) ? (deltaEnergyThreshold) : (0.0);

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
            /// Runs the solve against an energy model. Iteration count and reset are passed per call
            /// rather than read from the asset, because single-stepping and a full solve share the
            /// same solver.
            ///
            /// The energy says what is being minimised and the solver says how, which is the whole
            /// point of keeping them in separate assets - any solver can be pointed at any energy.
            /// </summary>
            public abstract void Solve(EDEnergyModel.Instance energy, int iterations, bool resetBeforeSolve);
        }
    }
}
#endif
