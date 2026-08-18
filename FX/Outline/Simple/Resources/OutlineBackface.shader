Shader "Unity Common/Effects/Outline (Backface Extrude)"
{
    Properties
    {
        [KeywordEnum(UV0, UV1, UV2, UV3, UV4, UV5, UV6, UV7, Normal)]
        _DirSource ("Direction Source", Float) = 8

        [KeywordEnum(Object, World, Clip)]
        _ExtrudeSpace ("Extrude Space", Float) = 1

        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Float) = 0.02
    }

    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Cull Front
        ZWrite Off
        ZTest  LEqual
        Blend  One Zero  // solid color

        Pass
        {
            Name "Outline"
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

             #pragma shader_feature_local _DIRSOURCE_UV0 _DIRSOURCE_UV1 _DIRSOURCE_UV2 _DIRSOURCE_UV3 _DIRSOURCE_UV4 _DIRSOURCE_UV5 _DIRSOURCE_UV6 _DIRSOURCE_UV7 _DIRSOURCE_NORMAL
             #pragma shader_feature_local _EXTRUDESPACE_OBJECT _EXTRUDESPACE_WORLD _EXTRUDESPACE_CLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                #if defined(_DIRSOURCE_UV0)
                float4 uv : TEXCOORD0;
                #elif defined(_DIRSOURCE_UV1)
                float4 uv : TEXCOORD1;
                #elif defined(_DIRSOURCE_UV2)
                float4 uv : TEXCOORD2;
                #elif defined(_DIRSOURCE_UV3)
                float4 uv : TEXCOORD3;
                #elif defined(_DIRSOURCE_UV4)
                float4 uv : TEXCOORD4;
                #elif defined(_DIRSOURCE_UV5)
                float4 uv : TEXCOORD5;
                #elif defined(_DIRSOURCE_UV6)
                float4 uv : TEXCOORD6;
                #elif defined(_DIRSOURCE_UV7)
                float4 uv : TEXCOORD7;
                #endif
            };

            float3 GetDirectionOS(Attributes IN)
            {
                #if defined(_DIRSOURCE_NORMAL)
                    return IN.normalOS;
                #else
                    return IN.uv.xyz;
                #endif
            }

            struct Varyings 
            { 
                float4 positionCS : SV_POSITION; 
            };

            // Transform like a normal (handles non-uniform scale)
            float3 TransformObjectToWorldDirSafe(float3 dirOS)
            {
                return normalize(mul((float3x3)UNITY_MATRIX_I_M, dirOS));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 dirOS = GetDirectionOS(IN);
                if (dot(dirOS, dirOS) < 1e-8) dirOS = IN.normalOS;

                #if defined(_EXTRUDESPACE_OBJECT)
                    // Extrude directly in OBJECT space by _OutlineWidth units
                    float3 posOS = IN.positionOS.xyz;
                    float3 outOS = posOS + normalize(dirOS) * _OutlineWidth;
                    OUT.positionCS = TransformObjectToHClip(outOS);

                #elif defined(_EXTRUDESPACE_WORLD)
                    // Extrude in WORLD space by _OutlineWidth units
                    float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                    float3 dirWS = TransformObjectToWorldDirSafe(dirOS);
                    float3 outWS = posWS + dirWS * _OutlineWidth;
                    OUT.positionCS = TransformWorldToHClip(outWS);

                #else /* _EXTRUDESPACE_CLIP */
                    // Project position, compute a screen-space direction from a small step along dir,
                    // then offset in NDC by a fixed pixel amount.
                    float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                    float3 dirWS = TransformObjectToWorldDirSafe(dirOS);

                    // 1) Project base and stepped point
                    float4 P0 = TransformWorldToHClip(posWS);                     // clip
                    float4 P1 = TransformWorldToHClip(posWS + dirWS);             // clip along direction

                    // 2) Convert to NDC (xy / w)
                    float2 ndc0 = P0.xy / max(P0.w, 1e-6);
                    float2 ndc1 = P1.xy / max(P1.w, 1e-6);

                    // 3) Screen direction in NDC
                    float2 dirNDC = ndc1 - ndc0;
                    float len2 = max(dot(dirNDC, dirNDC), 1e-10);
                    dirNDC *= rsqrt(len2);                                        // normalize; falls back if tiny

                    // 4) Pixels -> NDC scale: 2 / resolution
                    float2 pxToNDC = 2.0 / _ScreenParams.xy;
                    
                    float2 ndcOffset = dirNDC * (_OutlineWidth * pxToNDC);
                    float2 clipOffset = ndcOffset * P0.w;

                    float4 Pout = P0;
                    Pout.xy += clipOffset;

                    OUT.positionCS = Pout;
                #endif

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return (half4)_OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
