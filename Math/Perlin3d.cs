using UnityEngine;

using System;

public class PerlinNoise3D
{
    // Classic permutation (Ken Perlin). Duplicated to avoid overflow without &255.
    private readonly int[] P =
    {
        151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,
        140,36,103,30,69,142, 8,99,37,240,21,10,23,190, 6,148,
        247,120,234,75, 0,26,197, 62,94,252,219,203,117,35,11, 32,
        57,177,33, 88,237,149, 56,87,174,20,125,136,171,168, 68,175,
        74,165,71,134,139, 48, 27,166, 77,146,158,231, 83,111,229,122,
        60,211,133,230,220,105, 92,41,55, 46,245, 40,244,102,143, 54,
        65, 25, 63,161, 1,216, 80, 73,209, 76,132,187,208, 89, 18,169,
        200,196,135,130,116,188,159, 86,164,100,109,198,173,186, 3, 64,
        52,217,226,250,124,123, 5,202, 38,147,118,126,255, 82, 85,212,
        207,206, 59,227, 47,16, 58, 17,182,189, 28, 42,223,183,170,213,
        119,248,152, 2, 44,154,163, 70,221,153,101,155,167, 43,172, 9,
        129, 22, 39,253, 19, 98,108,110, 79,113,224,232,178,185,112,104,
        218,246,97,228,251, 34,242,193,238,210,144,12,191,179,162,241,
        81, 51,145,235,249, 14,239,107, 49,192,214, 31,181,199,106,157,
        184, 84,204,176,115,121, 50,45,127, 4,150,254,138,236,205, 93,
        222,114, 67, 29, 24, 72,243,141,128,195, 78, 66,215, 61,156,180
    };

    private readonly int[] perm = new int[512];

    private Quaternion R0 = Quaternion.Euler(23f, 47f, 13f);
    private Vector3 T0 = new(17.13f, 3.71f, 91.7f);
    private float S0 = 1.000f;

    public PerlinNoise3D()
    {
        // Build default permutation table (unseeded)
        for (int i = 0; i < 512; i++) perm[i] = P[i & 255];
    }
    public PerlinNoise3D(int seed)
    {
        Reseed(seed);
    }

    public void Reseed(int seed)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = P[i];
        var rng = new System.Random(seed);

        // Fisher–Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++) perm[i] = p[i & 255];

        R0 = Quaternion.Euler((float)rng.NextDouble() * 90.0f, (float)rng.NextDouble() * 90.0f, (float)rng.NextDouble() * 90.0f);
        T0 = new Vector3((float)rng.NextDouble() * 100.0f - 50.0f, (float)rng.NextDouble() * 100.0f - 50.0f, (float)rng.NextDouble() * 100.0f - 50.0f);
        S0 = (float)rng.NextDouble() * 0.02f + 0.99f;
    }

    public float Evaluate(Vector3 p)
    {
        return Evaluate(p.x, p.y, p.z);
    }

    // 3D Perlin noise in [0,1].
    public float Evaluate(float x, float y, float z)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), iz = Mathf.FloorToInt(z);
        int X = ix & 255, Y = iy & 255, Z = iz & 255;
        x -= ix; y -= iy; z -= iz;

        // Fade curves
        float u = Fade(x);
        float v = Fade(y);
        float w = Fade(z);

        // Hash cube corners
        int A = perm[X] + Y;
        int AA = perm[A] + Z;
        int AB = perm[A + 1] + Z;
        int B = perm[X + 1] + Y;
        int BA = perm[B] + Z;
        int BB = perm[B + 1] + Z;

        // Add blended results from 8 corners of cube
        float n =
        Mathf.Lerp(
            Mathf.Lerp(
                Mathf.Lerp(Grad(perm[AA], x, y, z),
                           Grad(perm[BA], x - 1, y, z), u),
                Mathf.Lerp(Grad(perm[AB], x, y - 1, z),
                           Grad(perm[BB], x - 1, y - 1, z), u),
                v),
            Mathf.Lerp(
                Mathf.Lerp(Grad(perm[AA + 1], x, y, z - 1),
                           Grad(perm[BA + 1], x - 1, y, z - 1), u),
                Mathf.Lerp(Grad(perm[AB + 1], x, y - 1, z - 1),
                           Grad(perm[BB + 1], x - 1, y - 1, z - 1), u),
                v),
            w);

        // Classic Perlin output is in [-1,1]. Normalize to [0,1].
        return 0.5f * (n + 1f);
    }

    // Fractal Brownian Motion (octave sum) of Perlin3D, returns [0,1].
    public float FBM(float x, float y, float z, int octaves = 5, float lacunarity = 2f, float gain = 0.5f)
    {
        float amp = 0.5f;      // start at 0.5 so sum stays in [0,1] with our normalized Perlin
        float freq = 1f;
        float sum = 0f;
        float totalAmp = 0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Evaluate(x * freq, y * freq, z * freq);
            totalAmp += amp;
            freq *= lacunarity;
            amp *= gain;
        }
        return (totalAmp > 0f) ? sum / totalAmp : 0f;
    }

    public float EvaluateExt(float x, float y, float z)
    {
        return EvaluateExt(new Vector3(x, y, z));
    }

    public float EvaluateExt(Vector3 p)
    {
        return Evaluate(R0 * (p * S0) + T0);
    }

    private float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = (h < 8) ? x : y;
        float v = (h < 4) ? y : (h == 12 || h == 14 ? x : z);
        float res = (((h & 1) == 0) ? u : -u) + (((h & 2) == 0) ? v : -v);
        return res;
    }
}
