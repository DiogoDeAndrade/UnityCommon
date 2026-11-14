using System;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// What the damped solvers add the damping to the normal equations' diagonal in proportion to.
    /// Identity, the default and what every damped solve did until 2026-11-05, adds the same
    /// damping to every column. Marquardt adds it in proportion to each column's own curvature,
    /// the diagonal of JᵀJ at the iteration's Jacobian, so the damping is a fraction of what the
    /// energy already holds a column by rather than a fixed amount in the parameter's units.
    /// Declared here rather than beside the solver assets so the loops, which are free of Unity
    /// types, can read it.
    /// </summary>
    public enum EDDampingScaling
    {
        Identity,
        Marquardt,
    }

    /// <summary>
    /// The RemainingDisplacement stop (2026-11-12): how far the problem's geometry still has to
    /// travel, estimated from the steps the solve has taken, against a tolerance.
    ///
    /// A step is the largest distance any point of the geometry moved in one iteration, over the
    /// geometry's largest side (<see cref="IEDSolverProblem{TState}.GeometryStep"/>). Where an
    /// iteration converges linearly each step is rho times the one before, and the travel still
    /// to come is the series step (rho + rho^2 + ...) = step rho / (1 - rho). A threshold on one
    /// iteration's gain, in the energy or in the step, misses that factor: it is under one for a
    /// damped Newton solve near its minimum and was about 590 for the local-global iteration of
    /// volumetric ARAP on the cactus, where a 1e-5 energy rule stopped with an arm 14% of the
    /// mesh's size from where it ends (docs/ved-plan.md, 2026-11-12). rho is not given by the
    /// solver; it is read off the steps, at every scale the steps so far allow, and the largest
    /// is taken: the last h steps summed over the h before them, as a rate per step, for every h
    /// from one - the last step over the one before it - to half the <see cref="window"/>. Sums
    /// and not two samples, because the steps of a line search or of a damping that moves are
    /// erratic, and a ratio of two of them said "converging" of an L-BFGS solve that was 4% of
    /// its size from its end (Tools/MeshlessCheck, the day this was written); the largest and not
    /// the longest, so that one sudden small step (a damping raised by an order, a refused
    /// direction) followed by steps of its own size reads as steps that are not shrinking, which
    /// the short sums say at once and the long ones only once the large steps have left them.
    /// With rho at one or over nothing can be said, and the estimate is infinite. The factor is
    /// never taken under one, so where the solve converges fast the rule is "no point moved more
    /// than the tolerance".
    ///
    /// The estimate has to be under the tolerance on consecutive iterations to stop the solve:
    /// two where the steps are at least halving (<see cref="fastRate"/>), the energy rule's
    /// convention, and <see cref="slowConfirmations"/> where they shrink more slowly - there the
    /// estimate is the step times a large factor read off noisy steps, an iteration is a small
    /// part of the solve, and against steps that only look as if they were shrinking the extra
    /// confirmations are what keeps the stop from firing by chance (measured on the day, on
    /// synthetic plateaus of a thousand steps a tenth of the tolerance with 30% noise: a false
    /// stop one time in 300 with four and one in six with two; with two large steps before the
    /// plateau, one in fourteen with four and one in two with two). It is an estimate from the
    /// solve's own steps and no more: a plateau noisy enough can still pass for convergence, and
    /// a direction the solve has not begun to move along is not in the steps at all.
    ///
    /// The same rule, constant for constant, as RemainingDisplacement in Tools/ARAP/arap_deform.py.
    ///
    /// **The RecentDisplacement stop (2026-11-14) is the second rule this class holds, and it has
    /// no rate in it.** On a Levenberg-Marquardt solve of VED (the cactus, docs/ved-plan.md item
    /// 5) the steps follow the damping - times 0.3 on an acceptance, times ten on a refusal - so
    /// a step was larger than the one before it on half the iterations of the solve's long tail,
    /// rho read one or more, and the estimate above was infinite on 136 rows of 146: the rule
    /// never fired, and a rate taken on windows of ten fired with seven times the tolerance still
    /// to come. What can be said of such a solve is what it has just done: the largest distance
    /// any point of the geometry is from where it was <c>recentWindow</c> iterations before
    /// (<see cref="IEDSolverProblem{TState}.GeometryTravel"/>), in which steps that go back and
    /// forth with the damping cancel, where they add in a sum of their lengths. The rule stops
    /// the solve once that distance is under its tolerance on <see cref="recentConfirmations"/>
    /// consecutive iterations, the energy rule's convention. It bounds the movement of the last
    /// iterations and not the movement left: on that solve a tolerance of 0.1% stopped where up
    /// to 0.7% was still to come. And it is not a replacement for the rule above where the steps
    /// do shrink at a rate: on the local-global iteration of ARAP it would stop with a step of a
    /// tenth of its tolerance and 585 times that still to travel.
    /// </summary>
    public sealed class EDDisplacementStop
    {
        public const int window = 20;
        public const double fastRate = 0.5;
        public const int slowConfirmations = 4;
        public const int recentConfirmations = 2;

        private readonly double tolerance;
        private readonly System.Collections.Generic.List<double> steps = new System.Collections.Generic.List<double>();
        private int flat;

        private readonly double recentTolerance;
        private readonly int recentWindow;
        private int stepsSinceReset;
        private int recentFlat;

        /// <summary>The estimate after the last step taken: infinite before two steps, or while the steps are not shrinking.</summary>
        public double estimate { get; private set; } = double.PositiveInfinity;

        /// <summary>How many iterations back the RecentDisplacement rule looks, or zero when that rule is off and no travel need be measured.</summary>
        public int travelWindow => (recentTolerance > 0.0) ? (recentWindow) : (0);

        /// <summary>Which of the two rules ended the solve, once <see cref="Add(double, double)"/> has answered true.</summary>
        public EDSolveEnd reached { get; private set; } = EDSolveEnd.DisplacementStop;

        public EDDisplacementStop(double tolerance, double recentTolerance = 0.0, int recentWindow = 10)
        {
            this.tolerance = tolerance;
            this.recentTolerance = recentTolerance;
            this.recentWindow = Math.Max(1, recentWindow);
        }

        /// <summary>Forgets the steps so far: what follows is another objective's (the inputs moved).</summary>
        public void Reset()
        {
            steps.Clear();
            flat = 0;
            estimate = double.PositiveInfinity;

            stepsSinceReset = 0;
            recentFlat = 0;
        }

        /// <summary>
        /// Takes an iteration's step and, for the RecentDisplacement rule, the geometry's travel
        /// over the last <see cref="travelWindow"/> iterations; true once either rule says the
        /// solve should stop, <see cref="reached"/> then saying which. The travel counts only once
        /// that many steps have been taken since the last reset, so a window never reaches back
        /// across a moved request, and a travel that is not finite is no measurement.
        /// </summary>
        public bool Add(double step, double travel)
        {
            bool remaining = Add(step);

            if (!double.IsFinite(step)) return false;

            stepsSinceReset++;

            bool measured = (recentTolerance > 0.0) && (stepsSinceReset >= recentWindow) && (double.IsFinite(travel));

            recentFlat = ((measured) && (travel < recentTolerance)) ? (recentFlat + 1) : (0);

            if (remaining)
            {
                reached = EDSolveEnd.DisplacementStop;

                return true;
            }

            if (recentFlat >= recentConfirmations)
            {
                reached = EDSolveEnd.RecentDisplacementStop;

                return true;
            }

            return false;
        }

        private static double Ratio(double now, double before, int count)
        {
            if (before > 0.0) return Math.Pow(now / before, 1.0 / count);

            return (now == 0.0) ? (0.0) : (double.PositiveInfinity);
        }

        /// <summary>Takes an iteration's step; true once the solve should stop. A step that is not finite is not a measurement and never stops it.</summary>
        public bool Add(double step)
        {
            if (!double.IsFinite(step))
            {
                Reset();

                return false;
            }

            steps.Add(step);

            int k = steps.Count - 1;

            double rho = 0.0;

            if (k == 0)
            {
                estimate = double.PositiveInfinity;
            }
            else
            {
                // The last h steps over the h before them, for every h the steps so far allow.
                int longest = Math.Min(window / 2, steps.Count / 2);

                for (int h = 1; h <= longest; h++)
                {
                    double newer = 0.0, older = 0.0;

                    for (int i = 0; i < h; i++)
                    {
                        newer += steps[k - i];
                        older += steps[k - h - i];
                    }

                    rho = Math.Max(rho, Ratio(newer, older, h));
                }

                estimate = (rho >= 1.0) ? (double.PositiveInfinity) : (step * Math.Max(1.0, rho / (1.0 - rho)));
            }

            flat = (estimate < tolerance) ? (flat + 1) : (0);

            return (tolerance > 0.0) && (flat >= ((rho <= fastRate) ? (2) : (slowConfirmations)));
        }
    }

    // What the loops share, moved verbatim from EmbededDeformation.Solvers.cs on 2026-10-22. Free
    // of Unity types, as every .Loop.cs part is, so Tools/MeshlessCheck compiles the loops in place.
    public abstract partial class EDSolver
    {
        // Why the option exists (2026-11-05, docs/ved-plan.md item 8). With the identity, a
        // damping of L holds every parameter at its current value with the same stiffness L, so
        // a step that moves many parameters pays for each of them. On the unpinned Armadillo the
        // field term's refusals kept L near 0.15, forty times what regularization holds a far
        // body node's translation by, and a carry of the whole body - the energy's deeper state,
        // nearly flat for every term - paid that on every node while a bend of the arm paid it on
        // a few: the solve bent the arm and stayed on that plateau for 1,000 iterations, where
        // the same solve restarted at a damping of 1e-6 turned the body in 16. Scaled by the
        // diagonal, a column's damping is a fraction of its own curvature, so the field term's
        // stiff columns can be held hard while the body's soft ones are held softly by the same
        // number. The textbook form (Marquardt 1963); Moré's running maximum of the diagonal
        // (MINPACK) is the steadier variant, not built.

        /// <summary>
        /// The factor each column's damping is multiplied by, from the normal equations' diagonal,
        /// or null under the identity - for which the loops add the damping exactly as they always
        /// did. A diagonal entry under <paramref name="relativeFloor"/> times the largest is raised
        /// to it, so a column no row reaches still gets some damping and the damped matrix stays
        /// non-singular.
        /// </summary>
        internal static double[] DampingScale(Matrix<double> H, EDDampingScaling scaling, double relativeFloor)
        {
            if (scaling == EDDampingScaling.Identity) return null;

            int n = H.RowCount;
            var scale = new double[n];

            double largest = 0.0;

            for (int i = 0; i < n; i++)
            {
                scale[i] = H[i, i];

                if (scale[i] > largest) largest = scale[i];
            }

            double minimum = Math.Max(relativeFloor, 0.0) * largest;

            for (int i = 0; i < n; i++)
                scale[i] = Math.Max(scale[i], minimum);

            return scale;
        }

        /// <summary>
        /// Adds the damping to the diagonal of a copy of the normal equations: the same amount to
        /// every column when <paramref name="scale"/> is null - the statement the loops always ran
        /// - or in proportion to each column's scale otherwise.
        /// </summary>
        internal static void AddDamping(Matrix<double> Hlm, double lambda, double[] scale)
        {
            if (scale == null)
            {
                for (int i = 0; i < Hlm.RowCount; i++)
                    Hlm[i, i] += lambda;
            }
            else
            {
                for (int i = 0; i < Hlm.RowCount; i++)
                    Hlm[i, i] += lambda * scale[i];
            }
        }

        /// <summary>
        /// The DeltaEnergy stop: true once the relative improvement of the total energy - the
        /// squared residual norm, the quantity the export's total column carries - has stayed
        /// below the threshold for two consecutive iterations. Two rather than one because a
        /// single near-flat iteration mid-descent exists in the sweep data and a repeated one
        /// never resumed (2026-08-27, zero resume events over 26 runs). A threshold of zero
        /// disables the check entirely, which is the MaxIterations criteria and the default -
        /// the path every golden runs.
        /// </summary>
        internal static bool ShouldStopOnDeltaEnergy(double threshold, double previousError, double error, ref int flatIterations)
        {
            if ((threshold <= 0.0) || (!double.IsFinite(previousError))) return false;

            double before = previousError * previousError;
            double after = error * error;

            double improvement = (before > 0.0) ? ((before - after) / before) : (0.0);

            if (improvement < threshold) flatIterations++;
            else flatIterations = 0;

            return flatIterations >= 2;
        }

        /// <summary>
        /// The RemainingDisplacement stop for a solve about to begin, or null with a tolerance of
        /// zero - the criteria is another, and the geometry is then never measured, which is the
        /// path every golden runs. Hands the problem the state the solve departs from, so the
        /// first step taken has something to be measured against; a problem with no geometry to
        /// measure says so there, and the solve runs on without the stop.
        /// </summary>
        internal static EDDisplacementStop BeginDisplacementStop<TState>(IEDSolverProblem<TState> problem, double tolerance,
                                                                         double recentTolerance = 0.0, int recentWindow = 10) where TState : class
        {
            if ((tolerance <= 0.0) && (recentTolerance <= 0.0)) return null;

            if (double.IsNaN(problem.GeometryStep(problem.current)))
            {
                problem.Log(EDSolverLogLevel.Warning, "[ED] A displacement stop is on and the problem has no geometry to measure a step on; the solve runs its iterations.");

                return null;
            }

            var stop = new EDDisplacementStop(tolerance, recentTolerance, recentWindow);

            // Asked once here, with nothing yet to answer, so the problem knows how many states
            // back the RecentDisplacement rule will look and keeps that many from the first step.
            if (stop.travelWindow > 0) problem.GeometryTravel(stop.travelWindow);

            return stop;
        }

        /// <summary>
        /// One step of a solve under a displacement stop: the geometry is measured at the state
        /// the step produced and handed to the stop; true once the solve should end, the stop's
        /// <see cref="EDDisplacementStop.reached"/> then saying by which rule.
        /// </summary>
        internal static bool TakeDisplacementStep<TState>(EDDisplacementStop stop, IEDSolverProblem<TState> problem, TState state) where TState : class
        {
            double step = problem.GeometryStep(state);
            double travel = (stop.travelWindow > 0) ? (problem.GeometryTravel(stop.travelWindow)) : (double.NaN);

            return stop.Add(step, travel);
        }

        internal static bool TrySolveCholeskyWithDamping(Matrix<double> H, Vector<double> g, double initialLambda, out Vector<double> delta, out double usedLambda,
                                                         double[] dampingScale = null)
        {
            delta = null;
            usedLambda = initialLambda;

            const int maxAttempts = 8;
            const double lambdaMultiplier = 10.0;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var Hlm = H.Clone();

                AddDamping(Hlm, usedLambda, dampingScale);

                try
                {
                    var chol = Hlm.Cholesky();
                    delta = chol.Solve(-g);
                    return delta.All(v => double.IsFinite(v));
                }
                catch
                {
                    usedLambda *= lambdaMultiplier;
                }
            }

            return false;
        }
    }
}
#endif
