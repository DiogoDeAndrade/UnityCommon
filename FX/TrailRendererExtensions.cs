using UnityEngine;

namespace UC
{

    public static class TrailRendererExtensions
    {
        public static Tweener.BaseInterpolator FadeTo(this TrailRenderer trailRenderer, float targetAlphaStartPoint, float targetAlphaEndPoint, float time, string name = null)
        {
            var startColor = trailRenderer.startColor;
            var endColor = trailRenderer.endColor;

            return trailRenderer.Tween().Interpolate(0.0f, 1.0f, time, (currentValue) =>
            {
                if (trailRenderer)
                {
                    trailRenderer.startColor = Color.Lerp(startColor, startColor.ChangeAlpha(targetAlphaStartPoint), currentValue);
                    trailRenderer.endColor = Color.Lerp(endColor, endColor.ChangeAlpha(targetAlphaStartPoint), currentValue);
                }
            }, name);
        }
    }
}