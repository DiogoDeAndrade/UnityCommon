using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Limited-memory BFGS: a quasi-Newton method that builds an approximation of the inverse of
    /// the energy's curvature from the last few steps and the change in gradient across each,
    /// never forming or factoring a matrix, and takes each step along the direction it gives with a
    /// backtracking line search on the sufficient-decrease (Armijo) condition.
    ///
    /// Built 2026-10-22, Diogo's ask, as a solver of its own that runs on any problem: the meshless
    /// method's paper (Adams et al. 2008) minimised with the LBFGS of OPT++, and as an option it
    /// runs on an EmbededDeformation too. It reads the energy and its gradient only, 2 Jᵀf, so it
    /// needs no least-squares structure - and so it does not use the structure Levenberg-Marquardt
    /// uses: on terms weighted far apart it learns the curvature slowly from successive gradients,
    /// where LM has it from JᵀJ at once (the meshless harness's rigid request: 255 iterations
    /// against LM's 7). It is the slow route on these problems, and here for fidelity and as an
    /// ablation of the optimiser, not as a contender.
    ///
    /// The loop keeps the damped solvers' frame: the residual tolerance and the energy stop at an
    /// iteration's entry, the request's inputs moved there, no stop while they are still moving,
    /// the step tolerance on an accepted step. The history is dropped when the inputs move (its
    /// curvature was of another objective) and when a line search fails. The records read as the
    /// damped solvers' do, with one change of meaning: an attempt is a line-search trial, and the
    /// damping column carries its step length, the fraction of the direction taken.
    /// </summary>
    [CreateAssetMenu(fileName = "EDSolverLBFGS", menuName = "Unity Common/ED/Solver/LBFGS")]
    public partial class EDSolverLBFGS : EDSolver
    {
        [SerializeField, Min(1), Tooltip("How many of the last steps (and the gradient changes across them) the curvature is approximated from. 3 to 20 is usual; more remembers further back at the cost of a vector pair each.")]
        private int memory = 10;
        [SerializeField, Min(0.0f), Tooltip("The sufficient-decrease constant of the line search: a step is taken when the total falls by at least this fraction of what the gradient predicts for it. Small (1e-4 is the textbook value) accepts any real decrease.")]
        private float sufficientDecrease = 1e-4f;
        [SerializeField, Range(0.1f, 0.9f), Tooltip("What a refused trial's step length is multiplied by before the next trial.")]
        private float backtrackFactor = 0.5f;
        [SerializeField, Min(1), Tooltip("The trials of one line search before the iteration counts as finding no decrease.")]
        private int maxLineSearchSteps = 40;
        [SerializeField]
        private float residualTolerance = 1e-5f;
        [SerializeField]
        private float stepTolerance = 1e-6f;

        public override string modeLabel => "LBFGS";

        public override Instance NewInstance(EmbededDeformation deformation) => new ProblemInstance(this, deformation);

        public override bool solvesProblems => true;

        // The loop is RunLBFGS, in EDSolverLBFGS.Loop.cs.
        public override void Solve<TState>(IEDSolverProblem<TState> problem, int iterations, bool resetBeforeSolve)
        {
            RunLBFGS(problem,
                     iterations,
                     memory,
                     sufficientDecrease,
                     backtrackFactor,
                     maxLineSearchSteps,
                     residualTolerance,
                     stepTolerance,
                     resetBeforeSolve,
                     relativeEnergyStop);
        }
    }
}
#endif
