using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public static class TransformExtensions
{
    public static Vector3 Center(this Transform t)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr)
        {
            return sr.bounds.center;
        }

        return t.position;
    }

    public static Matrix4x4 GetLocalMatrix(this Transform t)
    {
        return Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
    }

    public static bool ScreenPointOverlaps(this RectTransform rectTransform, Vector2 pos, Camera camera)
    {
        // Convert the mouse position to world space and then to screen point
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, pos, camera, out Vector2 localPoint))
        {
            // Check if the local point is within the rect bounds
            return rectTransform.rect.Contains(localPoint);
        }

        return false;
    }

    public static Tweener.BaseInterpolator Shake2d(this Transform target, float duration, float strength)
    {
        // To avoid getting stuck in shakes, disable any shake that's currently running on this object
        target.Tween().Stop("ShakeTransform", Tweener.StopBehaviour.Cancel);

        var initialPosition = target.position;
        return target.Tween().Interpolate(0.0f, 1.0f, duration, (value) => target.position = initialPosition + (Random.insideUnitCircle * strength).xyz(0.0f), "ShakeTransform").Done(() => target.position = initialPosition);
    }

    public static Tweener.BaseInterpolator Scale(this RectTransform target, Vector2 targetScale, float duration)
    {
        // To avoid getting stuck in shakes, disable any shake that's currently running on this object
        target.Tween().Stop("ScaleRectTransform", Tweener.StopBehaviour.Cancel);

        return target.Tween().Interpolate(target.localScale.xy(), targetScale, duration, (value) => target.localScale = value, "ScaleRectTransform").Done(() => target.localScale = targetScale);
    }

    public static Tweener.BaseInterpolator ScaleTo(this Transform transform, Vector3 targetScale, float time, string name = null)
    {
        return transform.Tween().Interpolate(transform.localScale, targetScale, time, (currentValue) => transform.localScale = currentValue, name);
    }

    public static Tweener.BaseInterpolator Move(this Transform transform, Vector3 moveDelta, float time, string name = null)
    {
        return transform.Tween().Interpolate(transform.localPosition, transform.localPosition + moveDelta, time, (currentValue) => transform.localPosition = currentValue, name);
    }
    public static Tweener.BaseInterpolator MoveTo(this Transform transform, Vector3 targetPos, float time, string name = null)
    {
        return transform.Tween().Interpolate(transform.localPosition, targetPos, time, (currentValue) => transform.localPosition = currentValue, name);
    }
    public static Tweener.BaseInterpolator MoveToWorld(this Transform transform, Vector3 targetPosWorld, float time, string name = null)
    {
        return transform.Tween().Interpolate(transform.position, targetPosWorld, time, (currentValue) => transform.position = currentValue, name);
    }

}
