using System;
using UnityEngine;

public static class MathUtils
{
    public static float SampleGaussian(float mean, float sigma)
    {
        return SampleGaussian(mean, sigma, () => UnityEngine.Random.value);
    }

    public static float SampleGaussian(float mean, float sigma, Func<float> next01)
    {
        // Box-Muller. We generate one normal sample and clamp.
        // If sigma == 0 (or tiny), it collapses to mean.
        if (sigma <= 1e-6f) return mean;

        float z = SampleStandardNormal(next01);
        float v = mean + z * sigma;
        return v;
    }

    public static float SampleGaussianClamped(float mean, float min, float max, float sigma, Func<float> next01)
    {
        return Mathf.Clamp(SampleGaussian(mean, sigma, next01), min, max);
    }

    public static float SampleStandardNormal(Func<float> next01)
    {
        // Box-Muller transform
        // Ensure u1 not 0 to avoid log(0)
        float u1 = Mathf.Max(1e-7f, next01());
        float u2 = next01();

        float r = Mathf.Sqrt(-2f * Mathf.Log(u1));
        float theta = 2f * Mathf.PI * u2;
        return r * Mathf.Cos(theta);
    }

    public static float SampleBiasedUniform(float min, float max, float bias01, float u)
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
