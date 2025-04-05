using UnityEngine.UI;

namespace UC
{

    public static class ImageExtensions
    {
        public static Tweener.BaseInterpolator FadeIn(this Image image, float time)
        {
            return image.FadeTo(1.0f, time);
        }

        public static Tweener.BaseInterpolator FadeOut(this Image image, float time)
        {
            return image.FadeTo(0.0f, time);
        }

        public static Tweener.BaseInterpolator FadeTo(this Image image, float targetAlpha, float time)
        {
            if (image.color.a == targetAlpha) return null;
            return image.Tween().Interpolate(image.color.a, targetAlpha, time, (value) => { if (image) image.color = image.color.ChangeAlpha(value); }, "ImageColor");
        }

    }
}