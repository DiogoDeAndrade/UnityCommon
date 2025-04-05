using UnityEngine;

namespace UC
{

    public static class LineRendererExtensions
    {
        public static Tweener.BaseInterpolator FadeOut(this LineRenderer lineRenderer, float time)
        {
            return lineRenderer.FadeTo(0.0f, time);
        }

        public static Tweener.BaseInterpolator FadeTo(this LineRenderer lineRenderer, float targetAlpha, float time)
        {
            Color cStart = lineRenderer.startColor;
            Color cEnd = lineRenderer.endColor;

            if ((cStart.a == targetAlpha) && (cEnd.a == targetAlpha)) return null;

            return lineRenderer.Tween().Interpolate(0.0f, 1.0f, time, (value) =>
            {
                if (lineRenderer)
                {
                    lineRenderer.startColor = Color.Lerp(cStart, cStart.ChangeAlpha(targetAlpha), value);
                    lineRenderer.endColor = Color.Lerp(cEnd, cEnd.ChangeAlpha(targetAlpha), value);
                }
            }, "LRFade");
        }
    }
}