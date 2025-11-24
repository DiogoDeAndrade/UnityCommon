using UnityEngine;

namespace UC
{

    public class TooltipManager : MonoBehaviour
    {
        [SerializeField] private Tooltip        tooltipPrefab;
        [SerializeField] private RectTransform  tooltipParent;
        [SerializeField] private Camera         _referenceCamera;

        protected Canvas _parentCanvas;

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
            var tooltip = Instantiate(tooltipPrefab, tooltipParent);
            return tooltip;
        }

        public static Tooltip CreateTooltip()
        {
            return instance._CreateTooltip();
        }

        public static Camera referenceCamera => instance._referenceCamera;
        public static Canvas parentCanvas => instance._parentCanvas;
    }
}
