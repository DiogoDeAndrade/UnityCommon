// Noise3D.hlsl

// Permutation table
static const int perm[256] = {
    151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
    190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,
    136,171,168,68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
    55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,200,196,135,130,116,
    188,159,86,164,100,109,198,173,186,3,64,52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,
    59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
    172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,228,251,34,242,193,238,210,
    144,12,191,179,162,241,81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,
    50,45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
};

// Fade function for smoothing
float Fade(float t)
{
    return t * t * t * (t * (t * 6 - 15) + 10);
}

// Linear interpolation
float Lerp(float a, float b, float t)
{
    return a + t * (b - a);
}
 
// Gradient function
float Grad3D(int hash, float x, float y, float z)
{
    int h = hash & 15;
    float u = h < 8 ? x : y;
    float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
    return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
}

// Gradient dot product
float Grad2D(int hash, float x, float y) 
{
    int h = hash & 7;
    float u = h < 4 ? x : y;
    float v = h < 4 ? y : x;
    return ((h & 1) ? -u : u) + ((h & 2) ? -v : v);
}

void PerlinNoise2D_float(float2 pos, out float result)
{
    // Grid cell coordinates
    int X = (int)floor(pos.x) & 255;
    int Y = (int)floor(pos.y) & 255;

    // Relative pos within cell
    float x = pos.x - floor(pos.x);
    float y = pos.y - floor(pos.y);

    // Fade curves
    float u = Fade(x);
    float v = Fade(y);

    // Hash coords
    int A  = perm[X] + Y;
    int B  = perm[X + 1] + Y;

    // Blend results from corners
    float res = lerp(
        lerp(Grad2D(perm[A], x, y), Grad2D(perm[B], x - 1, y), u),
        lerp(Grad2D(perm[A + 1], x, y - 1), Grad2D(perm[B + 1], x - 1, y - 1), u),
        v
    );

    // Normalize to [0,1]
    result = res * 0.5 + 0.5;
}

// 3D Noise function
void PerlinNoise3D_float(float3 p, out float ret)
{
    int X = (int)floor(p.x) & 255;
    int Y = (int)floor(p.y) & 255;
    int Z = (int)floor(p.z) & 255;

    float x = p.x - floor(p.x);
    float y = p.y - floor(p.y);
    float z = p.z - floor(p.z);

    float u = Fade(x);
    float v = Fade(y);
    float w = Fade(z);

    int A = perm[X] + Y;
    int AA = perm[A] + Z;
    int AB = perm[A + 1] + Z;
    int B = perm[X + 1] + Y;
    int BA = perm[B] + Z;
    int BB = perm[B + 1] + Z;

    ret = Lerp(
        Lerp(
            Lerp(Grad3D(perm[AA], x, y, z), Grad3D(perm[BA], x - 1, y, z), u),
            Lerp(Grad3D(perm[AB], x, y - 1, z), Grad3D(perm[BB], x - 1, y - 1, z), u),
            v
        ),
        Lerp(
            Lerp(Grad3D(perm[AA + 1], x, y, z - 1), Grad3D(perm[BA + 1], x - 1, y, z - 1), u),
            Lerp(Grad3D(perm[AB + 1], x, y - 1, z - 1), Grad3D(perm[BB + 1], x - 1, y - 1, z - 1), u),
            v
        ),
        w
    );
}

// Vector2d
void PerlinVector2D_float(float2 uv, out float2 ret)
{
    float angle = 0;
    PerlinNoise2D_float(uv, angle);

    angle = angle * 6.2831853;

    ret = float2(cos(angle), sin(angle));
}

void PerlinVector2D_Animated_float(float2 uv, float scale, float time, float2 speed, out float2 ret)
{
    float2 p = uv * scale + time * speed;
    float angle = 0;
    PerlinNoise2D_float(p, angle);

    angle = angle * 6.2831853;

    ret = float2(cos(angle), sin(angle));
}


// 3D Noise function
void FBM4_3D_float(float3 pos, float4 amplitude, float4 frequency, float4 offset, out float ret)
{
    float noise1, noise2, noise3, noise4;
    PerlinNoise3D_float(pos * frequency.x + offset.x, noise1);
    PerlinNoise3D_float(pos * frequency.y + offset.y, noise2);
    PerlinNoise3D_float(pos * frequency.z + offset.z, noise3);
    PerlinNoise3D_float(pos * frequency.w + offset.w, noise4);

    ret = amplitude.x * noise1;
    ret += amplitude.y * noise2;
    ret += amplitude.z * noise3;
    ret += amplitude.w * noise4;
}
