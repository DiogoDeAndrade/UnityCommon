using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Axis : MonoBehaviour
{
    public enum Orientation { Horizontal, Vertical };

    public Orientation orientation;
    public Color       textColor;
    public float       textSize;
    public string      labelFormat = "0.00";

    Vector2 _range;
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
                labelMaxRT.anchorMin = new Vector2(1.0f, 1.0f);
                labelMaxRT.anchorMax = new Vector2(1.0f, 1.0f);
                labelMaxRT.pivot = new Vector2(1.0f, 1.0f);
                labelMax.alignment = TextAlignmentOptions.TopRight;
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
        if (labelMin) labelMin.text = string.Format("{0:" + labelFormat + "}", _range.x);
        if (labelMax) labelMax.text = string.Format("{0:" + labelFormat + "}", _range.y);
        if (labelAxisText) labelAxisText.text = labelAxis;
    }
}
