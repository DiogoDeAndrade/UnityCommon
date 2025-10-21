Shader "Unity Common/Effects/Outline (Backface Extrude)"
{
    Properties
    {
        [KeywordEnum(UV0, UV1, UV2, UV3, UV4, UV5, UV6, UV7, Normal)]
        _DirSource ("Direction Source", Float) = 8
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Float) = 0.02
        _Mode ("Space (0=World,1=View)", Float) = 0
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _Mode;          // 0=World, 1=View
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

            struct Varyings { float4 positionCS : SV_POSITION; };

            // Transform like a normal (handles non-uniform scale)
            float3 TransformObjectToWorldDirSafe(float3 dirOS)
            {
                return normalize(mul((float3x3)UNITY_MATRIX_I_M, dirOS));
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                float3 dirOS = GetDirectionOS(IN);
                if (dot(dirOS, dirOS) < 1e-8) 
                    dirOS = IN.normalOS; // fallback
                float3 dirWS = TransformObjectToWorldDirSafe(dirOS);

                float3 extrudeWS;

                if (_Mode < 0.5) // WORLD: fixed world-units thickness
                {
                    extrudeWS = dirWS * _OutlineWidth;
                }
                else             // VIEW: approx screen-constant thickness
                {
                    float3 posVS = TransformWorldToView(posWS);
                    float3 dirVS = mul((float3x3)UNITY_MATRIX_V, dirWS);
                    float z      = max(1e-4, -posVS.z);
                    float scale  = _OutlineWidth / z;
                    float3 offVS = normalize(dirVS) * scale;
                    extrudeWS    = mul((float3x3)UNITY_MATRIX_I_V, offVS);
                }

                float3 outWS = posWS + extrudeWS;
                OUT.positionCS = TransformWorldToHClip(outWS);
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
