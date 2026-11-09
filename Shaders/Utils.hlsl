#ifndef UC_UTILS_INCLUDED
#define UC_UTILS_INCLUDED

// General shader helpers for hand-written HLSL: simplex noise in 1, 2 and 3 dimensions, fractal
// sums of each, and domain perturbation. Self-contained (no textures, no lookup tables, no includes),
// so it can go into any pass, and every helper is prefixed UC_ so it can share a shader with the
// Shader Graph noise files in Shaders/Subgraphs, which declare perm, grad3, Fade, Lerp and friends
// globally.
//
// Every noise and FBM function here returns roughly -1..1, centred on 0. Remap with n * 0.5 + 0.5
// where 0..1 is wanted.
//
// The hashing is the mod 289 permutation polynomial from Ashima Arts / Stefan Gustavson's webgl-noise
// (MIT), and the 2D and 3D simplex follow that implementation; the 1D one follows Gustavson's
// reference simplex. Precision is good while the coordinates stay within a few thousand units of the
// origin - past that, scale them down before sampling rather than feeding raw world positions in.

//------------------------------------------------------------------------------------------------------
// Hashing
//------------------------------------------------------------------------------------------------------

float  UC_Mod289(float x)  { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float2 UC_Mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float3 UC_Mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 UC_Mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }

// A permutation of 0..288 for integer inputs in that range - exact in float, since the largest
// intermediate stays well under 2^24
float  UC_Permute(float x)  { return UC_Mod289(((x * 34.0) + 10.0) * x); }
float2 UC_Permute(float2 x) { return UC_Mod289(((x * 34.0) + 10.0) * x); }
float3 UC_Permute(float3 x) { return UC_Mod289(((x * 34.0) + 10.0) * x); }
float4 UC_Permute(float4 x) { return UC_Mod289(((x * 34.0) + 10.0) * x); }

float3 UC_TaylorInvSqrt(float3 r) { return 1.79284291400159 - 0.85373472095314 * r; }
float4 UC_TaylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

//------------------------------------------------------------------------------------------------------
// Simplex noise
//------------------------------------------------------------------------------------------------------

// 1D: two lattice points, each with a gradient of 1..8 and a random sign, under a (1 - x^2)^4 kernel.
// 0.395 is the factor that brings the peak (8 * (3/4)^4 = 2.53) to 1
float SimplexNoise1D(float x)
{
    float i0 = floor(x);
    float x0 = x - i0;
    float x1 = x0 - 1.0;

    float2 h = fmod(UC_Permute(UC_Mod289(float2(i0, i0 + 1.0))), 16.0);
    float2 g = (1.0 + fmod(h, 8.0)) * (1.0 - 2.0 * step(8.0, h));

    float2 t = 1.0 - float2(x0 * x0, x1 * x1);
    t *= t;
    t *= t;

    return 0.395 * (t.x * g.x * x0 + t.y * g.y * x1);
}

float SimplexNoise2D(float2 v)
{
    const float4 C = float4(0.211324865405187,     // (3 - sqrt(3)) / 6
                            0.366025403784439,     // (sqrt(3) - 1) / 2
                           -0.577350269189626,     // -1 + 2 * C.x
                            0.024390243902439);    // 1 / 41

    // Which simplex (triangle) the point is in, and its offsets from the three corners
    float2 i  = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);

    float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;

    // Hash the corners
    i = UC_Mod289(i);
    float3 p = UC_Permute(UC_Permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));

    float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;

    // Gradients: 41 points spread over a line, mapped onto a diamond and rotated
    float3 x  = 2.0 * frac(p * C.www) - 1.0;
    float3 h  = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;

    // Normalise the gradients implicitly by scaling m
    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    float3 g;
    g.x  = a0.x  * x0.x   + h.x  * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;

    return 130.0 * dot(m, g);
}

