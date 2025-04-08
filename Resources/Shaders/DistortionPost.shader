Shader "Unity Common/DistortionPost"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Float) = 0.02
    }

    HLSLINCLUDE
    #pragma target 4.5
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BlitTexture);
    SAMPLER(sampler_BlitTexture);
    
    TEXTURE2D(_DistortionMap);
    SAMPLER(sampler_DistortionMap);

    float4 _MainTex_TexelSize;
    float _DistortionStrength;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
    };

    struct Varyings
    {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;

        // Fullscreen triangle without vertex buffer
        float2 positions[3] = {
            float2(-1.0, -1.0),
            float2( 3.0, -1.0),
            float2(-1.0,  3.0)
        };

        float2 uvs[3] = {
            float2(0.0, 0.0),
            float2(2.0, 0.0),
            float2(0.0, 2.0)
        };

        output.positionHCS = float4(positions[input.vertexID], 0.0, 1.0);
        output.uv = uvs[input.vertexID];
        return output;
    }

    float4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        uv.y = 1.0 - uv.y;

        float3 distortion = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, uv).rgb;
        // Something happens to the texture that it seems that it is written as linear, but then read as sRGB, regardless of the configuration
        // This forces me to re-do sRGB conversion, which makes no sense
        distortion = pow(distortion, 1 / 2.2); 
        float2 offset = (distortion.xy - 0.5) * _DistortionStrength;

        float4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + offset);
        return color;
    }
    ENDHLSL

   SubShader
    {
        Tags { "RenderPipeline" = "UniversalRenderPipeline" }

        Pass
        {
            Name "DistortionPost"
            Tags { "LightMode" = "FullScreenPass" }

            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    FallBack Off
}
