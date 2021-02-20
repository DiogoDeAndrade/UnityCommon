using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UI.Extensions;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HistGraph : MonoBehaviour
{
    public struct Bin
    {
        public int      value;
        public Vector2  range;
    }
    public          bool    background = true;
    [ShowIf("background")]
    public          Color   backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.85f);
    public Color            barColor = Color.red;
    public float            padding = 0;
    public Color            textColor = Color.yellow;
    public TMP_FontAsset    font;
    public int              fontSize = 12;
    public float            titleTextSize = 14;

    [BoxGroup("Graph Area")]
    public Vector2 minGraphArea = new Vector2(0.15f, 0.15f);
    [BoxGroup("Graph Area")]
    public Vector2 maxGraphArea = new Vector2(0.95f, 0.85f);
    [BoxGroup("Graph Area")]
    public bool     fixedLimit;
    [ShowIf("fixedLimit"), BoxGroup("Graph Area")]
    public int      maxLimit = 10;
    [ShowIf("fixedLimit"), BoxGroup("Graph Area")]
    public bool     allowExpandY = false;
    [BoxGroup("Axis")]
    public          bool    displayAxisX = true;
    [BoxGroup("Axis")]
    public          bool    displayAxisY = true;
    [BoxGroup("Axis")]
    public          float   axisTextSize = 10;
    [BoxGroup("Axis")]
    public          string  labelAxisX = "AxisX";
    [BoxGroup("Axis")]
    public          string  labelAxisY = "AxisY";
    [BoxGroup("Axis")]
    public          string  labelFormatX = "0.00";
    [BoxGroup("Axis")]
    public          string  labelFormatY = "0.00";

    string              _title;
    Bin[]               data;
    bool                dirty;
    bool                layoutDirty;
    TextMeshProUGUI     titleElement;
    RectTransform       rectTransform;
    Rect                prevRect;
    RectTransform       graphingArea;
    Axis                axisX;
    Axis                axisY;

    struct BinObj
    {
        public RectTransform   rectRT;
        public Image           rect;
        public RectTransform   labelRT;
        public TextMeshProUGUI label;
    };

    List<BinObj>    binObjs;

    public string title
    {
        get { return _title; }
        set { if (titleElement != null) titleElement.text = value; _title = value; }
    }

    void Awake()
    {
        Image img = gameObject.GetComponent<Image>();
        if (background)
        {
            if (img == null) img = gameObject.GetComponent<Image>();
            img.color = backgroundColor;
            img.enabled = true;
        }
        else
        {
            if (img != null) img.enabled = false;
        }

        rectTransform = GetComponent<RectTransform>();

        titleElement = GraphUtils.CreateTextRenderer("Title", textColor, titleTextSize, rectTransform);
        titleElement.alignment = TextAlignmentOptions.Center;
        titleElement.alignment = TextAlignmentOptions.Top;
        titleElement.text = _title;
        RectTransform rt = titleElement.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1.0f);

        graphingArea = GraphUtils.CreateUIObject("Graph", rectTransform);
        graphingArea.anchorMin = new Vector2(0, 0);
        graphingArea.anchorMax = new Vector2(1, 1);
        graphingArea.pivot = new Vector2(0.5f, 0.5f);
        graphingArea.offsetMin = graphingArea.offsetMax = Vector2.zero;

        prevRect = rectTransform.rect;
        layoutDirty = true;
    }

    // Update is called once per frame
    void Update()
    {
        if ((layoutDirty) || (prevRect != rectTransform.rect))
        {
            UpdateLayout();
        }
        if (dirty)
        {
            RefreshHistogram();
        }
    }

    public void SetData(Bin[] data)
    {
        this.data = data;
        dirty = true;
    }

    void UpdateLayout()
    {
        graphingArea.sizeDelta = new Vector2(rectTransform.rect.width * (maxGraphArea.x - minGraphArea.x),
                                             rectTransform.rect.height * (maxGraphArea.y - minGraphArea.y));
        graphingArea.anchoredPosition = new Vector2(rectTransform.rect.width * minGraphArea.x,
                                                    rectTransform.rect.height * minGraphArea.y);
        graphingArea.offsetMin = new Vector2(rectTransform.rect.width * minGraphArea.x,
                                             rectTransform.rect.height * minGraphArea.y);
        graphingArea.offsetMax = new Vector2(-rectTransform.rect.width * (1 - maxGraphArea.x),
                                             -rectTransform.rect.height * (1 - maxGraphArea.y));

        if (displayAxisX)
        {
            if (axisX == null)
            {
                RectTransform rt = GraphUtils.CreateUIObject("AxisX", graphingArea);

                axisX = rt.gameObject.AddComponent<Axis>();
                axisX.orientation = Axis.Orientation.Horizontal;
                axisX.textColor = textColor;
                axisX.textSize = axisTextSize;
                axisX.labelFormat = labelFormatX;
            }
            axisX.UpdateLayout();
            axisX.labelAxis = labelAxisX;
        }
        else
        {
            if (axisX != null) axisX.gameObject.SetActive(false);
        }

        if (displayAxisY)
        {
            if (axisY == null)
            {
                RectTransform rt = GraphUtils.CreateUIObject("AxisY", graphingArea);

                axisY = rt.gameObject.AddComponent<Axis>();
                axisY.orientation = Axis.Orientation.Vertical;
                axisY.textColor = textColor;
                axisY.textSize = axisTextSize;
                axisY.labelFormat = labelFormatY;
            }
            axisY.UpdateLayout();
            axisY.labelAxis = labelAxisY;
        }
        else
        {
            if (axisY != null) axisY.gameObject.SetActive(false);
        }

        prevRect = rectTransform.rect;
        layoutDirty = false;
    }

    void RefreshHistogram()
    {
        float width = graphingArea.rect.width;
        float height = graphingArea.rect.height;

        if (binObjs == null) binObjs = new List<BinObj>();

        for (int i = binObjs.Count; i < data.Length; i++)
        {
            // Create object
            GameObject newObj = new GameObject();
            newObj.name = string.Format("Bin {0:000}", i);
            newObj.transform.SetParent(graphingArea.transform);

            GameObject newTextObj = new GameObject();
            newTextObj.name = string.Format("Bin Text {0:000}", i);
            newTextObj.transform.SetParent(newObj.transform);

            var newBinObj = new BinObj();
            newBinObj.rect = newObj.AddComponent<Image>();
            newBinObj.label = newTextObj.AddComponent<TextMeshProUGUI>();

            newBinObj.rectRT = newObj.GetComponent<RectTransform>();
            if (newBinObj.rectRT == null) newBinObj.rectRT = newObj.AddComponent<RectTransform>();
            newBinObj.rectRT.anchorMin = Vector2.zero;
            newBinObj.rectRT.anchorMax = Vector2.zero;
            newBinObj.rectRT.pivot = Vector2.zero;

            newBinObj.label.alignment = TextAlignmentOptions.Center;
            newBinObj.label.alignment = TextAlignmentOptions.Top;
            if (font) newBinObj.label.font = font;
            newBinObj.label.fontSize = fontSize;
            newBinObj.label.color = textColor;

            newBinObj.labelRT = newTextObj.GetComponent<RectTransform>();
            if (newBinObj.labelRT == null) newBinObj.labelRT = newTextObj.AddComponent<RectTransform>();
            newBinObj.labelRT.anchorMin = new Vector2(0.5f, 0.0f);
            newBinObj.labelRT.anchorMax = new Vector2(0.5f, 0.0f);
            newBinObj.labelRT.pivot = new Vector2(0.5f, 1.0f);

            binObjs.Add(newBinObj);
        }
        if (binObjs.Count > data.Length)
        {
            for (int i = data.Length; i < binObjs.Count; i++)
            {
                // Remove objects
                Destroy(binObjs[i].rectRT.gameObject);
            }
            binObjs.RemoveRange(data.Length, binObjs.Count - data.Length);
        }

        float maxValue = data[0].value;
        for (int i = 0; i < data.Length; i++)
        {
            maxValue = Mathf.Max(maxValue, data[i].value);
        }

        if (fixedLimit)
        {
            if (allowExpandY)
            {
                if (maxValue > maxLimit)
                {
                    maxLimit = (int)maxValue;
                }
            }

            maxValue = maxLimit;
        }

        float dx = width / data.Length;

        for (int i = 0; i < data.Length; i++)
        {
            float t = Mathf.Clamp01(data[i].value / maxValue);
            binObjs[i].rectRT.localScale = Vector2.one;
            binObjs[i].rectRT.anchoredPosition = new Vector2(dx * i + padding * 0.5f, 0);
            binObjs[i].rectRT.sizeDelta = new Vector2(dx - padding, height * t);

            binObjs[i].rect.color = barColor;

            float midPoint = (data[i].range.x + data[i].range.y) * 0.5f;
            binObjs[i].label.text = string.Format("{0:0.00}", midPoint);
        }

        if (axisY)
        {
            if (displayAxisY)
            {
                axisY.range = new Vector2(0, maxValue);
            }
            axisY.gameObject.SetActive(displayAxisY);
        }

        dirty = false;
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Handles.Label(transform.position, name);
        }
#endif
    }

}
