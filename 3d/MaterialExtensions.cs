using UnityEngine;

namespace UC
{

    public static class MaterialExtensions
    {
        public static Tweener.BaseInterpolator TweenProperty(this Material material, GameObject ownerGameObject, string propertyName, float endValue, float duration)
        {
            ownerGameObject.Tween().Stop($"Tween{propertyName}", Tweener.StopBehaviour.SkipToEnd);

            var current = material.GetFloat(propertyName);

            return ownerGameObject.Tween().Interpolate(current, endValue, duration, (value) =>
            {
                material.SetFloat(propertyName, value);
            }, $"Tween{propertyName}").Done(() => material.SetFloat(propertyName, endValue));
        }

        public static Tweener.BaseInterpolator TweenProperty(this Material material, GameObject ownerGameObject, string propertyName, Color endColor, float duration)
        {
            ownerGameObject.Tween().Stop($"Tween{propertyName}", Tweener.StopBehaviour.SkipToEnd);

            var current = material.GetColor(propertyName);

            return ownerGameObject.Tween().Interpolate(current, endColor, duration, (value) =>
            {
                material.SetColor(propertyName, value);
            }, $"Tween{propertyName}").Done(() => material.SetColor(propertyName, endColor));
        }
    }
}