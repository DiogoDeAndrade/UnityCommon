using NaughtyAttributes;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// When a multi-iteration solve stops: at the iteration budget, or as soon as the total energy
    /// has stopped improving. DeltaEnergy is still bounded by the budget - it can only stop
    /// earlier, never run longer. RemainingDisplacement (2026-11-12) stops on the geometry and
    /// not on the energy: once the distance the problem's geometry still has to travel, estimated
    /// from the steps the solve has taken (<see cref="EDDisplacementStop"/>), is under a fraction
    /// of the geometry's size - bounded by the budget in the same way. Stored by index, so new
    /// values go at the end.
    /// </summary>
    public enum EDStopCriteria
    {
        MaxIterations,
        DeltaEnergy,
        RemainingDisplacement,
        // 2026-11-14: stops once no point of the geometry is further than a tolerance from where
        // it was a window of iterations before. No estimate of what is left - the rule for a
        // solve whose steps do not shrink at a rate, a damped one in its tail (EDDisplacementStop).
        RecentDisplacement,
    }

    /// <summary>
    /// A way of driving the deformation towards its constraints, and the parameters that way needs.
    ///
    /// The asset is the definition: shared, and never written to while solving. Each owner asks for
    /// an <see cref="Instance"/>, which holds everything mutable a run needs, so two components
    /// referencing the same solver asset cannot interfere with one another. This mirrors the
    /// Ability/Ability.Instance split used elsewhere in these projects.
    ///
    /// **The term solvers own their loops** (2026-10-22, Diogo's call): Gauss-Newton,
    /// Levenberg-Marquardt, the navigation solver and LBFGS each run on an
    /// <see cref="IEDSolverProblem{TState}"/>, in a static method of their own class in a
    /// `.Loop.cs` partial, with every parameter passed and every local state in locals - so the
    /// asset stays a definition, and any of them runs on any problem: an EmbededDeformation, or the
    /// meshless method. The three that were EmbededDeformation's SolveED_GN, SolveED_LM and
    /// SolveED_Nav were moved verbatim, the calls into the deformation becoming the problem's. The
    /// translation-only solver is not one of them: a linear solve over the point constraints, it
    /// has no residual to minimise.
    /// </summary>
    public abstract partial class EDSolver : ScriptableObject
    {
        [SerializeField, Min(1)]
        protected int maxIterations = 10;

        [SerializeField, Tooltip("MaxIterations runs the full budget. DeltaEnergy stops early once the relative energy improvement stays below the threshold for two consecutive iterations - two rather than one because a single near-flat iteration mid-descent exists in the data and a repeated one never resumes (2026-08-27 sweep). RemainingDisplacement stops early once the distance the geometry still has to travel, estimated from the steps taken so far, stays below the displacement tolerance on consecutive iterations (two where the steps are at least halving, four where they shrink more slowly) - a stop in the geometry's own units, where an energy threshold bounds what one iteration gains and not what is left. Term solvers only; the translation-only solver has no energy to watch.")]
        protected EDStopCriteria stopCriteria = EDStopCriteria.MaxIterations;

        [SerializeField, Range(0.0f, 1.0f), ShowIf(nameof(isStopDeltaEnergy)), Tooltip("The relative energy improvement per iteration below which DeltaEnergy stops, as a fraction - 0.01 is 1%. Measured on the total energy (the squared residual norm), the same quantity the iteration export's total column carries.")]
        protected float deltaEnergyThreshold = 0.01f;

        // The geometry is the problem's own: on a deformation, the vertices the graph was sampled
        // from (the benchmark's mesh, a piece's navigation mesh), carried as the output is. A step
        // is the largest distance any of them moves in an iteration, over the largest side of
        // their rest bounding box; the estimate multiplies it by rho / (1 - rho), rho the ratio of
        // successive steps, never by less than one - so where the solve converges fast the rule
        // is "no vertex moved more than this". It costs one deformation of those vertices an
        // iteration, and nothing when the criteria is another.
        [SerializeField, Range(0.0f, 0.1f), ShowIf(nameof(isStopDisplacement)),Tooltip("RemainingDisplacement: the solve stops once the distance any vertex of the geometry still has to travel, estimated from the solve's own steps, is under this fraction of the geometry's largest side on consecutive iterations (two where the steps are at least halving, four otherwise) - 0.001 is a tenth of a percent. The estimate is an iteration's largest vertex move times rho / (1 - rho), rho the rate the moves are shrinking at, and never less than the move itself. Costs one deformation of the geometry's vertices per iteration.")]
        protected float displacementTolerance = 0.001f;

        // The second displacement rule (2026-11-14), with a tolerance of its own: the number means
        // another thing - how far the geometry moved over the last iterations, not how far it has
        // left to go - so it is not the field above under another criteria.
        [SerializeField, Range(0.0f, 0.1f), ShowIf(nameof(isStopRecentDisplacement)), Tooltip("RecentDisplacement: the solve stops once no vertex of the geometry is further than this fraction of the geometry's largest side from where it was Recent Displacement Window iterations before, on two consecutive iterations - 0.005 is half a percent. It bounds what the last iterations moved, not what is left to move: the rule for a damped solve, whose steps follow the damping up and down and give no rate to estimate the rest from. Costs one deformation of the geometry's vertices per iteration.")]
        protected float recentDisplacementTolerance = 0.005f;

        [SerializeField, Min(1), ShowIf(nameof(isStopRecentDisplacement)), Tooltip("RecentDisplacement: how many iterations back the geometry is compared with. Steps that go back and forth inside the window cancel; the rule cannot fire before this many iterations.")]
        protected int recentDisplacementWindow = 10;

        public int defaultIterationCount => maxIterations;

        /// <summary>
        /// The relative-improvement threshold a solve should stop at, or zero when the criteria is
        /// not DeltaEnergy - one number, so the solve loops need no knowledge of the enum.
        /// </summary>
        public double relativeEnergyStop => (stopCriteria == EDStopCriteria.DeltaEnergy) ? (deltaEnergyThreshold) : (0.0);

        /// <summary>
        /// The remaining-displacement tolerance a solve should stop at, as a fraction of the
        /// geometry's size, or zero when the criteria is not RemainingDisplacement - zero is the
        /// path every solve took before the criteria existed, on which the geometry is never measured.
        /// </summary>
        public double remainingDisplacementStop => (stopCriteria == EDStopCriteria.RemainingDisplacement) ? (displacementTolerance) : (0.0);

        /// <summary>
        /// The recent-displacement tolerance a solve should stop at, as a fraction of the
        /// geometry's size, or zero when the criteria is not RecentDisplacement; and the
        /// iterations it looks back over.
        /// </summary>
        public double recentDisplacementStop => (stopCriteria == EDStopCriteria.RecentDisplacement) ? (recentDisplacementTolerance) : (0.0);
        public int recentDisplacementSteps => Mathf.Max(1, recentDisplacementWindow);

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
        public virtual bool restartsDamping => false;
        public virtual bool sparseNormalEquations => false;
        public virtual EDDampingScaling dampingScalingMode => EDDampingScaling.Identity;
        public virtual float dampingScaleRelativeFloor => 0.0f;
        public virtual float smoothnessWeight => 0.1f;

        public abstract Instance NewInstance(EmbededDeformation deformation);

        /// <summary>Whether this solver runs on any <see cref="IEDSolverProblem{TState}"/>, not only on an EmbededDeformation.</summary>
        public virtual bool solvesProblems => false;

        bool isStopDeltaEnergy => stopCriteria == EDStopCriteria.DeltaEnergy;
        bool isStopDisplacement => stopCriteria == EDStopCriteria.RemainingDisplacement;
        bool isStopRecentDisplacement => stopCriteria == EDStopCriteria.RecentDisplacement;

        /// <summary>
        /// Runs this solver on a problem, with this asset's parameters. Iteration count and reset are
        /// passed per call, as <see cref="Instance.Solve"/> takes them.
        /// </summary>
        public virtual void Solve<TState>(IEDSolverProblem<TState> problem, int iterations, bool resetBeforeSolve) where TState : class
        {
            throw new System.NotSupportedException($"{GetType().Name} solves an EmbededDeformation only, and runs on no other problem.");
        }

        /// <summary>
        /// The instance of every solver that runs on problems: the deformation and the energy made
        /// into one, handed to <see cref="Solve{TState}"/>.
        /// </summary>
        public class ProblemInstance : Instance
        {
            public ProblemInstance(EDSolver solver, EmbededDeformation deformation)
                : base(solver, deformation)
            {
            }

            public override void Solve(EDEnergyModel.Instance energy, int iterations, bool resetBeforeSolve)
            {
                solver.Solve(deformation.CreateSolverProblem(energy), iterations, resetBeforeSolve);
            }
        }

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
