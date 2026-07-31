using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    public partial class EmbededDeformation
    {
        #region Solver
        public void SolveED_GN(int maxIterations = 10, WeightConfig weights = null,
                               double damping = 1.0,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true)
        {
            if (resetBeforeSolve)
                ResetDeformation();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            var BuildJacobian = buildJacobian;
            var EvaluateResidualVector = evaluateResidualVector;

            TraceResidualLayout(weights);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                var stateView = new EDStateView(currentState);

                var f = EvaluateResidualVector(stateView, weights);

                double error = f.L2Norm();

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                // Already solved / close enough
                if (!double.IsFinite(error) || error < residualTolerance)
                {
                    break;
                }

                var J = BuildJacobian(currentState, out double jNorm, weights);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if (!double.IsFinite(jNorm) || jNorm < 1e-12)
                {
                    break;
                }

                Vector<double> delta;

                try
                {
                    var qr = J.QR();
                    delta = qr.Solve(-f);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"SolveED failed: {ex.Message}");
                    return;
                }

                double stepNorm = delta.L2Norm();

                if (!double.IsFinite(stepNorm))
                {
                    Debug.LogError("[ED] SolveED produced non-finite delta.");
                    return;
                }

                if (stepNorm < stepTolerance)
                {
                    break;
                }

                currentState.Apply(delta, damping);
            }
#else
    throw new NotImplementedException();
#endif
        }

        public void SolveED_LM(int maxIterations = 10,
                               WeightConfig weights = null,
                               double lambda = 1e-3,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true,
                               bool adaptiveLambda = true)
        {
            if (resetBeforeSolve)
                ResetDeformation();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            double currentLambda = lambda;

            var BuildJacobian = buildJacobian;
            var EvaluateResidualVector = evaluateResidualVector;

            TraceResidualLayout(weights);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                var stateView = new EDStateView(currentState);

                var f = EvaluateResidualVector(stateView, weights);

                double error = f.L2Norm();

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    Debug.LogError("[ED] Residual became non-finite.");
                    return;
                }

                if (error < residualTolerance)
                    break;

                var J = BuildJacobian(currentState, out double jNorm, weights);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                    break;

                var JT = J.Transpose();
                var H = JT * J;
                var g = JT * f;

                Vector<double> delta = null;
                EDState acceptedState = null;
                bool solved = false;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var Hlm = H.Clone();

                    for (int i = 0; i < Hlm.RowCount; i++)
                        Hlm[i, i] += currentLambda;

                    try
                    {
                        delta = Hlm.Solve(-g);
                    }
                    catch
                    {
                        delta = null;
                    }

                    if (delta == null)
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    double stepNorm = delta.L2Norm();

                    if (!double.IsFinite(stepNorm))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    EDState candidateState;

                    try
                    {
                        candidateState = currentState.CloneAndApply(delta, 1.0);
                    }
                    catch
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    var candidateView = new EDStateView(candidateState);

                    var fCandidate = EvaluateResidualVector(candidateView, weights);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    if (candidateError <= error)
                    {
                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        if (stepNorm < stepTolerance)
                        {
                            currentState = acceptedState;
                            return;
                        }

                        break;
                    }

                    currentLambda *= 10.0;
                }

                if (!solved)
                {
                    Debug.LogWarning("[ED] LM could not find an improving step.");
                    break;
                }

                currentState = acceptedState;
            }
#else
    throw new NotImplementedException();
