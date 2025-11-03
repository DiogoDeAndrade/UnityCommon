using TMPro;
using UnityEngine;

namespace UC
{

    [RequireComponent(typeof(CanvasGroup))]
    public class Tooltip : MonoBehaviour
    {
        TextMeshProUGUI text;
        CanvasGroup canvasGroup;
        Canvas parentCanvas;
        RectTransform rectTransform;

        private void Awake()
        {
            text = GetComponentInChildren<TextMeshProUGUI>();
            canvasGroup = GetComponent<CanvasGroup>();
            parentCanvas = GetComponentInParent<Canvas>();
            rectTransform = transform as RectTransform;
            canvasGroup.alpha = 0.0f;
        }

        void Start()
        {
            if (parentCanvas.renderMode != RenderMode.WorldSpace)
            {
                if ((parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ||
                    (parentCanvas.worldCamera == null))
                {
                    Debug.LogWarning("Tooltip won't work correctly if using overlay mode on canvas, or if camera is not set");
                }
            }
        }

        public void SetText(string text)
        {
            if (text == "")
            {
                canvasGroup.FadeOut(0.1f);
            }
            else
            {
                this.text.text = text;
                canvasGroup.FadeIn(0.1f);
            }
        }

        public void SetPosition(Vector3 worldPos)
        {
            Vector2 screenPosition;
            Vector2 localPoint;

            switch (parentCanvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    RectTransform canvasTransform = parentCanvas.transform as RectTransform;
                    screenPosition = RectTransformUtility.WorldToScreenPoint(null, worldPos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasTransform,
                        screenPosition,
                        null, // Null because Overlay mode does not use a camera
                        out localPoint
                    );
                    float canvasScaleFactor = canvasTransform.lossyScale.x;
                    rectTransform.anchoredPosition = localPoint / canvasScaleFactor;
                    break;
                case RenderMode.ScreenSpaceCamera:
                    screenPosition = RectTransformUtility.WorldToScreenPoint(parentCanvas.worldCamera, worldPos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentCanvas.transform as RectTransform,
                        screenPosition,
                        parentCanvas.worldCamera,
                        out localPoint
                    );
                    rectTransform.anchoredPosition = localPoint;
                    break;
                case RenderMode.WorldSpace:
                    rectTransform.anchoredPosition = worldPos;
                    break;
                default:
                    break;
            }
        }
    }
}