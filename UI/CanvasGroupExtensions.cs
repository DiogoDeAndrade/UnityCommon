using System.Collections;
using UnityEngine;

public static class CanvasGroupExtensions
{
    public static Tweener.BaseInterpolator FadeIn(this CanvasGroup group, float time)
    {
        return group.FadeTo(1.0f, time);
    }

    public static Tweener.BaseInterpolator FadeOut(this CanvasGroup group, float time)
    {
        return group.FadeTo(0.0f, time);
    }

    public static Tweener.BaseInterpolator FadeTo(this CanvasGroup group, float targetAlpha, float time)
    {
        if (group.alpha == targetAlpha) return null;

        var currentInterpolator = group.Tween().GetInterpolator("CanvasAlpha");
        if (currentInterpolator != null)
        {
            // Check if this is the interpolator already in use and with the same target
            var floatInterpolator = currentInterpolator as Tweener.Interpolator<float>;
            if ((floatInterpolator.endValue == targetAlpha) &&
                (floatInterpolator.totalTime == time)) return currentInterpolator;
        }

        return group.Tween().Interpolate(group.alpha, targetAlpha, time, (value) => { if (group) group.alpha = value; }, "CanvasAlpha");
    }

}
