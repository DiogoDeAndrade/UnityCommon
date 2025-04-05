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
    }
}