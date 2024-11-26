using NUnit.Framework.Internal;
using System;
using UnityEngine;

public static class SpriteRendererExtensions
{
    public static Tweener.BaseInterpolator FadeTo(this SpriteRenderer spriteRenderer, Color targetColor, float time, string name = null)
    {
        return spriteRenderer.Tween().Interpolate(spriteRenderer.color, targetColor, time, (currentValue) => spriteRenderer.color = currentValue, name);
    }
}
