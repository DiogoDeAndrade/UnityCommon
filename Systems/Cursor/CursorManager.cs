using System;
using UC;
using UnityEngine;
using UnityEngine.UI;

namespace UC
{
    public class CursorManager : MonoBehaviour
    {
        [SerializeField] private Sprite defaultCursor;
        [SerializeField] private float fadeTime = 0.15f;
        [SerializeField] private bool  hwCursor = false;

        Image           cursorImage;
        RectTransform   rectTransform;
        CanvasGroup     canvasGroup;
        Vector2         defaultSize = Vector2.zero;
        Color           defaultColor = Color.white;
        Camera          uiCamera;

        Sprite  currentCursor;
        Color   currentColor;
        Vector2 currentSize;

        void Start()
        {
            cursorImage = GetComponent<Image>();
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = transform as RectTransform;
            if ((canvasGroup) && (defaultCursor))
            {
                canvasGroup.FadeIn(fadeTime);
                defaultSize = rectTransform.sizeDelta;
                defaultColor = (cursorImage) ? (cursorImage.color) : (Color.white);

                SetCursor(defaultCursor, defaultColor, defaultSize);
                SetCursor(true);
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    uiCamera = canvas.worldCamera;
            }
        }

        public void SetCursor(CursorDef def)
        {
            if (def != null)
                SetCursor(def.cursor, def.color, def.size);
            else
                SetCursor(null, defaultColor, defaultSize);
        }

        public void SetCursor(Sprite cursor, Color color, Vector2 size)
        {
            if (defaultCursor == null)
            {
                if (cursor)
                {
                    canvasGroup.FadeIn(fadeTime);
                }
                else
                {

                    canvasGroup.FadeOut(fadeTime);
                }
            }

            if ((cursor == null) && (defaultCursor))
            {
                cursor = defaultCursor;
                size = defaultSize;
                color = defaultColor;
            }

            if (cursorImage)
            {
                cursorImage.sprite = cursor;
                cursorImage.color = color;
            }
            if (size != Vector2.zero)
            {
                rectTransform.sizeDelta = size;
            }

            currentCursor = cursor;
            currentColor = color;
            currentSize = size;

            if (hwCursor)
            {
                if (currentCursor)
                {
                    Cursor.SetCursor(currentCursor.texture, new Vector2(currentCursor.pivot.x, currentCursor.rect.height - currentCursor.pivot.y), CursorMode.ForceSoftware);
                }
            }
        }

        public void SetCursor(bool show)
        {
            if (show) canvasGroup.FadeIn(fadeTime);
            else canvasGroup.FadeOut(fadeTime);

            if (hwCursor)
            {
                Cursor.visible = show;
            }
        }
    }
}
