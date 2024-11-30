using System.Collections;
using UnityEngine;

public static class CanvasGroupExtensions
{
    public static void FadeIn(this CanvasGroup group, float time)
    {
        if (group.alpha == 1.0f) return;
        group.Tween().Interpolate(group.alpha, 1.0f, time, (value) => group.alpha = value, "CanvasAlpha");
    }

    public static void FadeOut(this CanvasGroup group, float time)
    {
        if (group.alpha == 0.0f) return;
        group.Tween().Interpolate(group.alpha, 0.0f, time, (value) => group.alpha = value, "CanvasAlpha");
    }

    public static void FadeTo(this CanvasGroup group, float targetAlpha, float time)
    {
        if (group.alpha == targetAlpha) return;
        group.Tween().Interpolate(group.alpha, targetAlpha, time, (value) => group.alpha = value, "CanvasAlpha");
    }

}
