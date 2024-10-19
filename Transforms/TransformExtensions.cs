using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
}
