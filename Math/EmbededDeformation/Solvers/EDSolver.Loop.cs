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
