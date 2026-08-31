// ExtSimplexNoise3D.hlsl

static const float3 grad3[12] = {
    float3(1,1,0), float3(-1,1,0), float3(1,-1,0), float3(-1,-1,0),
    float3(1,0,1), float3(-1,0,1), float3(1,0,-1), float3(-1,0,-1),
    float3(0,1,1), float3(0,-1,1), float3(0,1,-1), float3(0,-1,-1)
};

static const int simplexPerm[512] = {
    151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
    190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,
    136,171,168,68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
    55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,200,196,135,130,116,
    188,159,86,164,100,109,198,173,186,3,64,52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,
    59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
    172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,228,251,34,242,193,238,210,
    144,12,191,179,162,241,81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,
    50,45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
    151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
    190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,125,
    136,171,168,68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
    55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,200,196,135,130,116,
    188,159,86,164,100,109,198,173,186,3,64,52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,
    59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
    172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,228,251,34,242,193,238,210,
    144,12,191,179,162,241,81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,
    50,45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
};

float Dot3(float3 g, float3 p)
{
    return dot(g, p);
}

void ExtSimplexNoise3D_float(float3 p, out float ret)
{
    const float F3 = 1.0 / 3.0;
    const float G3 = 1.0 / 6.0;

    float s = (p.x + p.y + p.z) * F3;
    int i = (int)floor(p.x + s);
    int j = (int)floor(p.y + s);
    int k = (int)floor(p.z + s);

    float t = (i + j + k) * G3;
    float3 cellOrigin = float3(i - t, j - t, k - t);
    float3 x0 = p - cellOrigin;

    int i1, j1, k1;
    int i2, j2, k2;

    if (x0.x >= x0.y)
    {
        if (x0.y >= x0.z) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
        else if (x0.x >= x0.z) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
        else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
    }
    else
    {
        if (x0.y < x0.z) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
        else if (x0.x < x0.z) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
        else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
    }

    float3 x1 = x0 - float3(i1, j1, k1) + G3;
    float3 x2 = x0 - float3(i2, j2, k2) + 2.0 * G3;
    float3 x3 = x0 - 1.0 + 3.0 * G3;

    // IMPORTANT: wrap indices
    int ii = i & 255;
    int jj = j & 255;
    int kk = k & 255;

    int gi0 = simplexPerm[ii + simplexPerm[jj + simplexPerm[kk]]] % 12;
    int gi1 = simplexPerm[ii + i1 + simplexPerm[jj + j1 + simplexPerm[kk + k1]]] % 12;
    int gi2 = simplexPerm[ii + i2 + simplexPerm[jj + j2 + simplexPerm[kk + k2]]] % 12;
    int gi3 = simplexPerm[ii + 1 + simplexPerm[jj + 1 + simplexPerm[kk + 1]]] % 12;

    float n0, n1, n2, n3;

    float t0 = 0.5 - dot(x0, x0);
    if (t0 < 0.0) n0 = 0.0;
    else
    {
        t0 *= t0;
        n0 = t0 * t0 * Dot3(grad3[gi0], x0);
    }

    float t1 = 0.5 - dot(x1, x1);
    if (t1 < 0.0) n1 = 0.0;
    else
    {
        t1 *= t1;
        n1 = t1 * t1 * Dot3(grad3[gi1], x1);
    }

    float t2 = 0.5 - dot(x2, x2);
    if (t2 < 0.0) n2 = 0.0;
    else
    {
        t2 *= t2;
        n2 = t2 * t2 * Dot3(grad3[gi2], x2);
    }

    float t3 = 0.5 - dot(x3, x3);
    if (t3 < 0.0) n3 = 0.0;
    else
    {
        t3 *= t3;
        n3 = t3 * t3 * Dot3(grad3[gi3], x3);
    }

    // Standard approximate normalization factor for 3D simplex
    // The 76 here is the empirically/analytically derived factor that stretches the output to the full [-1, 1] range for 3D simplex noise
    // Normally people use 32, per the original Gustavson implementation, but that leads to a 0.25-0.75 range after normalization.
    float retSigned = 76.0 * (n0 + n1 + n2 + n3);

    // Optional safety clamp if you want a hard bounded output
    retSigned = clamp(retSigned, -1.0, 1.0);

    ret = retSigned * 0.5 + 0.5;
}
