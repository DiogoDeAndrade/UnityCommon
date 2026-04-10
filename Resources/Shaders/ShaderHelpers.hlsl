#ifndef SHADER_HELPERS_INCLUDED
#define SHADER_HELPERS_INCLUDED

half4 ComputeOutline(TEXTURE2D_PARAM(tex, texSampler), float2 uv, float2 texelSize, float width, half4 outlineColor, out half centerAlpha)
{
    centerAlpha = SAMPLE_TEXTURE2D(tex, texSampler, uv).a;

    if (width == 0)
        return half4(0, 0, 0, 0);

    if (centerAlpha > 0.9)
        return half4(0, 0, 0, 0);

    float2 offsets[4] = {
        float2( width * texelSize.x,  0),
        float2(-width * texelSize.x,  0),
        float2(0,  width * texelSize.y),
        float2(0, -width * texelSize.y)
    };

    int count = 0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        half a = SAMPLE_TEXTURE2D(tex, texSampler, uv + offsets[i]).a;
        count += (a > 0.01) ? 1 : 0;
    }

    if (count == 0)
        return half4(0, 0, 0, 0);

    half alpha = (count >= 2) ? 1.0 : 0.5;
    return half4(outlineColor.rgb, outlineColor.a * alpha);
}

#endif // SHADER_HELPERS_INCLUDED
