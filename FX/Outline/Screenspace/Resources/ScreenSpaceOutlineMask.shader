// Silhouette mask for the screen space outline (see ScreenSpaceOutlineFeature).
//
// One shared material draws every outlined object, so the per-object parameters are GLOBALS set between draws
// rather than material properties - CommandBuffer.DrawRenderer takes no property block. What lands in the mask:
//
//   RGB = the object's outline color
//   A   = the object's width in PIXELS / the feature's Max Width  (A > 0 is what "covered" means)
//
// The width is normalized here, in the fragment shader, which is what lets a width authored in world units
// become a pixel width that varies with distance while the resolve pass stays completely unaware of the mode.
//
// The material properties (_BaseMap, _Cutoff) are only used by the alpha-clip variant: when the feature has
// Alpha Clip on, it hands alpha-clipped renderers their own material copy carrying their source texture,
// since a shared material can't vary per draw.
Shader "Unity Common/Effects/Screen Space Outline Mask"
{
    Properties
    {
        [MainTexture] _BaseMap ("Alpha Clip Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineMask"

            // The occlusion test is done in the fragment shader against the depth TEXTURE, not with a depth
            // attachment: attachments would have to match the camera's MSAA sample count, and doing it here
            // also keeps "visible only" a per-object value instead of pipeline state.
            // Cull Off: single-sided geometry still has to produce a silhouette.
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // multi_compile rather than shader_feature: the variant materials are created at runtime, and
            // shader_feature keywords they enable can be stripped from a build.
            #pragma multi_compile _ _OUTLINE_ALPHATEST

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ScreenSpaceOutlineCommon.hlsl"

            float4  _OutlineColor;
            // x = width (pixels, or world units in world mode), y = 1 when the depth test is on,
            // z = 1 in world-space width mode, w = the feature's Max Width in pixels
            float4  _OutlineParams;
            float   _OutlineDepthBias;

            #if defined(_OUTLINE_ALPHATEST)
                TEXTURE2D(_BaseMap);
                SAMPLER(sampler_BaseMap);

                CBUFFER_START(UnityPerMaterial)
                    float4 _BaseMap_ST;
                    float  _Cutoff;
                CBUFFER_END
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  eyeDepth   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                // View space looks down -z, so this is distance in front of the camera.
                OUT.eyeDepth = -positionVS.z;

                #if defined(_OUTLINE_ALPHATEST)
                    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                #else
                    OUT.uv = IN.uv;
                #endif

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                #if defined(_OUTLINE_ALPHATEST)
                    // The silhouette follows the texture, not the geometry - a fence outlines its bars
                    // instead of its quad.
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                    if (alpha < _Cutoff) discard;
                #endif

                if (_OutlineParams.y > 0.5)
                {
                    // Behind the scene means this part of the object is hidden, so it contributes no
                    // silhouette. The bias absorbs the exact-equality case: for a visible opaque object the
                    // mask rasterizes the very same triangles that wrote the depth buffer.
                    if (IN.eyeDepth > OutlineSceneEyeDepth(IN.positionCS.xy) + _OutlineDepthBias) discard;
                }

                float width = _OutlineParams.x;
                if (_OutlineParams.z > 0.5) width *= OutlinePixelsPerWorldUnit(IN.eyeDepth);

                // Clamped by Max Width because that is what bounds the resolve kernel - a width the kernel
                // can't reach would just be a lie.
                return half4(_OutlineColor.rgb, saturate(width / max(_OutlineParams.w, 1e-4)));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
