using System;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class ValueRange
    {
        public enum Mode
        {
            Constant,
            Uniform,
            Triangular,
            GaussianClamped,
            BiasedUniform
        }

        public Mode mode = Mode.Uniform;

        [Tooltip("Center of the distribution. For Constant, this is the value.")]
        public float mean = 1.0f;

        [Tooltip("Half-width around mean. Min = mean - range, Max = mean + range (unless range is 0)."), Min(0f)]
        public float range = 0.0f;

        // Gaussian controls (only used when mode == GaussianClamped)
        [Tooltip("Standard deviation as a fraction of 'range'. Example: 0.33 => sigma = range*0.33. If range=0, sigma=0."), Min(0f)]
        public float gaussianSigmaFrac = 0.33f;

        // Biased uniform controls (only used when mode == BiasedUniform)
        [Tooltip("0 = strongly towards Min, 0.5 = neutral (uniform), 1 = strongly towards Max."), Range(0f, 1f)]
        public float bias = 0.5f;

        // If you want to allow clamping to something other than [mean-range, mean+range], add these later.
        // For now, we clamp to the implicit min/max.
        public float Min => mean - range;
        public float Max => mean + range;

        public float GetRandom(System.Random rng = null)
        {
            // Choose RNG source: UnityEngine.Random by default (deterministic per Unity seed),
            // or System.Random if you pass one.
            float Next01()
            {
                if (rng != null) return (float)rng.NextDouble();
                return UnityEngine.Random.value;
            }

            float min = Min;
            float max = Max;

            switch (mode)
            {
                case Mode.Constant:
                    return mean;
                case Mode.Uniform:
                    if (range <= 0f) return mean;
                    return Mathf.Lerp(min, max, Next01());
                case Mode.Triangular:
                    // Triangular with mode at mean (clamped to [min,max]).
                    // Uses inverse CDF. Mean as mode gives a strong "usually near mean" feel.
                    if (range <= 0f) return mean;
                    return SampleTriangular(min, max, Mathf.Clamp(mean, min, max), Next01());
                case Mode.GaussianClamped:
                    if (range <= 0f) return mean;
                    return SampleGaussianClamped(mean, min, max, range * gaussianSigmaFrac, Next01);
                case Mode.BiasedUniform:
                    if (range <= 0f) return mean;
                    // Remap uniform through an exponent to bias towards ends.
                    // bias=0.5 -> uniform. bias<0.5 -> towards min. bias>0.5 -> towards max.
                    return SampleBiasedUniform(min, max, bias, Next01());
                default:
                    return mean;
            }
        }

        public float GetExpectedValue()
        {
            // Useful sometimes (DPS estimates, UI tooltips)
            // Constant/Uniform/Triangular are exact; clamped Gaussian is approximate (close to mean if symmetric + not heavily clamped)
            switch (mode)
            {
                case Mode.Constant: return mean;
                case Mode.Uniform: return mean;
                case Mode.Triangular: return mean; // if mode == mean and symmetric bounds, expected == mean
                case Mode.GaussianClamped: return mean; // approx
                case Mode.BiasedUniform: return mean; // approx; if you care, we can compute exact E for chosen bias mapping
                default: return mean;
            }
        }

        public float GetRelativeDensity(float x)
        {
            float min = Min;
            float max = Max;

            // Handle degenerate range
            if (max <= min + 1e-6f)
            {
                // Constant-like spike around mean for preview purposes
                return Mathf.Abs(x - mean) < 1e-4f ? 1f : 0f;
            }

            // Outside support => zero
            if ((x < min) || (x > max))
                return 0f;

            switch (mode)
            {
                case Mode.Constant:
                    return Mathf.Abs(x - mean) < 1e-4f ? 1f : 0f;
                case Mode.Uniform:
                    return 1f; // flat (relative)
                case Mode.Triangular:
                    {
                        // Triangular with "peak" at mean (clamped to support)
                        float c = Mathf.Clamp(mean, min, max);
                        if (Mathf.Abs(max - min) < 1e-6f) return 1f;

                        if (x <= c)
                        {
                            float denom = (c - min);
                            if (denom < 1e-6f) return 0f;
                            return (x - min) / denom;
                        }
                        else
                        {
                            float denom = (max - c);
                            if (denom < 1e-6f) return 0f;
                            return (max - x) / denom;
                        }
                    }
                case Mode.GaussianClamped:
                    {
                        float sigma = Mathf.Max(1e-6f, range * gaussianSigmaFrac);
                        float z = (x - mean) / sigma;
                        // unnormalized normal density (relative)
                        return Mathf.Exp(-0.5f * z * z);
                    }
                case Mode.BiasedUniform:
                    {
                        // This matches your sampling mapping in SampleBiasedUniform:
                        // shaped = u^k, x = min + (max-min)*shaped
                        // pdf(x) = t^(1/k - 1)  where t=(x-min)/(max-min)
                        // This is ChatGPT made
                        float k = BiasToExponent(bias);
                        float t = (x - min) / (max - min);
                        t = Mathf.Clamp01(t);

                        // avoid inf at t=0 when exponent < 0
                        if (t <= 1e-6f) return (k > 1f) ? 1f : 0f;
                        if (t >= 1f - 1e-6f) return (k < 1f) ? 1f : 0f;

                        float a = (1f / k) - 1f;
                        return Mathf.Pow(t, a);
                    }
                default:
                    return 0f;
            }
        }

        public void GetPreviewDomain(out float xmin, out float xmax)
        {
            if (range > 1e-6f)
            {
                xmin = mean - range;
                xmax = mean + range;
            }
            else
            {
                xmin = mean - 1f;
                xmax = mean + 1f;
            }
        }

        // --- Sampling helpers ---

        static float BiasToExponent(float bias01)
        {
            const float kMin = 0.25f;
            const float kMax = 4.0f;
            float t = Mathf.Clamp01(bias01);

            return (t < 0.5f)
                ? Mathf.Lerp(1f, kMax, (0.5f - t) / 0.5f)
                : Mathf.Lerp(1f, kMin, (t - 0.5f) / 0.5f);
        }

        static float SampleTriangular(float min, float max, float mode, float u)
        {
            // Inverse CDF for triangular distribution
            float c = (mode - min) / (max - min);
            if (u < c)
                return min + Mathf.Sqrt(u * (max - min) * (mode - min));
            else
                return max - Mathf.Sqrt((1f - u) * (max - min) * (max - mode));
        }

        static float SampleGaussianClamped(float mean, float min, float max, float sigma, Func<float> next01)
        {
            // Box-Muller. We generate one normal sample and clamp.
            // If sigma == 0 (or tiny), it collapses to mean.
            if (sigma <= 1e-6f) return Mathf.Clamp(mean, min, max);

            float z = SampleStandardNormal(next01);
            float v = mean + z * sigma;
            return Mathf.Clamp(v, min, max);
        }

        static float SampleStandardNormal(Func<float> next01)
        {
            // Box-Muller transform
            // Ensure u1 not 0 to avoid log(0)
            float u1 = Mathf.Max(1e-7f, next01());
            float u2 = next01();

            float r = Mathf.Sqrt(-2f * Mathf.Log(u1));
            float theta = 2f * Mathf.PI * u2;
            return r * Mathf.Cos(theta);
        }

        static float SampleBiasedUniform(float min, float max, float bias01, float u)
        {
            // Simple, intuitive bias mapping:
            // Convert bias in [0,1] to an exponent k in [0.25, 4] around 1.
            // bias=0.5 => k=1 (uniform)
            // bias<0.5 => k>1 (push towards min)
            // bias>0.5 => k<1 (push towards max)
            // This is monotonic and easy to tune without extra params.
            const float kMin = 0.25f;
            const float kMax = 4.0f;

            float t = Mathf.Clamp01(bias01);
            float k = (t < 0.5f) ? Mathf.Lerp(1f, kMax, (0.5f - t) / 0.5f) : Mathf.Lerp(1f, kMin, (t - 0.5f) / 0.5f);

            float shaped = Mathf.Pow(u, k);
            return Mathf.Lerp(min, max, shaped);
        }

    }
}
