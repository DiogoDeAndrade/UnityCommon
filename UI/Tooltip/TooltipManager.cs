using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UC
{
    public class TooltipManager : MonoBehaviour
    {
        static public event Func<bool> isTooltipEnabled;

        [Flags]
        public enum Target
        {
            None = 0,
            Subsystem2D = 1,
            Subsystem3D = 2,
            SubsystemUI = 4
        }

        private struct TooltipHit
        {
            public ITooltip tooltip;
            public float distance;
            public int order;

            public TooltipHit(ITooltip tooltip, float distance)
            {
                this.tooltip = tooltip;
                this.distance = distance;
                this.order = tooltip.GetOrder();
            }
        }

        [Header("Targets")]
        [SerializeField]
        private Target allowedTargets = Target.Subsystem2D | Target.Subsystem3D | Target.SubsystemUI;

        [SerializeField]
        private Camera          interactionCamera;
        [SerializeField]
        private PlayerInput     playerInput;
        [SerializeField, InputPlayer(nameof(playerInput))]
        private UC.InputControl mousePositionControl;

        [SerializeField, ShowIf(nameof(has2d))]
        private LayerMask mask2d;

        [SerializeField, ShowIf(nameof(has3d))]
        private LayerMask mask3d;

        [SerializeField, ShowIf(nameof(hasUI))]
        private LayerMask maskUI;

        [Header("UI")]
        [SerializeField, ShowIf(nameof(hasUI))]
        private GraphicRaycaster graphicRaycaster;

        [SerializeField, ShowIf(nameof(hasUI))]
        private EventSystem eventSystem;

        [Header("Tooltip Position")]
        [SerializeField]
        private Vector2 cursorOffset = new Vector2(18f, -18f);

        [SerializeField]
        private Vector2 screenMargin = new Vector2(8f, 8f);

        private bool has2d => (allowedTargets & Target.Subsystem2D) != 0;
        private bool has3d => (allowedTargets & Target.Subsystem3D) != 0;
        private bool hasUI => (allowedTargets & Target.SubsystemUI) != 0;

        private ITooltip        currentTooltip;
        private RectTransform   currentTooltipObject;
        private RectTransform   rectTransform;
        private Canvas          canvas;

        private readonly List<TooltipHit> potentialTooltips = new();
        private readonly List<RaycastResult> uiRaycastResults = new();

        static public bool isInteractionEnabled
        {
            get
            {
                if (isTooltipEnabled == null)
                    return true;

                foreach (System.Func<bool> callback in isTooltipEnabled.GetInvocationList())
                {
                    if (!callback())
                        return false;
                }

                return true;
            }
        }

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            canvas = GetComponentInParent<Canvas>();

            if (interactionCamera == null)
                interactionCamera = Camera.main;

            if (graphicRaycaster == null)
                graphicRaycaster = GetComponentInParent<GraphicRaycaster>();

            if (eventSystem == null)
                eventSystem = EventSystem.current;

            mousePositionControl.playerInput = playerInput;
        }

        private void Update()
        {
            potentialTooltips.Clear();

            if (interactionCamera == null)
                return;
            if (!isInteractionEnabled)
            {
                DestroyTooltip();
                return;
            }

            if (has2d)
                Raycast2D();

            if (has3d)
                Raycast3D();

            if (hasUI)
                RaycastUI();

            ITooltip candidateTooltip = GetBestTooltip();

            if (candidateTooltip != null)
            {
                if (candidateTooltip != currentTooltip)
                {
                    DestroyTooltip();

                    currentTooltip = candidateTooltip;
                    currentTooltipObject = currentTooltip.GetTooltip(rectTransform);
                }
            }
            else
            {
                DestroyTooltip();
            }

            if (currentTooltipObject != null)
            {
                FollowMouse();
            }
        }

        private void Raycast2D()
        {
            Vector2 worldPoint = interactionCamera.ScreenToWorldPoint(GetMousePosition());

            RaycastHit2D[] hits = Physics2D.RaycastAll(worldPoint, Vector2.zero, 0f, mask2d);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                ITooltip tooltip = hit.collider.GetComponentInParent<ITooltip>();

                if (tooltip == null)
                    continue;

                float distance = Vector3.Distance(interactionCamera.transform.position, hit.collider.transform.position);
                potentialTooltips.Add(new TooltipHit(tooltip, distance));
            }
        }

        private void Raycast3D()
        {
            Ray ray = interactionCamera.ScreenPointToRay(GetMousePosition());

            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, mask3d, QueryTriggerInteraction.Collide);

            foreach (RaycastHit hit in hits)
            {
                ITooltip tooltip = hit.collider.GetComponentInParent<ITooltip>();

                if (tooltip == null)
                    continue;

                potentialTooltips.Add(new TooltipHit(tooltip, hit.distance));
            }
        }

        private void RaycastUI()
        {
            if ((graphicRaycaster == null) || (eventSystem == null))
                return;

            uiRaycastResults.Clear();

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = GetMousePosition()
            };

            graphicRaycaster.Raycast(pointerData, uiRaycastResults);

            foreach (RaycastResult result in uiRaycastResults)
            {
                GameObject go = result.gameObject;

                if (((1 << go.layer) & maskUI.value) == 0)
                    continue;

                ITooltip tooltip = go.GetComponentInParent<ITooltip>();

                if (tooltip == null)
                    continue;

                potentialTooltips.Add(new TooltipHit(tooltip, result.distance));
            }
        }

        private ITooltip GetBestTooltip()
        {
            if (potentialTooltips.Count == 0)
                return null;

            potentialTooltips.Sort((a, b) =>
            {
                // Higher order first.
                int orderCompare = b.order.CompareTo(a.order);

                if (orderCompare != 0)
                    return orderCompare;

                // Within the same order, closest first.
                return a.distance.CompareTo(b.distance);
            });

            return potentialTooltips[0].tooltip;
        }

        private void FollowMouse()
        {
            Vector2 screenPosition = GetMousePosition() + cursorOffset;

            Camera canvasCamera = null;

            if ((canvas != null) && (canvas.renderMode != RenderMode.ScreenSpaceOverlay))
                canvasCamera = canvas.worldCamera != null ? canvas.worldCamera : interactionCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, canvasCamera, out Vector2 localPoint);

            // Force tooltip to use top-left anchoring.
            currentTooltipObject.anchorMin = new Vector2(0f, 1f);
            currentTooltipObject.anchorMax = new Vector2(0f, 1f);
            currentTooltipObject.pivot = new Vector2(0f, 1f);

            Rect parentRect = rectTransform.rect;

            // Convert from parent-local coordinates to top-left anchored coordinates.
            Vector2 topLeft = new Vector2(parentRect.xMin, parentRect.yMax);
            Vector2 anchoredPosition = localPoint - topLeft;

            currentTooltipObject.anchoredPosition = anchoredPosition;

            ClampTooltipToParentTopLeft();
        }

        private void ClampTooltipToParentTopLeft()
        {
            Canvas.ForceUpdateCanvases();

            Rect parentRect = rectTransform.rect;
            Rect tooltipRect = currentTooltipObject.rect;

            float parentWidth = parentRect.width;
            float parentHeight = parentRect.height;

            Vector2 position = currentTooltipObject.anchoredPosition;

            // In top-left anchored UI:
            // X grows right.
            // Y grows up, so downwards is negative.
            float minX = screenMargin.x;
            float maxX = parentWidth - tooltipRect.width - screenMargin.x;

            float maxY = -screenMargin.y;
            float minY = -parentHeight + tooltipRect.height + screenMargin.y;

            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = Mathf.Clamp(position.y, minY, maxY);

            currentTooltipObject.anchoredPosition = position;
        }

        private void DestroyTooltip()
        {
            if (currentTooltipObject != null)
            {
                Destroy(currentTooltipObject.gameObject);
                currentTooltipObject = null;
            }

            currentTooltip = null;
        }

        private Vector2 GetMousePosition()
        {
            return mousePositionControl.GetAxis2();
        }
    }

}