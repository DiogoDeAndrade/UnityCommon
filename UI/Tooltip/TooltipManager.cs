using UnityEngine;

namespace UC
{

    public class TooltipManager : MonoBehaviour
    {
        [SerializeField] private Tooltip        tooltipPrefab;
        [SerializeField] private RectTransform  tooltipParent;
        [SerializeField] private Camera         _referenceCamera;
        [SerializeField] private bool           forceSingleTooltip = false;

        protected Canvas    _parentCanvas;
        protected Tooltip   lastTooltip;

        static TooltipManager instance;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            _parentCanvas = GetComponentInParent<Canvas>();

            if (_parentCanvas.renderMode != RenderMode.WorldSpace)
            {
                if ((_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ||
                    (_parentCanvas.worldCamera == null))
                {
                    Debug.LogWarning("Tooltip won't work correctly if using overlay mode on canvas, or if camera is not set");
                }
            }

            if (tooltipParent == null) tooltipParent = transform as RectTransform;
        }

        private Tooltip _CreateTooltip()
        {
            if (forceSingleTooltip && (lastTooltip != null))
            {
                Destroy(lastTooltip.gameObject);
            }
            lastTooltip = Instantiate(tooltipPrefab, tooltipParent);
            return lastTooltip;
        }

        private void _ResetTooltip()
        {
            if (forceSingleTooltip && (lastTooltip != null))
            {
                Destroy(lastTooltip.gameObject);
                lastTooltip = null;
            }
        }

        public static Tooltip CreateTooltip()
        {
            return instance._CreateTooltip();
        }

        public static void ResetTooltip()
        {
            instance._ResetTooltip();
        }

        public static Camera referenceCamera => instance._referenceCamera;
        public static Canvas parentCanvas => instance._parentCanvas;
    }
}
