using System.Collections;
using UnityEngine;

public static class CanvasGroupExtensions
{
    public static void FadeIn(this CanvasGroup group, float time)
    {
        group.Tween().Interpolate(group.alpha, 1.0f, time, (value) => group.alpha = value);
    }

    public static void FadeOut(this CanvasGroup group, float time)
    {
        group.Tween().Interpolate(group.alpha, 0.0f, time, (value) => group.alpha = value);
    }
}
