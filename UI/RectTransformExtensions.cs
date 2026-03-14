using System.Runtime.CompilerServices;
using UnityEngine;

namespace UC
{

    public static class RectTransformExtensions
    {
        public static bool TryGetCursorUV(this RectTransform rt, Camera eventCamera, ref Vector2 uv)
        {
            uv = default;
            if (rt == null) return false;

            // 1) Get cursor position in screen space
            Vector2 screenPos = InputControl.GetScreenMousePosition();

            // 2) Convert screen point to local point in the RectTransform
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, eventCamera, out var local))
                return false;

            // 3) Check containment in local space rect
            Rect r = rt.rect; // local-space rect (anchored around pivot)
            if (!r.Contains(local))
                return false;

            // 4) Convert local point to normalized UV within rect
            uv.x = (local.x - r.xMin) / r.width;
            uv.y = (local.y - r.yMin) / r.height;

            return true;
        }

        public static Camera GetEventCamera(this RectTransform rt)
        {
            var canvas = rt.GetComponent<Canvas>();
            if (!canvas) canvas = rt.GetComponentInParent<Canvas>();
            if (canvas)
            {
                switch (canvas.renderMode)
                {
                    case RenderMode.ScreenSpaceCamera:
                    case RenderMode.WorldSpace:
                        return canvas.worldCamera;
                }
            }
            return null;
        }
    }
}