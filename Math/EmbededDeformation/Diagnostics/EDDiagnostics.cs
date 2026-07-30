using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UC.DoubleMath;

#if MATH_NET_AVAILABLE
using MathNet.Numerics;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Verification support for the embedded deformation solver.
    ///
    /// The refactoring work this was written for has to preserve numerical results exactly, so
    /// there needs to be a configuration in which a solve is reproducible bit for bit. Two things
    /// stand in the way by default: the native Math.NET providers multi-thread internally, and
    /// parallel loops that reduce doubles across workers sum them in completion order. Verification
    /// mode removes both.
    /// </summary>
    public static class EDDiagnostics
    {
        // Every double in a dump goes through this. G17 round-trips exactly, so two dumps are
        // bitwise-equal iff their text is equal - which is what makes the comparison a plain
        // line diff rather than a tolerance check.
        public const string numberFormat = "G17";

        private static readonly ParallelOptions serialOptions = new ParallelOptions { MaxDegreeOfParallelism = 1 };
        private static readonly ParallelOptions defaultOptions = new ParallelOptions();

        private static bool _verificationMode;

        public static bool verificationMode => _verificationMode;

        /// <summary>
        /// Passed to every Parallel.For in the solver. Collapses to a single worker while
        /// verifying, so any accidental order dependence shows up as a difference rather than
        /// as intermittent noise.
        /// </summary>
        public static ParallelOptions parallelOptions => (_verificationMode) ? (serialOptions) : (defaultOptions);

        /// <summary>
        /// Trace sink for the per-iteration solver sections. Null when not capturing, which is
        /// how the solvers know to stay quiet.
        /// </summary>
        public static TextWriter activeTrace { get; private set; }

        public static void BeginVerification(TextWriter trace)
        {
            _verificationMode = true;
            activeTrace = trace;

            ApplyMathNetProviders();
        }

        /// <summary>
        /// Forces Math.NET into its only bit-reproducible configuration. Called when verification
        /// starts, and again by the solver's own init so a solve cannot switch back to a native
        /// provider halfway through a capture.
        /// </summary>
        public static void ApplyMathNetProviders()
        {
#if MATH_NET_AVAILABLE
            // The native providers multi-thread internally, so the low bits of a linear solve
            // depend on machine load. Managed on a single thread is the same every run.
            Control.UseManaged();
            Control.UseSingleThread();
#endif
        }

        public static void EndVerification()
        {
            _verificationMode = false;
            activeTrace = null;
        }

        public static void Trace(string line)
        {
            activeTrace?.WriteLine(line);
        }

        #region Formatting

        public static string F(double v) => v.ToString(numberFormat, CultureInfo.InvariantCulture);
        public static string F(float v) => ((double)v).ToString(numberFormat, CultureInfo.InvariantCulture);
        public static string F(Vector3 v) => $"{F(v.x)} {F(v.y)} {F(v.z)}";
        public static string F(DVector3 v) => $"{F(v.x)} {F(v.y)} {F(v.z)}";

        public static string F(IReadOnlyList<int> values)
        {
            if (values == null) return "-";

            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static string F(IReadOnlyList<double> values)
        {
            if (values == null) return "-";

            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(F(values[i]));
            }
            return sb.ToString();
        }

        #endregion

        #region Comparison

        /// <summary>
        /// Compares a freshly captured dump against a stored golden. Reports the first place they
        /// diverge - section, line, both values and the ulp distance - rather than a pass/fail,
        /// because when this fires the useful question is always "which block broke".
        /// </summary>
        public static bool CompareGolden(string goldenPath, string currentPath, out string report)
        {
            if (!File.Exists(goldenPath))
            {
                report = $"No golden at {goldenPath}. Capture one first.";
                return false;
            }

            if (!File.Exists(currentPath))
            {
                report = $"No current dump at {currentPath}.";
                return false;
            }

            string[] golden = File.ReadAllLines(goldenPath);
            string[] current = File.ReadAllLines(currentPath);

            string section = "(none)";
            int shared = Math.Min(golden.Length, current.Length);

            for (int i = 0; i < shared; i++)
            {
                if (golden[i].StartsWith("[")) section = golden[i];

                if (golden[i] == current[i]) continue;

                var sb = new StringBuilder();
                sb.AppendLine($"DIVERGENCE in section {section}, line {i + 1}:");
                sb.AppendLine($"  golden : {Elide(golden[i])}");
                sb.AppendLine($"  current: {Elide(current[i])}");

                long ulps = MaxUlpDistance(golden[i], current[i]);
                if (ulps >= 0)
                    sb.AppendLine($"  max ulp distance across numeric tokens: {ulps}");
                else
                    sb.AppendLine("  lines differ structurally (token count or non-numeric content)");

                report = sb.ToString();
                return false;
            }

            if (golden.Length != current.Length)
            {
                report = $"DIVERGENCE: line count differs after section {section} - golden has {golden.Length}, current has {current.Length}.";
                return false;
            }

            report = $"Identical: {golden.Length} lines match exactly.";
            return true;
        }

        private static string Elide(string s, int max = 200)
            => (s.Length <= max) ? (s) : (s.Substring(0, max) + $"... (+{s.Length - max} chars)");

        /// <summary>
        /// Returns the largest ulp gap between corresponding numeric tokens on the two lines,
        /// or -1 when they cannot be compared token-wise.
        /// </summary>
        private static long MaxUlpDistance(string a, string b)
        {
            var ta = a.Split(' ');
            var tb = b.Split(' ');

            if (ta.Length != tb.Length) return -1;

            long worst = 0;
            bool anyNumeric = false;

            for (int i = 0; i < ta.Length; i++)
            {
                if (ta[i] == tb[i]) continue;

                if ((!double.TryParse(ta[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double va)) ||
                    (!double.TryParse(tb[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double vb)))
                {
                    return -1;
                }

                anyNumeric = true;
                worst = Math.Max(worst, UlpDistance(va, vb));
            }

            return (anyNumeric) ? (worst) : (-1);
        }

        public static long UlpDistance(double a, double b)
        {
            if ((double.IsNaN(a)) || (double.IsNaN(b))) return long.MaxValue;
            if (a == b) return 0;

            long la = BitConverter.DoubleToInt64Bits(a);
            long lb = BitConverter.DoubleToInt64Bits(b);

            // Doubles are stored sign-magnitude; map the negative half onto a continuous
            // ordering so that subtracting the two gives a meaningful step count.
            if (la < 0) la = long.MinValue - la;
            if (lb < 0) lb = long.MinValue - lb;

            return Math.Abs(la - lb);
        }

        #endregion
    }
}
#endif
