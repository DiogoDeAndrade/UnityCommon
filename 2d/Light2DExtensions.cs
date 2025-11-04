using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UC
{

    public static class Light2DExtensions
    {
        public static Tweener.BaseInterpolator Flash(this Light2D light, float intensity, float duration, bool smooth)
        {
            light.Tween().Stop("LightFlash", Tweener.StopBehaviour.SkipToEnd);

            var current = light.intensity;

            if (smooth)
            {
                return light.Tween().Interpolate(0.0f, 1.0f, duration, (value) =>
                {
                    if (value < 0.5f) light.intensity = value * 2.0f * intensity;
                    else light.intensity = (1.0f - (value - 0.5f) * 2.0f) * intensity;
                }, "LightFlash").Done(() => light.intensity = current);
            }
            else
            {
                return light.Tween().Interpolate(0.0f, 1.0f, duration, (value) =>
                {
                    light.intensity = intensity;
                }, "LightFlash").Done(() => light.intensity = current);
            }
        }

        public static Tweener.BaseInterpolator FadeOut(this Light2D light, float duration)
        {
            return light.FadeTo(0.0f, duration);
        }

        public static Tweener.BaseInterpolator FadeTo(this Light2D light, float targetIntensity, float duration)
        {
            light.Tween().Stop("LightFade", Tweener.StopBehaviour.SkipToEnd);

            var current = light.intensity;

            if (current == targetIntensity) return null;

            return light.Tween().Interpolate(current, targetIntensity, duration, (value) => light.intensity = value, "LightFade");
        }

        public static void SetSortingLayerToEverything(this Light2D light)
        {
            FieldInfo sortingLayersField = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.NonPublic | BindingFlags.Instance);
            int defaultLayerID = SortingLayer.NameToID("Default");
            var layers = SortingLayer.layers;
            var allLayers = new List<int>();
            foreach (var layer in layers)
            {
                allLayers.Add(SortingLayer.NameToID(layer.name));
            }
            sortingLayersField.SetValue(light, allLayers.ToArray());
        }

        public static float GetLighting(this Light2D light, Vector2 worldPos)
        {
            if (light == null || !light.enabled)
                return 0f;

            if (light.lightCookieSprite != null)
                throw new System.NotImplementedException("Get lighting with lights with cookies not supported!");

            switch (light.lightType)
            {
                case Light2D.LightType.Point:
                    return ComputePointLighting(light, worldPos);

                case Light2D.LightType.Global:
                    return light.intensity;

                default:
                    return 0f;
            }
        }

        private static float ComputePointLighting(Light2D light, Vector2 worldPos)
        {
            Vector2 lightPos = light.transform.position;
            Vector2 dir = worldPos - lightPos;
            float dist = dir.magnitude;

            // --- Distance falloff ---
            float rOuter = Mathf.Max(0f, light.pointLightOuterRadius);
            if (rOuter <= 0.0001f)
                return 0f;

            float rInner = Mathf.Clamp(light.pointLightInnerRadius, 0f, rOuter);

            float distanceFactor;
            if (dist <= rInner) distanceFactor = 1f;
            else if (dist >= rOuter) distanceFactor = 0f;
            else
            {
                float t = Mathf.InverseLerp(rOuter, rInner, dist);
                distanceFactor = SmoothStep01(t);
            }

            // --- Angle falloff (if using cone) ---
            float angleFactor = 1f;
            if (light.pointLightOuterAngle < 360f)
            {
                Vector2 fwd = light.transform.up;
                float ang = Vector2.Angle(fwd, dir);
                float halfOuter = light.pointLightOuterAngle * 0.5f;
                float halfInner = Mathf.Min(light.pointLightInnerAngle * 0.5f, halfOuter);

                if (ang > halfOuter) angleFactor = 0f;
                else if (ang > halfInner)
                {
                    float t = Mathf.InverseLerp(halfOuter, halfInner, ang);
                    angleFactor = SmoothStep01(t);
                }
            }

            return light.intensity * distanceFactor * angleFactor;
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}