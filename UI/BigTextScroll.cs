using UnityEngine;
using NaughtyAttributes;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UC
{

    public class BigTextScroll : MonoBehaviour
    {
        public delegate void OnEndScroll();
        public event OnEndScroll onEndScroll;

        [SerializeField]
        TextAsset textFile;
        [SerializeField, ResizableTextArea, ShowIf("noFile")]
        private string text;
        [SerializeField]
        private TextMeshProUGUI textPrefab;
        [SerializeField]
        private float scrollSpeed;
        [SerializeField]
        private PlayerInput playerInput;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        protected InputControl backControl;
        [SerializeField]
        protected bool  enableManualLimits = false;
        [SerializeField, ShowIf(nameof(enableManualLimits))]
        protected Vector2 manualLimits = Vector2.zero;

        Vector3 originalPosition;
        RectTransform rectTransform;
        RectTransform lastRectTransform;

        bool noFile => textFile == null;

        void Start()
        {
            backControl.playerInput = playerInput;

            if (textFile)
            {
                text = textFile.text;
            }

            var lines = text.Split('\n', System.StringSplitOptions.None);
            foreach (var line in lines)
            {
                var tmp = Instantiate(textPrefab, transform);
                if (string.IsNullOrEmpty(line.Trim()))
                    tmp.text = "<color=#FF000000>||ABCD||</color>";
                else
                    tmp.text = line;

                lastRectTransform = tmp.GetComponent<RectTransform>();
            }

            rectTransform = GetComponent<RectTransform>();
            originalPosition = rectTransform.anchoredPosition;

            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        void Update()
        {
            rectTransform.anchoredPosition = rectTransform.anchoredPosition + Vector2.up * scrollSpeed * Time.deltaTime;

            float maxY = Mathf.Abs(lastRectTransform.anchoredPosition.y) + 150.0f;
            if (enableManualLimits)
                maxY = manualLimits.y;

            if ((rectTransform.anchoredPosition.y > maxY) || (backControl.IsDown()))
            {
                onEndScroll?.Invoke();
            }
        }

        public void Reset()
        {
            if (enableManualLimits)
                rectTransform.anchoredPosition = new Vector2(originalPosition.x, manualLimits.x);
            else
                rectTransform.anchoredPosition = originalPosition;
        }
    }
}