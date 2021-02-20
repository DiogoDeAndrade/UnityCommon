using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Legend : MonoBehaviour
{
    public Color       textColor = Color.white;
    public float       textSize = 10;

    bool dirty;

    struct LegendElem
    {
        public string        name;
        public Color         color;
        public RectTransform layoutGroup;
    }

    List<LegendElem> legend;
    RectTransform    rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = Vector2.one;
        var vlg = rectTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandHeight = vlg.childForceExpandWidth = false;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.spacing = 5;
        vlg.childAlignment = TextAnchor.UpperRight;
    }

    /*    Vector2 _range;
        public Vector2     range
        {
            get { return _range; }
            set { _range = value; UpdateVisuals(); }
        }

        string _labelAxis;
        public string      labelAxis
        {
            get { return _labelAxis; }
            set { _labelAxis = value; UpdateVisuals(); }
        }

        RectTransform   rectTransform;
        RectTransform   parentRT;
        TextMeshProUGUI labelMin;
        RectTransform   labelMinRT;
        TextMeshProUGUI labelMax;
        RectTransform   labelMaxRT;
        TextMeshProUGUI labelAxisText;
        RectTransform   labelAxisRT;

        public void UpdateLayout()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (parentRT == null) parentRT = rectTransform.parent.GetComponent<RectTransform>();
            if (labelMin == null)
            {
                labelMin = GraphUtils.CreateTextRenderer("AxisMinLabel", textColor, textSize, rectTransform);
                labelMinRT = labelMin.GetComponent<RectTransform>();
            }
            if (labelMax == null)
            {
                labelMax = GraphUtils.CreateTextRenderer("AxisMaxLabel", textColor, textSize, rectTransform);
                labelMaxRT = labelMax.GetComponent<RectTransform>();
            }
            if (labelAxisText == null)
            {
                labelAxisText = GraphUtils.CreateTextRenderer("AxisLabel", textColor, textSize + 2, rectTransform);
                labelAxisRT = labelAxisText.GetComponent<RectTransform>();
            }

            switch (orientation)
            {
                case Orientation.Horizontal:
                    rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
                    rectTransform.anchorMax = new Vector2(1.0f, 0.0f);
                    rectTransform.pivot = new Vector2(0.5f, 1.0f);
                    rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
                    rectTransform.sizeDelta = new Vector2(0, textSize * 2);
                    labelMinRT.anchorMin = labelMinRT.anchorMax = new Vector2(0.0f, 1.0f);
                    labelMinRT.pivot = new Vector2(0.0f, 1.0f);
                    labelMin.alignment = TextAlignmentOptions.TopLeft;
                    labelMin.overflowMode = TextOverflowModes.Overflow;
                    labelMin.enableWordWrapping = false;

                    labelMaxRT.offsetMin = labelMaxRT.offsetMax = Vector2.zero;
                    labelMaxRT.anchorMin = new Vector2(0.0f, 1.0f);
                    labelMaxRT.anchorMax = new Vector2(1.0f, 1.0f);
                    labelMaxRT.pivot = new Vector2(1.0f, 1.0f);
                    labelMax.alignment = TextAlignmentOptions.TopRight;
                    labelMax.overflowMode = TextOverflowModes.Overflow;
                    labelMax.enableWordWrapping = false;

                    labelAxisRT.offsetMin = labelMaxRT.offsetMax = Vector2.zero;
                    labelAxisRT.anchorMin = new Vector2(0.0f, 1.0f);
                    labelAxisRT.anchorMax = new Vector2(1.0f, 1.0f);
                    labelAxisRT.pivot = new Vector2(0.5f, 1.0f);
                    labelAxisRT.anchoredPosition = new Vector2(0, -(textSize + 2));
                    labelAxisRT.sizeDelta = new Vector2(0, textSize * 2);
                    labelAxisText.alignment = TextAlignmentOptions.Center;
                    labelAxisText.alignment = TextAlignmentOptions.Top;
                    labelAxisText.overflowMode = TextOverflowModes.Overflow;
                    labelAxisText.enableWordWrapping = false;
                    break;
                case Orientation.Vertical:
                    rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
                    rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
                    rectTransform.pivot = new Vector2(1.0f, 0.5f);
                    rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
                    rectTransform.sizeDelta = new Vector2(textSize * 2, 0);

                    labelMinRT.anchorMin = labelMinRT.anchorMax = new Vector2(1.0f, 0.0f);
                    labelMinRT.pivot = new Vector2(1.0f, 0.0f);
                    labelMin.alignment = TextAlignmentOptions.BottomRight;
                    labelMin.overflowMode = TextOverflowModes.Overflow;
                    labelMin.enableWordWrapping = false;

                    labelMaxRT.offsetMin = labelMaxRT.offsetMax = Vector2.zero;
                    labelMaxRT.anchorMin = new Vector2(0.0f, 0.0f);
                    labelMaxRT.anchorMax = new Vector2(0.0f, 1.0f);
                    labelMaxRT.pivot = new Vector2(0.0f, 1.0f);
                    labelMax.alignment = TextAlignmentOptions.TopLeft;
                    labelMax.overflowMode = TextOverflowModes.Overflow;
                    labelMax.enableWordWrapping = false;

                    labelAxisRT.offsetMin = labelMaxRT.offsetMax = Vector2.zero;
                    labelAxisRT.anchorMin = new Vector2(0.0f, 0.0f);
                    labelAxisRT.anchorMax = new Vector2(0.0f, 1.0f);
                    labelAxisRT.pivot = new Vector2(1.0f, 0.5f);
                    labelAxisRT.anchoredPosition = new Vector2(-(textSize * 2.0f), 0);
                    labelAxisRT.sizeDelta = new Vector2(textSize * 2, 0);
                    labelAxisRT.rotation = Quaternion.Euler(0, 0, 90);
                    labelAxisText.alignment = TextAlignmentOptions.Center;
                    labelAxisText.overflowMode = TextOverflowModes.Overflow;
                    labelAxisText.enableWordWrapping = false;
                    break;
            }
        }

        void UpdateVisuals()
        {
            if (labelMin) labelMin.text = "" + _range.x;
            if (labelMax) labelMax.text = "" + _range.y;
            if (labelAxisText) labelAxisText.text = labelAxis;
        }*/

    public void Clear()
    {
        if (legend != null)
        {
            foreach (var l in legend)
            {
                Destroy(l.layoutGroup.gameObject);
            }
        }
        legend = new List<LegendElem>();
        dirty = true;
    }

    public void Add(string name, Color c)
    {
        if (legend == null) legend = new List<LegendElem>();
        
        var newLegend = new LegendElem() 
        { 
            name = name, 
            color = c 
        };
        newLegend.layoutGroup = GraphUtils.CreateUIObject("Element " + name, rectTransform);
        newLegend.layoutGroup.sizeDelta = new Vector2(0, textSize + 2);
        newLegend.layoutGroup.pivot = new Vector2(1, 1);
        var hlg = newLegend.layoutGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = hlg.childControlWidth = false;
        hlg.childForceExpandHeight = hlg.childForceExpandWidth = false;
        hlg.spacing = 5;
        var text = GraphUtils.CreateTextRenderer("Label", textColor, textSize, newLegend.layoutGroup);
        text.text = name;
        text.alignment = TextAlignmentOptions.TopRight;
        var boxBorder = GraphUtils.CreateImageRenderer("BoxBorder", Color.black, textSize, textSize, newLegend.layoutGroup);
        var boxInside = GraphUtils.CreateImageRenderer("BoxInside", c, textSize * 0.9f, textSize * 0.9f, boxBorder.GetComponent<RectTransform>());
        var boxInsideRT = boxInside.GetComponent<RectTransform>();
        boxInsideRT.anchorMin = boxInsideRT.anchorMax = boxInsideRT.pivot = new Vector2(0.5f, 0.5f);
        boxInsideRT.sizeDelta = new Vector2(textSize * 0.9f, textSize * 0.9f);

        legend.Add(newLegend);
        dirty = true;
    }

    private void Update()
    {
        if (dirty)
        {
            UpdateLayout();
        }
    }

    void UpdateLayout()
    {
        dirty = false;
    }
}
