using UnityEngine;

namespace UC
{

    public static class LightExtensions
    {
        public static Tweener.BaseInterpolator FadeTo(this Light light, Color endColor, float duration)
        {
            light.Tween().Stop("FadeColorTo", Tweener.StopBehaviour.SkipToEnd);

            var current = light.color;

            return light.Tween().Interpolate(current, endColor, duration, (value) =>
            {
                light.color = value;
            }, "FadeColorTo").Done(() => light.color = endColor);
        }
    }
}