float SimplexNoise3D(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
    const float4 D = float4(0.0, 0.5, 1.0, 2.0);

    // Which simplex (tetrahedron) the point is in
    float3 i  = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    float3 g  = step(x0.yzx, x0.xyz);
    float3 l  = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - D.yyy;

    // Hash the corners
    i = UC_Mod289(i);
    float4 p = UC_Permute(UC_Permute(UC_Permute(
                   i.z + float4(0.0, i1.z, i2.z, 1.0))
                 + i.y + float4(0.0, i1.y, i2.y, 1.0))
                 + i.x + float4(0.0, i1.x, i2.x, 1.0));

    // Gradients: 7x7 points over a square, mapped onto an octahedron
    float  n_ = 0.142857142857;     // 1 / 7
    float3 ns = n_ * D.wyz - D.xzx;

    float4 j  = p - 49.0 * floor(p * ns.z * ns.z);

    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_);

    float4 x  = x_ * ns.x + ns.yyyy;
    float4 y  = y_ * ns.x + ns.yyyy;
    float4 h  = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, 0.0);

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 p0 = float3(a0.xy, h.x);
    float3 p1 = float3(a0.zw, h.y);
    float3 p2 = float3(a1.xy, h.z);
    float3 p3 = float3(a1.zw, h.w);

    float4 norm = UC_TaylorInvSqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;

    // Mix the corner contributions
    float4 m = max(0.5 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;

    return 105.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
}

//------------------------------------------------------------------------------------------------------
// Fractal Brownian motion
//
// Sums octaves of simplex noise: each octave's frequency is multiplied by lacunarity (2 is the usual
// value) and its amplitude by amplitudeGain (0.5 is the usual value - lower is smoother, higher is
// rougher). The sum is divided by the total amplitude, so the result stays in -1..1 whatever the
// octave count. Each octave is also shifted by a fixed offset, so the octaves don't all line up on the
// origin, where every one of them is zero.
//------------------------------------------------------------------------------------------------------

float FBM1D(float x, int octaves, float lacunarity, float amplitudeGain)
{
    float sum = 0.0;
    float amplitude = 1.0;
    float total = 0.0;

    for (int octave = 0; octave < octaves; octave++)
    {
        sum += amplitude * SimplexNoise1D(x);
        total += amplitude;

        x = x * lacunarity + 19.19;
        amplitude *= amplitudeGain;
    }

    return (total > 0.0) ? (sum / total) : (0.0);
}

float FBM2D(float2 p, int octaves, float lacunarity, float amplitudeGain)
{
    float sum = 0.0;
    float amplitude = 1.0;
    float total = 0.0;

    for (int octave = 0; octave < octaves; octave++)
    {
        sum += amplitude * SimplexNoise2D(p);
        total += amplitude;

        p = p * lacunarity + float2(19.19, 7.31);
        amplitude *= amplitudeGain;
    }

    return (total > 0.0) ? (sum / total) : (0.0);
}

float FBM3D(float3 p, int octaves, float lacunarity, float amplitudeGain)
{
    float sum = 0.0;
    float amplitude = 1.0;
    float total = 0.0;

    for (int octave = 0; octave < octaves; octave++)
    {
        sum += amplitude * SimplexNoise3D(p);
        total += amplitude;

        p = p * lacunarity + float3(19.19, 7.31, 13.73);
        amplitude *= amplitudeGain;
    }

    return (total > 0.0) ? (sum / total) : (0.0);
}

//------------------------------------------------------------------------------------------------------
// Perturbation
//------------------------------------------------------------------------------------------------------

// Pushes p around by noise: each axis moves by up to its component of displacement, driven by 2D
// simplex sampled at p * noiseScale. The two axes take independent samples (the second far off along
// a fixed offset), so the push is a direction, not a slide along the diagonal. Smaller noiseScale
// gives broader, slower warps; animate by adding time to p before the call or scrolling the result.
float2 Perturb2D(float2 p, float2 displacement, float noiseScale)
{
    float2 q = p * noiseScale;
    float2 n = float2(SimplexNoise2D(q), SimplexNoise2D(q + float2(31.41, 47.85)));

    return p + n * displacement;
}

#endif // UC_UTILS_INCLUDED
