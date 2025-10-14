using TMPro;
using UnityEngine;

namespace UC
{

    public static class TextMeshProExtensions
    {
        public static Tweener.BaseInterpolator FlashColor(this TextMeshProUGUI text, Color originalColor, Color flashColor, float time)
        {
            return text.Tween().Interpolate(flashColor, originalColor, time, (value) => { text.color = value; }, "FlashColor");
        }

        public static Tweener.BaseInterpolator FlashColor(this TextMeshPro text, Color originalColor, Color flashColor, float time)
        {
            return text.Tween().Interpolate(flashColor, originalColor, time, (value) => { text.color = value; }, "FlashColor");
        }
    }
}