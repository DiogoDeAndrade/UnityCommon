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
        public void SolveED_GN(int maxIterations, EDEnergyModel.Instance energy,
                               double damping = 1.0,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true)
        {
            if (resetBeforeSolve)
                ResetDeformation();

            InitMathNet();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            // Row counts and weights are resolved once here rather than rebuilt inside every
            // residual evaluation, as the layout builder used to be. They are a function of the
            // graph and the weights, neither of which changes during a solve.
            energy.Resolve();

            TraceResidualLayout(energy);

            DebugProfiler.DebugMark(timeIteration);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                CountSolveIteration();

                var stateView = new EDStateView(currentState);

                var f = energy.EvaluateResidual(stateView);

                double error = f.L2Norm();

                ReportIteration(f, energy, stateView);

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                // Already solved / close enough
                if (!double.IsFinite(error) || error < residualTolerance)
                {
                    break;
                }

                var J = energy.BuildJacobian(currentState, out double jNorm);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if (!double.IsFinite(jNorm) || jNorm < 1e-12)
                {
                    break;
                }

                Vector<double> delta;

                try
                {
                    DebugProfiler.DebugMark(timeSolve);

                    var qr = J.QR();
                    delta = qr.Solve(-f);

                    DebugProfiler.DebugMark(timeSolve);
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

            DebugProfiler.DebugMark(timeIteration);
#else
    throw new NotImplementedException();
#endif
        }

        public void SolveED_LM(int maxIterations,
                               EDEnergyModel.Instance energy,
                               double lambda = 1e-3,
                               double residualTolerance = 1e-5,
                               double stepTolerance = 1e-6,
                               bool resetBeforeSolve = true,
                               bool adaptiveLambda = true)
        {
            if (resetBeforeSolve)
                ResetDeformation();

            InitMathNet();

#if MATH_NET_AVAILABLE
            if (currentState == null)
                currentState = new EDState(nodes.Count);

            double currentLambda = lambda;

            // Row counts and weights are resolved once here rather than rebuilt inside every
            // residual evaluation, as the layout builder used to be. They are a function of the
            // graph and the weights, neither of which changes during a solve.
            energy.Resolve();

            TraceResidualLayout(energy);

            DebugProfiler.DebugMark(timeIteration);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                CountSolveIteration();

                var stateView = new EDStateView(currentState);

                var f = energy.EvaluateResidual(stateView);

                double error = f.L2Norm();

                ReportIteration(f, energy, stateView);

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    Debug.LogError("[ED] Residual became non-finite.");
                    return;
                }

                if (error < residualTolerance)
                    break;

                var J = energy.BuildJacobian(currentState, out double jNorm);

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
                        DebugProfiler.DebugMark(timeSolve);

                        delta = Hlm.Solve(-g);

                        DebugProfiler.DebugMark(timeSolve);
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

                    var fCandidate = energy.EvaluateResidual(candidateView);

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

                            DebugProfiler.DebugMark(timeIteration);

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

            DebugProfiler.DebugMark(timeIteration);
#else
    throw new NotImplementedException();
#endif
        }

        // The configuration itself lives on EDDiagnostics, because verification mode is what decides
        // it and because these providers are process-global: whoever sets them last wins for the
        // rest of the session. Kept as a named call at the top of each solver so it is visible that
        // a solve configures its own environment rather than trusting what it finds.
        static void InitMathNet() => EDDiagnostics.ApplyMathNetProviders();

        public void SolveED_Nav(int maxIterations,
                                EDEnergyModel.Instance energy,
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
            // Row counts and weights are resolved once here rather than rebuilt inside every
            // residual evaluation, as the layout builder used to be. They are a function of the
            // graph and the weights, neither of which changes during a solve.
            energy.Resolve();

            TraceResidualLayout(energy);

            int iter = 0;

            DebugProfiler.DebugMark(timeIteration);

            for (iter = 0; iter < maxIterations; iter++)
            {
                CountSolveIteration();

                var stateView = new EDStateView(currentState);

                var f = energy.EvaluateResidual(stateView);

                double error = f.L2Norm();

                LogResidualEnergies(f, energy, iter);
                ReportIteration(f, energy, stateView);

                EDDiagnostics.Trace($"[iter {iter}] residual {EDDiagnostics.F(error)}");

                if (!double.IsFinite(error))
                {
                    Debug.LogError($"[ED] Residual became non-finite after {iter} iterations.");
                    return;
                }

                if (error < residualTolerance)
                {
                    break;
                }

                var J = energy.BuildJacobian(currentState, out double jNorm);

                EDDiagnostics.Trace($"[iter {iter}] jNorm {EDDiagnostics.F(jNorm)}");

                if ((!double.IsFinite(jNorm)) || (jNorm < 1e-12))
                {
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

                    var fCandidate = energy.EvaluateResidual(candidateView);

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
                ComputeClearance(currentState);
            }

            DebugProfiler.DebugMark(timeIteration);

            var acceptedView = new EDStateView(currentState);
            var acceptedResidual = energy.EvaluateResidual(acceptedView);

            LogResidualEnergies(acceptedResidual, energy, iter);
            ReportIteration(acceptedResidual, energy, acceptedView);
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
