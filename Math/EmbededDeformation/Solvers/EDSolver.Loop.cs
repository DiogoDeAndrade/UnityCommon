using System.Linq;
using MathNet.Numerics.LinearAlgebra;

#if UC_ENABLE_ED
namespace UC.ED
{
    // What the loops share, moved verbatim from EmbededDeformation.Solvers.cs on 2026-10-22. Free
    // of Unity types, as every .Loop.cs part is, so Tools/MeshlessCheck compiles the loops in place.
    public abstract partial class EDSolver
    {
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

        internal static bool TrySolveCholeskyWithDamping(Matrix<double> H, Vector<double> g, double initialLambda, out Vector<double> delta, out double usedLambda)
        {
            delta = null;
            usedLambda = initialLambda;

            const int maxAttempts = 8;
            const double lambdaMultiplier = 10.0;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var Hlm = H.Clone();

                for (int i = 0; i < Hlm.RowCount; i++)
                    Hlm[i, i] += usedLambda;

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
