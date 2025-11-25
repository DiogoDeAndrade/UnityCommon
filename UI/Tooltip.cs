using TMPro;
using UnityEngine;

namespace UC
{

    [RequireComponent(typeof(CanvasGroup))]
    public class Tooltip : MonoBehaviour
    {
        protected TextMeshProUGUI text;
        protected CanvasGroup canvasGroup;
        protected RectTransform rectTransform;

        protected virtual void Awake()
        {
            text = GetComponentInChildren<TextMeshProUGUI>();
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = transform as RectTransform;
            canvasGroup.alpha = 0.0f;
        }

        protected virtual void Start()
        {
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

            switch (TooltipManager.parentCanvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    RectTransform canvasTransform = TooltipManager.parentCanvas.transform as RectTransform;
                    screenPosition = RectTransformUtility.WorldToScreenPoint(null, worldPos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasTransform, screenPosition, null, out localPoint);
                    float canvasScaleFactor = canvasTransform.lossyScale.x;
                    rectTransform.anchoredPosition = localPoint / canvasScaleFactor;
                    break;
                case RenderMode.ScreenSpaceCamera:
                    screenPosition = RectTransformUtility.WorldToScreenPoint(TooltipManager.referenceCamera, worldPos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(TooltipManager.parentCanvas.transform as RectTransform, screenPosition, TooltipManager.parentCanvas.worldCamera, out localPoint);
                    rectTransform.anchoredPosition = localPoint;
                    break;
                case RenderMode.WorldSpace:
                    rectTransform.anchoredPosition = worldPos;
                    break;
                default:
                    break;
            }
        }

        public void Remove()
        {
            if (canvasGroup)
                canvasGroup.FadeOut(0.1f).Done(() => { Destroy(gameObject); });
            else
                Destroy(gameObject);
        }
    }
}
