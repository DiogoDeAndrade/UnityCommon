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

        Image           cursorImage;
        RectTransform   rectTransform;
        CanvasGroup     canvasGroup;
        Vector2         defaultSize = Vector2.zero;
        Color           defaultColor = Color.white;

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
                defaultColor = cursorImage.color;
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

            cursorImage.sprite = cursor;
            cursorImage.color = color;
            if (size != Vector2.zero)
            {
                rectTransform.sizeDelta = size;
            }

            currentCursor = cursor;
            currentColor = color;
            currentSize = size;
        }

        public void SetCursor(bool show)
        {
            if (show) canvasGroup.FadeIn(fadeTime);
            else canvasGroup.FadeOut(fadeTime);
        }
    }
}
