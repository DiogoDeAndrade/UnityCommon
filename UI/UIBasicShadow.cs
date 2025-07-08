using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NaughtyAttributes;

namespace UC
{

    public class UIBasicShadow : MonoBehaviour
    {
        [SerializeField] private Vector2 shadowOffset = new Vector2(5, 5);
        [SerializeField] private Color shadowColor = Color.black;

        Image parentImage;
        Image shadowImage;

        TextMeshProUGUI parentText;
        TextMeshProUGUI shadowText;

        RectTransform parentRectTransform;
        RectTransform shadowRectTransform;
        Canvas shadowCanvas;

        void Start()
        {
            Init();

            ExecuteShadow();
        }

        void Update()
        {
            ExecuteShadow();
        }

        [Button("Test Shadow")]
        void TestShadow()
        {
            Init();
            ExecuteShadow();
        }

        [Button("Clear Shadow")]
        void ClearShadow()
        {
            if (shadowImage) DestroyImmediate(shadowImage);
            if (shadowText) DestroyImmediate(shadowText);
            if (shadowCanvas) DestroyImmediate(shadowCanvas);
        }

        void ExecuteShadow()
        {
            if (shadowImage != null) ExecuteShadowImage();
            else if (shadowText != null) ExecuteShadowText();

            shadowRectTransform.localPosition = Vector3.zero;
            shadowRectTransform.localRotation = Quaternion.identity;
            shadowRectTransform.localScale = Vector3.one;

            shadowRectTransform.ForceUpdateRectTransforms();
            shadowRectTransform.anchoredPosition = shadowOffset;
            shadowRectTransform.sizeDelta = parentRectTransform.sizeDelta;

            shadowRectTransform.SetSiblingIndex(parentRectTransform.GetSiblingIndex());
        }

        void ExecuteShadowImage()
        {
            shadowImage.sprite = parentImage.sprite;
            shadowImage.color = shadowColor;
        }

        void ExecuteShadowText()
        {
            shadowText.text = parentText.text;
            shadowText.font = parentText.font;
            shadowText.fontSize = parentText.fontSize;
            shadowText.fontStyle = parentText.fontStyle;
            shadowText.alignment = parentText.alignment;
            shadowText.autoSizeTextContainer = parentText.autoSizeTextContainer;
            shadowText.colorGradient = parentText.colorGradient;
            shadowText.colorGradientPreset = parentText.colorGradientPreset;
            shadowText.enableAutoSizing = parentText.enableAutoSizing;
            shadowText.fontMaterial = parentText.fontMaterial;
            shadowText.textWrappingMode = parentText.textWrappingMode;
            shadowText.color = shadowColor;
        }

        private void Init()
        {
            parentRectTransform = transform.parent as RectTransform;
            shadowRectTransform = transform as RectTransform;

            if (transform.parent == null)
            {
                Debug.LogError("UIBasicShader component has to be placed on a child object of the object you want to shadow");
                return;
            }

            parentImage = transform.parent.GetComponent<Image>();
            if (parentImage == null)
            {
                parentText = transform.parent.GetComponent<TextMeshProUGUI>();
                if (parentText == null)
                {
                    Debug.LogError("UIBasicShader only supports shadows on UI.Image or TMPro objects!");
                    return;
                }
                else
                {
                    shadowText = GetComponent<TextMeshProUGUI>();
                    if (shadowText == null) shadowText = gameObject.AddComponent<TextMeshProUGUI>();

                    shadowCanvas = GetComponent<Canvas>();
                    if (shadowCanvas == null)
                    {
                        shadowCanvas = gameObject.AddComponent<Canvas>();
                        shadowCanvas.overrideSorting = true;

                        Canvas parentCanvas = parentText.GetComponent<Canvas>();
                        if (parentCanvas)
                        {
                            shadowCanvas.sortingOrder = parentCanvas.sortingOrder - 1;
                        }
                    }
                }
            }
            else
            {
                shadowImage = GetComponent<Image>();
                if (shadowImage == null) shadowImage = gameObject.AddComponent<Image>();

                shadowCanvas = GetComponent<Canvas>();
                if (shadowCanvas == null)
                {
                    shadowCanvas = gameObject.AddComponent<Canvas>();
                    shadowCanvas.overrideSorting = true;

                    Canvas parentCanvas = parentImage.GetComponent<Canvas>();
                    if (parentCanvas)
                    {
                        shadowCanvas.sortingOrder = parentCanvas.sortingOrder - 1;
                    }
                }
            }
        }
    }
}