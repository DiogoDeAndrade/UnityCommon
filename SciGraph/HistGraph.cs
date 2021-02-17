using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UI.Extensions;
using NaughtyAttributes;

public class HistGraph : MonoBehaviour
{
    public struct Bin
    {
        public int      value;
        public Vector2  range;
    }
    public Color            barColor = Color.red;
    public float            padding = 0;
    public Color            textColor = Color.yellow;
    public TMP_FontAsset    font;
    public int              fontSize = 12;

    [SerializeField] TextMeshProUGUI titleElement;
    [SerializeField] RectTransform   plotArea;
    [SerializeField] TextMeshProUGUI labelMinY;
    [SerializeField] TextMeshProUGUI labelMaxY;
    [SerializeField] TextMeshProUGUI labelAxisX;
    [SerializeField] TextMeshProUGUI labelAxisY;


    string              _title;
    Bin[]               data;
    bool                dirty;

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
    public string axisX
    {
        get { return labelAxisX.text; }
        set { labelAxisX.text = value; }
    }
    public string axisY
    {
        get { return labelAxisY.text; }
        set { labelAxisY.text = value; }
    }

    void Start()
    {
        if (titleElement) titleElement.text = _title;
    }

    // Update is called once per frame
    void Update()
    {
        if (dirty)
        {
            RefreshHistogram();
            dirty = false;
        }
    }

    public void SetData(Bin[] data)
    {
        this.data = data;
        dirty = true;
    }

    void RefreshHistogram()
    {
        float width = plotArea.sizeDelta.x;
        float height = plotArea.sizeDelta.y;

        if (binObjs == null) binObjs = new List<BinObj>();

        for (int i = binObjs.Count; i < data.Length; i++)
        {
            // Create object
            GameObject newObj = new GameObject();
            newObj.name = string.Format("Bin {0:000}", i);
            newObj.transform.SetParent(plotArea.transform);

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

        float dx = width / data.Length;

        for (int i = 0; i < data.Length; i++)
        {
            binObjs[i].rectRT.localScale = Vector2.one;
            binObjs[i].rectRT.anchoredPosition = new Vector2(dx * i + padding * 0.5f, 0);
            binObjs[i].rectRT.sizeDelta = new Vector2(dx - padding, height * data[i].value / maxValue);

            binObjs[i].rect.color = barColor;

            float midPoint = (data[i].range.x + data[i].range.y) * 0.5f;
            binObjs[i].label.text = string.Format("{0:0.00}", midPoint);
        }

        if (labelMinY) labelMinY.text = "0";
        if (labelMaxY) labelMaxY.text = "" + (int)maxValue;
    }
}
