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
        return group.Tween().Interpolate(group.alpha, targetAlpha, time, (value) => group.alpha = value, "CanvasAlpha");
    }

}
