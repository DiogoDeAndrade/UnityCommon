Shader "Unity Common/Effects/Outline (Fresnel)"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Threshold    ("Rim Threshold", Range(0,1)) = 0.6
        _Width        ("Rim Band Width", Range(0,1)) = 0.15
        _Softness     ("Band Softness", Range(0,1)) = 0.05
        _Power        ("Rim Power (falloff)", Float) = 2.0
    }

    SubShader
    {
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Cull Back
        ZWrite Off
        ZTest  LEqual
        Blend  SrcAlpha OneMinusSrcAlpha

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
                float  _Threshold;
                float  _Width;
                float  _Softness;
                float  _Power;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 posWS   = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalW = TransformObjectToWorldNormal(IN.normalOS);

                OUT.positionWS = posWS;
                OUT.normalWS   = normalW;
                OUT.positionCS = TransformWorldToHClip(posWS);
                
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // World-space normal/view
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);

                // Fresnel rim: 1 - dot(N,V), shaped by _Power
                half rim = pow(saturate(1.0h - dot(N, V)), _Power);

                // Build a thin band around _Threshold, of width _Width, softened by _Softness
                // Band edges with smoothstep:
                half innerEdge = smoothstep(_Threshold - _Width - _Softness, _Threshold - _Width, rim);
                half outerEdge = 1.0h - smoothstep(_Threshold + _Width, _Threshold + _Width + _Softness, rim);
                half band = saturate(innerEdge * outerEdge);

                // Color with alpha controlled by band; keep RGB from _OutlineColor
                half4 col = _OutlineColor;
                col.a *= band;

                // If you want a hard, single-line look, you can also set col.rgb *= step(_Threshold, rim);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
