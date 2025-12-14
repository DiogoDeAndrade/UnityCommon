using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UC
{

    public abstract class TextHoverHandler : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
    {
        private Canvas rootCanvas;
        private TextMeshProUGUI textComponent;
        private Camera uiCamera;
        private int hoveredLinkIndex = -1;

        void Start()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            // Check if it has interaction
            var raycaster = rootCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                // This object is not doing anything without a raycaster
                Destroy(gameObject);
                return;
            }

            if ((rootCanvas != null) && (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay))
            {
                uiCamera = rootCanvas.worldCamera;
            }
            else
            {
                uiCamera = null;
            }

            textComponent = GetComponent<TextMeshProUGUI>();
            if ((textComponent == null) || (!textComponent.raycastTarget))
            {
                // No textmesh component
                Destroy(gameObject);
                return;
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, uiCamera);

            if (linkIndex == hoveredLinkIndex)
            {
                // Didn't change
                return;
            }

            // Hover changed
            if (hoveredLinkIndex != -1)
            {
                // Exit previous
                OnLinkExit(GetLinkId(hoveredLinkIndex));
            }

            hoveredLinkIndex = linkIndex;

            if (hoveredLinkIndex != -1)
            {
                // Enter new
                string id = GetLinkId(hoveredLinkIndex);
                Vector2 screenPos = eventData.position;
                OnLinkEnter(id, screenPos);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hoveredLinkIndex != -1)
            {
                OnLinkExit(GetLinkId(hoveredLinkIndex));
                hoveredLinkIndex = -1;
            }
        }

        private string GetLinkId(int linkIndex)
        {
            return textComponent.textInfo.linkInfo[linkIndex].GetLinkID();
        }

        protected abstract void OnLinkEnter(string linkId, Vector2 screenPos);
        protected abstract void OnLinkExit(string linkId);
    }
}