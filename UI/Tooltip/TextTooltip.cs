using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UC
{
    public class TextTooltip : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private float targetAspectRatio = 2.5f; // width / height
        [SerializeField] private Vector2 margin = new Vector2(16f, 10f);

        [Header("Limits")]
        [SerializeField] private float minWidth = 80f;
        [SerializeField] private float maxWidth = 500f;
        [SerializeField] private float minHeight = 32f;
        [SerializeField] private float maxHeight = 300f;

        private RectTransform rectTransform;
        private RectTransform textRectTransform;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            textRectTransform = text.transform as RectTransform;
        }

        public void SetText(string txt)
        {
            text.text = txt;

            ResizeToText(txt);
        }

        [Button("Rebuild layout")]
        protected void RebuildLayout()
        {
            SetText(text.text);
        }

        private void ResizeToText(string txt)
        {
            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            if (textRectTransform == null)
                textRectTransform = text.transform as RectTransform;

            text.textWrappingMode = TextWrappingModes.Normal;

            float innerMinWidth = Mathf.Max(1f, minWidth - margin.x * 2f);
            float innerMaxWidth = Mathf.Max(innerMinWidth, maxWidth - margin.x * 2f);

            float bestInnerWidth = innerMaxWidth;
            Vector2 bestTextSize = Vector2.zero;

            // Binary search for the width that gives the desired aspect ratio.
            for (int i = 0; i < 16; i++)
            {
                float candidateInnerWidth = (innerMinWidth + innerMaxWidth) * 0.5f;

                Vector2 preferred = text.GetPreferredValues(txt, candidateInnerWidth, 0f);

                float outerWidth = candidateInnerWidth + margin.x * 2f;
                float outerHeight = preferred.y + margin.y * 2f;

                float aspect = outerWidth / outerHeight;

                bestInnerWidth = candidateInnerWidth;
                bestTextSize = preferred;

                if (aspect < targetAspectRatio)
                {
                    // Too tall / narrow, so increase width.
                    innerMinWidth = candidateInnerWidth;
                }
                else
                {
                    // Too wide / short, so decrease width.
                    innerMaxWidth = candidateInnerWidth;
                }
            }

            float finalInnerWidth = bestInnerWidth;
            float finalInnerHeight = bestTextSize.y;

            float finalOuterWidth = finalInnerWidth + margin.x * 2f;
            float finalOuterHeight = finalInnerHeight + margin.y * 2f;

            finalOuterWidth = Mathf.Clamp(finalOuterWidth, minWidth, maxWidth);
            finalOuterHeight = Mathf.Clamp(finalOuterHeight, minHeight, maxHeight);

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalOuterWidth);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalOuterHeight);

            // Make the text fill the inside of the outline with margins.
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.offsetMin = new Vector2(margin.x, margin.y);
            textRectTransform.offsetMax = new Vector2(-margin.x, -margin.y);

            text.ForceMeshUpdate();

            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }
}
