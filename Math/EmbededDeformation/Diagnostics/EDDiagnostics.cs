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
        /// Whether a normal (non-verification) solve may use the native Math.NET providers.
        ///
        /// Off, because turning them on is what makes a solve on this map piece take minutes
        /// instead of seconds. It was previously reached only from the navigation solver, so it
        /// looked like configuration order mattered - a Gauss-Newton run was fast in a fresh session
        /// and slow once a navigation solve had switched the providers under it. Both are the same
        /// fault seen from different directions.
        ///
        /// Left as a flag rather than deleted because the intent - native BLAS for large solves - is
        /// reasonable, and this is a small dense problem where the setup and threading cost swamps
        /// any gain. Worth revisiting with a measurement rather than by flipping it back.
        /// </summary>
        public static bool allowNativeProviders = false;

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

        public static void EndVerification()
        {
            _verificationMode = false;
            activeTrace = null;

            // Put the providers back. Without this the managed single-threaded configuration
            // outlived the capture that asked for it and every later solve in the session ran under
            // it, which is a process-global setting changing because of something that already
            // finished.
            ApplyMathNetProviders();
        }

        /// <summary>
        /// Sets the Math.NET providers this session wants, and is the only thing that does.
        ///
        /// These are process-global and survive anything short of a domain reload, so a solve that
        /// does not set them runs under whatever the previous one left behind - which made the cost
        /// and the low bits of a solve depend on which configuration had been run before it in the
        /// same editor session. Every solver entry point calls this, so none of them inherit.
        ///
        /// It reads verificationMode rather than taking an argument, so there is no way to ask for
        /// a capture and a non-reproducible provider at the same time.
        /// </summary>
        public static void ApplyMathNetProviders()
        {
#if MATH_NET_AVAILABLE
            if (_verificationMode)
            {
                // The native providers multi-thread internally, so the low bits of a linear solve
                // depend on machine load. Managed on a single thread is the same every run.
                Control.UseManaged();
                Control.UseSingleThread();

                LogProviderChange();
                return;
            }

            if (!allowNativeProviders)
            {
                // Managed and multi-threaded: what Math.NET initialises itself to, and what a solve
                // in a fresh editor session used to get. Deliberately not touching
                // MaxDegreeOfParallelism either, so this is exactly the default rather than an
                // approximation of it.
                Control.UseManaged();
                Control.UseMultiThreading();

                LogProviderChange();
                return;
            }

            Control.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1);

            try
            {
                Control.NativeProviderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Plugins/MathNet/OpenBLAS/win-x64"));

                bool nativeOk = Control.TryUseNativeOpenBLAS();

                if (!nativeOk)
                    nativeOk = Control.TryUseNativeMKL();

                if (!nativeOk)
                    Control.UseMultiThreading();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Math.NET native provider failed: {e.Message}");

                Control.UseMultiThreading();
            }

            LogProviderChange();
#endif
        }

#if MATH_NET_AVAILABLE
        private static string lastDescribedProviders;

        /// <summary>
        /// Logs the provider configuration, but only when it actually changes. This used to be
        /// printed on every navigation solve and by nothing else, which is the reason a solve
        /// silently inheriting another configuration's providers was invisible.
        /// </summary>
        private static void LogProviderChange()
        {
            string description = Control.Describe();

            if (description == lastDescribedProviders) return;

            lastDescribedProviders = description;

            Debug.Log($"[ED] Math.NET providers now: {description}");
        }
#endif

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

            // Reported per section rather than stopping at the first difference. Which sections
            // moved is the diagnostic that matters: a change confined to [config] means settings
            // were reorganised, while one in [graph] or [final] means behaviour changed.
            var sectionsSeen = new List<string>();
            var firstInSection = new Dictionary<string, string>();
            var countInSection = new Dictionary<string, int>();

            for (int i = 0; i < shared; i++)
            {
                if (golden[i].StartsWith("["))
                    section = golden[i];

                if (golden[i] == current[i]) continue;

                if (!countInSection.ContainsKey(section))
                {
                    sectionsSeen.Add(section);
                    countInSection[section] = 0;

                    var detail = new StringBuilder();
                    detail.AppendLine($"  line {i + 1}");
                    detail.AppendLine($"    golden : {Elide(golden[i])}");
                    detail.AppendLine($"    current: {Elide(current[i])}");

                    long ulps = MaxUlpDistance(golden[i], current[i]);
                    if (ulps >= 0)
                        detail.AppendLine($"    max ulp distance across numeric tokens: {ulps}");
                    else
                        detail.AppendLine("    lines differ structurally (token count or non-numeric content)");

                    firstInSection[section] = detail.ToString();
                }

                countInSection[section]++;
            }

            bool lengthDiffers = (golden.Length != current.Length);

            if ((sectionsSeen.Count == 0) && (!lengthDiffers))
            {
                report = $"Identical: {golden.Length} lines match exactly.";
                return true;
            }

            var sb2 = new StringBuilder();
            sb2.AppendLine($"Diverging sections: {string.Join(", ", sectionsSeen)}");

            if (lengthDiffers)
                sb2.AppendLine($"Line count differs - golden has {golden.Length}, current has {current.Length}.");

            foreach (var s in sectionsSeen)
            {
                sb2.AppendLine($"{s}  ({countInSection[s]} differing line(s)), first:");
                sb2.Append(firstInSection[s]);
            }

            report = sb2.ToString();
            return false;
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
