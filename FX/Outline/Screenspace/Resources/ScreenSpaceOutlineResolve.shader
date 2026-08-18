// Turns the silhouette mask into outlines and blends them over the scene (see ScreenSpaceOutlineFeature).
//
// For every pixel NOT covered by the mask, find the nearest covered pixel whose own stored width reaches this
// far, and take its color. That per-tap width test is what lets one kernel draw outlines of different
// thicknesses at once, and reading the color from the winning tap does the same for color.
//
// Cost is (2*radius+1)^2 loads of an 8-bit texture per uncovered pixel, which is why the feature caps the
// radius with Max Width. If it ever shows up in a profile, the next steps are a viewport restricted to the
// screen-space bounds of the outlined objects, or a jump-flood distance field instead of the square kernel.
Shader "Unity Common/Effects/Screen Space Outline Resolve"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "OutlineResolve"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ScreenSpaceOutlineCommon.hlsl"

            TEXTURE2D(_OutlineMask);

            // x = max width (pixels), y = kernel radius (pixels), z = depth bias, w = 1 to hide the outline
            // behind nearer geometry
            float4 _OutlineResolveParams;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                // Fullscreen triangle, no vertex buffer. The fragment shader works off SV_POSITION rather
                // than an interpolated uv, so mask pixels line up exactly with target pixels.
                float2 positions[3] =
                {
                    float2(-1.0, -1.0),
                    float2( 3.0, -1.0),
                    float2(-1.0,  3.0)
                };

                OUT.positionCS = float4(positions[IN.vertexID], 0.0, 1.0);

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                int2 pixel = int2(IN.positionCS.xy);

                // Inside the silhouette is not outline: the ring is drawn strictly outside it. This also
                // means the whole interior of every outlined object costs one load and nothing else - the
                // return is what guarantees that, since discard on its own is allowed to keep executing.
                if (LOAD_TEXTURE2D(_OutlineMask, pixel).a > 0.0)
                {
                    discard;
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                float   maxWidth    = _OutlineResolveParams.x;
                int     radius      = (int)_OutlineResolveParams.y;

                float   bestDistSq  = 1e9;
                float3  bestColor   = float3(0.0, 0.0, 0.0);
                float   bestWidth   = 0.0;
                int2    bestOffset  = int2(0, 0);

                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        // Loads outside the texture return 0, which reads as "not covered" - no clamping needed.
                        float4 tap = LOAD_TEXTURE2D(_OutlineMask, pixel + int2(x, y));
                        if (tap.a <= 0.0) continue;

                        float width  = tap.a * maxWidth;
                        float distSq = float(x * x + y * y);

                        // A tap only owns this pixel if its own width stretches this far.
                        if (distSq > (width + 0.5) * (width + 0.5)) continue;
                        if (distSq >= bestDistSq) continue;

                        bestDistSq = distSq;
                        bestColor  = tap.rgb;
                        bestWidth  = width;
                        bestOffset = int2(x, y);
                    }
                }

                if (bestDistSq > 1e8)
                {
                    discard;
                    return half4(0.0, 0.0, 0.0, 0.0);
                }

                // The ring lands on pixels that belong to OTHER geometry, and it has no depth of its own, so
                // without this it paints over things standing in front of the outlined object. Comparing the
                // scene here against the scene at the pixel the ring came from is what gives it depth - and it
                // needs no per-object mode, because a hidden object's own pixel already reads its occluder's
                // depth, which keeps its ring visible across that same occluder.
                if (_OutlineResolveParams.w > 0.5)
                {
                    float here = OutlineSceneEyeDepth(IN.positionCS.xy);
                    float seed = OutlineSceneEyeDepth(IN.positionCS.xy + float2(bestOffset));

                    if (here < seed - _OutlineResolveParams.z)
                    {
                        discard;
                        return half4(0.0, 0.0, 0.0, 0.0);
                    }
                }

                // One pixel of feather at the outer edge, so the ring is antialiased.
                float alpha = saturate(bestWidth + 0.5 - sqrt(bestDistSq));

                return half4(bestColor, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