#endif
        }

        static void InitMathNet()
        {
            if (EDDiagnostics.verificationMode)
            {
                EDDiagnostics.ApplyMathNetProviders();

                Debug.Log("[ED] Verification mode: Math.NET managed provider, single thread.");
                return;
            }

            Control.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);

            bool nativeOk = false;

            try
            {
                // Optional: point this to the folder containing the native DLLs.
                // For testing, an absolute path is fine.
                Control.NativeProviderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Plugins/MathNet/OpenBLAS/win-x64"));

                nativeOk = Control.TryUseNativeOpenBLAS();

                if (!nativeOk)
                    nativeOk = Control.TryUseNativeMKL();

                if (!nativeOk)
                    Control.UseMultiThreading();

                Debug.Log($"Math.NET native provider active: {nativeOk}");
                Debug.Log(Control.Describe());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Math.NET native provider failed: {e.Message}");

                Control.UseMultiThreading();
                Debug.Log(Control.Describe());
            }
        }

        public void SolveED_Nav(int maxIterations = 10,
                                WeightConfig weights = null,
                                double lambda = 1e-3,
                                double residualTolerance = 1e-5,
                                double stepTolerance = 1e-6,
                                bool resetBeforeSolve = true,
                                bool adaptiveLambda = true,
                                bool choleskyFactorization = false)
        {
            if (resetBeforeSolve)
                ResetDeformation();

            InitMathNet();

#if MATH_NET_AVAILABLE
            if (currentState == null)
            {
                currentState = new EDState(nodes.Count);
                ComputeClearance(currentState);
            }

            double currentLambda = lambda;
            var BuildJacobian = buildJacobian;
            var EvaluateResidualVector = evaluateResidualVector;

            TraceResidualLayout(weights);

            int iter = 0;

            for (iter = 0; iter < maxIterations; iter++)
            {
                DebugProfiler.DebugMark(timeIteration);

                var stateView = new EDStateView(currentState);

                var f = EvaluateResidualVector(stateView, weights);

                double error = f.L2Norm();

                LogResidualEnergies(f, weights, iter);

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    Debug.LogError($"[ED] Residual became non-finite after {iter} iterations.");
                    DebugProfiler.DebugMark(timeIteration);
                    return;
                }

                if (error < residualTolerance)
                {
                    DebugProfiler.DebugMark(timeIteration);
                    break;
                }

                var J = BuildJacobian(currentState, out double jNorm, weights);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                /*int nonZero = 0;
                int total = J.RowCount * J.ColumnCount;

                for (int r = 0; r < J.RowCount; r++)
                {
                    for (int c = 0; c < J.ColumnCount; c++)
                    {
                        if (Math.Abs(J[r, c]) > 1e-12)
                            nonZero++;
                    }
                }

                Debug.Log($"Jacobian density (iteration {iter}): {(100.0 * nonZero / total):F2}% ({nonZero}/{total})");*/

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                {
                    DebugProfiler.DebugMark(timeIteration);
                    break;
                }

                var JT = J.Transpose();

                var H = JT * J; // approximate Hessian
                var g = JT * f; // gradient term

                Vector<double> delta = null;
                EDState acceptedState = null;
                bool solved = false;

                // Try current lambda, optionally increasing it if solve or step is bad.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    DebugProfiler.DebugMark(timeSolve);

                    if (choleskyFactorization)
                    {
                        if (!TrySolveCholeskyWithDamping(H, g, currentLambda, out delta, out double usedLambda))
                        {
                            currentLambda = usedLambda;
                            continue;
                        }

                        currentLambda = usedLambda;
                    }
                    else
                    {
                        var Hlm = H.Clone();

                        for (int i = 0; i < Hlm.RowCount; i++)
                            Hlm[i, i] += currentLambda;

                        try
                        {
                            delta = Hlm.Solve(-g);
                        }
                        catch
                        {
                            delta = null;
                        }
                    }

                    DebugProfiler.DebugMark(timeSolve);

                    if (delta == null)
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    double stepNorm = delta.L2Norm();

                    if (!double.IsFinite(stepNorm))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    EDState candidateState;

                    try
                    {
                        candidateState = currentState.CloneAndApply(delta, 1.0);
                        ComputeClearance(candidateState);
                    }
                    catch
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    var candidateView = new EDStateView(candidateState);

                    var fCandidate = EvaluateResidualVector(candidateView, weights);

                    double candidateError = fCandidate.L2Norm();

                    if (!double.IsFinite(candidateError))
                    {
                        currentLambda *= 10.0;
                        continue;
                    }

                    // Accept only if it improves the residual.
                    if (candidateError <= error)
                    {
                        EDDiagnostics.Trace($"[iter {iter}] accepted attempt {attempt} lambda {EDDiagnostics.F(currentLambda)} step {EDDiagnostics.F(stepNorm)} candidateError {EDDiagnostics.F(candidateError)}");

                        acceptedState = candidateState;
                        solved = true;

                        if (adaptiveLambda)
                            currentLambda = Math.Max(currentLambda * 0.3, 1e-12);

                        if (stepNorm < stepTolerance)
                        {
                            currentState = acceptedState;

                            DebugProfiler.DebugMark(timeIteration);

                            Debug.Log($"Ran {iter} iterations...");
                            LogTimerReport();

                            return;
                        }

                        break;
                    }

                    currentLambda *= 10.0;
                }

                if (!solved)
                {
                    Debug.LogWarning("[ED] LM could not find an improving step.");
                    DebugProfiler.DebugMark(timeIteration);
                    break;
                }

                currentState = acceptedState;
                ComputeClearance(currentState);

                DebugProfiler.DebugMark(timeIteration);
            }

            var acceptedView = new EDStateView(currentState);
            var acceptedResidual = EvaluateResidualVector(acceptedView, weights);

            LogResidualEnergies(acceptedResidual, weights, iter);
            LogTimerReport();
#else
    throw new NotImplementedException();
#endif
        }

        bool TrySolveCholeskyWithDamping(Matrix<double> H, Vector<double> g, double initialLambda, out Vector<double> delta, out double usedLambda)
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

        #endregion
    }
}
#endif
