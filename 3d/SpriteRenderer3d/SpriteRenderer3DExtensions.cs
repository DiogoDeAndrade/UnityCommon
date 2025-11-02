using UnityEngine;

namespace UC
{

    public static class SpriteRenderer3DExtensions
    {
        public static Tweener.BaseInterpolator FadeTo(this SpriteRenderer3D spriteRenderer, Color targetColor, float time, string name = null)
        {
            return spriteRenderer.Tween().Interpolate(spriteRenderer.color, targetColor, time, (currentValue) => { if (spriteRenderer) spriteRenderer.color = currentValue; }, name);
        }
    }
}