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
}
