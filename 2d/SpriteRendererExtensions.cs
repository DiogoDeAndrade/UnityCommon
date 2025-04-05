using UnityEngine;

namespace UC
{

    public static class SpriteRendererExtensions
    {
        public static Tweener.BaseInterpolator FadeTo(this SpriteRenderer spriteRenderer, Color targetColor, float time, string name = null)
        {
            return spriteRenderer.Tween().Interpolate(spriteRenderer.color, targetColor, time, (currentValue) => { if (spriteRenderer) spriteRenderer.color = currentValue; }, name);
        }
    }
}