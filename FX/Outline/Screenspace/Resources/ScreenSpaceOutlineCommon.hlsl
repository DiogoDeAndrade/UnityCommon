#ifndef UNITY_COMMON_SCREEN_SPACE_OUTLINE_INCLUDED
#define UNITY_COMMON_SCREEN_SPACE_OUTLINE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Scene depth under a fragment, in eye units (distance in front of the camera).
//
// Goes through URP's own SampleSceneDepth + GetNormalizedScreenSpaceUV rather than loading by absolute
// pixel, because the depth texture's dimensions are NOT the rendered region's: URP allocates these as
// RTHandles sized to the largest viewport seen and reuses them for smaller ones, so a texture can be bigger
// than the area actually being drawn. Indexing it by pixel (or rescaling by hand, as this used to) reads the
// wrong texel the moment the two sizes diverge - which is what a game-view resize does, and it showed up as
// an outline traced from a mis-scaled silhouette with holes punched through its interior.
//
// GetNormalizedScreenSpaceUV also applies TransformNormalizedScreenUV, so the render-target y-flip is
// handled too.
float OutlineSceneEyeDepth(float2 positionCS)
{
    float raw = SampleSceneDepth(GetNormalizedScreenSpaceUV(positionCS));

    if (unity_OrthoParams.w > 0.5)
    {
        // Orthographic depth is already linear across [near, far].
        #if UNITY_REVERSED_Z
            raw = 1.0 - raw;
        #endif
        return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
    }

    return LinearEyeDepth(raw, _ZBufferParams);
}

// How many pixels one world unit covers at a given distance from the camera. Lets a width authored in world
// units become the pixel width the mask stores, so the resolve never has to know which mode it is in.
float OutlinePixelsPerWorldUnit(float eyeDepth)
{
    // Vertical projection scale: 1/tan(fov/2) perspective, 1/orthoSize orthographic.
    float pixels = 0.5 * _ScaledScreenParams.y * abs(UNITY_MATRIX_P._m11);

    // Orthographic size doesn't fall off with distance.
    if (unity_OrthoParams.w > 0.5) return pixels;

    return pixels / max(eyeDepth, 1e-4);
}

#endif
