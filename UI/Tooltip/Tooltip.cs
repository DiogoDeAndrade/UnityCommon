using NaughtyAttributes;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UC
{

    [RequireComponent(typeof(CanvasGroup))]
    public class Tooltip : MonoBehaviour
    {
        [SerializeField] 
        private bool autoFollowCursor;
        [SerializeField, ShowIf(nameof(autoFollowCursor))] 
        private Vector2 tooltipOffset = Vector2.zero;
        [SerializeField, ShowIf(nameof(autoFollowCursor))]
        private PlayerInput playerInput;
        [SerializeField, ShowIf(nameof(hasPlayerInput))]
        private InputControl mousePositionControl;

        protected TextMeshProUGUI text;
        protected CanvasGroup canvasGroup;
        protected RectTransform rectTransform;

        bool hasPlayerInput => autoFollowCursor && (playerInput != null);

        protected virtual void Awake()
        {
            text = GetComponentInChildren<TextMeshProUGUI>();
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = transform as RectTransform;
            canvasGroup.alpha = 0.0f;

            if (playerInput) mousePositionControl.playerInput = playerInput;
        }
        public virtual void Set(object obj)
        {
            throw new NotImplementedException();
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

        public void Update()
        {
            if (!autoFollowCursor) return;

            var canvas = TooltipManager.parentCanvas;
            var parentRect = rectTransform.parent as RectTransform;

            Vector2 mousePosition;
            if (hasPlayerInput) mousePosition = mousePositionControl.GetAxis2();
            else mousePosition = Input.mousePosition;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out localPoint);

            if (Input.mousePosition.x < Screen.width * 0.5f)
            {
                if (Input.mousePosition.y < Screen.height * 0.5f)
                {
                    rectTransform.anchoredPosition = localPoint + new Vector2(tooltipOffset.x, -tooltipOffset.y);
                    rectTransform.pivot = new Vector2(0f, 0f);
                }
                else
                {
                    rectTransform.anchoredPosition = localPoint + tooltipOffset;
                    rectTransform.pivot = new Vector2(0f, 1f);
                }
            }
            else
            {
                if (Input.mousePosition.y < Screen.height * 0.5f)
                {
                    rectTransform.anchoredPosition = localPoint + new Vector2(-tooltipOffset.x, -tooltipOffset.y);
                    rectTransform.pivot = new Vector2(1f, 0f);
                }
                else
                {
                    rectTransform.anchoredPosition = localPoint + new Vector2(-tooltipOffset.x, tooltipOffset.y);
                    rectTransform.pivot = new Vector2(1f, 1f);
                }
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
            {
                var fader = canvasGroup.FadeOut(0.1f);
                if (fader != null) fader.Done(() => 
                { 
                    Destroy(gameObject); 
                });
                else
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
