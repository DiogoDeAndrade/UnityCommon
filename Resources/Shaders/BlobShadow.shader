Shader "Unity Common/Blob Shadow"
{
    Properties
    {
        _MainTex ("Blob Texture", 2D) = "white" {}
        _ShadowIntensity ("Shadow Intensity", float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-1" }
        LOD 100

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode"="UniversalForward" }

            Blend DstColor Zero     // Multiply blending
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            }; 

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ShadowIntensity;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half alphaTex = 1 - tex2D(_MainTex, IN.uv).r * _ShadowIntensity;
                return half4(alphaTex, alphaTex, alphaTex, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Unlit/Transparent"
}
