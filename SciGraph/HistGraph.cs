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
    public bool             background = true;
    [ShowIf("background")]
    public Color            backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.85f);
    public float            padding = 0;
    public bool             displayLegend = false;
    public Color            textColor = Color.yellow;
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
    [BoxGroup("Bin Config")]
    public int      nBins = 10;
    [BoxGroup("Bin Config")]
    public bool     useBinFixedRange = false;
    [BoxGroup("Bin Config"), ShowIf("useBinFixedRange"), SerializeField]
            Vector2 binRange = new Vector2(0.0f, 100.0f);
    [BoxGroup("Bin Config"), HideIf("useBinFixedRange")]
    public int      binMainSubgraph = 0;
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
    bool                dirty;
    bool                layoutDirty;
    TextMeshProUGUI     titleElement;
    RectTransform       rectTransform;
    Rect                prevRect;
    RectTransform       graphingArea;
    Axis                axisX;
    Axis                axisY;
    Legend              legend;

    struct BinObj
    {
        public RectTransform   rectRT;
        public Image           rect;
    };

    struct BinLabel
    {
        public RectTransform labelRT;
        public TextMeshProUGUI label;
    }

    class Subgraph
    {
        public string       name;
        public Bin[]        data;
        public Color        color;
        public List<BinObj> binObjs;
    }

    List<Subgraph>         subGraphs;
    List<BinLabel>         binLabels;

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

    public int AddSubgraph(Color color, string name)
    {
        var subgraph = new Subgraph();
        subgraph.name = name;
        subgraph.color = color;
        subgraph.binObjs = new List<BinObj>();

        if (subGraphs == null) subGraphs = new List<Subgraph>();
        subGraphs.Add(subgraph);

        UpdateLegend();

        return subGraphs.Count - 1;
    }

    public void SetData(int subgraph_id, Bin[] data)
    {
        subGraphs[subgraph_id].data = data;
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

        UpdateLegend();

        prevRect = rectTransform.rect;
        layoutDirty = false;
    }

    void UpdateLegend()
    {
        if (displayLegend)
        {
            if (legend == null)
            {
                var rt = GraphUtils.CreateUIObject("Legend", graphingArea);
                legend = rt.gameObject.AddComponent<Legend>();
                rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
            }
            legend.gameObject.SetActive(true);

            legend.Clear();
            foreach (var sg in subGraphs)
            {
                legend.Add(sg.name, sg.color);
            }
        }
    }

    void RefreshHistogram()
    {
        float width = graphingArea.rect.width;
        float height = graphingArea.rect.height;

        float maxValue = 0; 
        foreach (var graph in subGraphs)
        {
            if (graph.data != null)
            {
                for (int i = 0; i < graph.data.Length; i++)
                {
                    maxValue = Mathf.Max(maxValue, graph.data[i].value);
                }
            }
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

        for (int graph_id = 0; graph_id < subGraphs.Count; graph_id++)
        {
            var graph = subGraphs[graph_id];
            if (graph.data == null) continue;

            for (int i = graph.binObjs.Count; i < graph.data.Length; i++)
            {
                // Create object
                var newBinObj = CreateBin(i);

                graph.binObjs.Add(newBinObj);
            }

            if (graph.binObjs.Count > graph.data.Length)
            {
                for (int i = graph.data.Length; i < graph.binObjs.Count; i++)
                {
                    // Remove objects
                    Destroy(graph.binObjs[i].rectRT.gameObject);
                }
                graph.binObjs.RemoveRange(graph.data.Length, graph.binObjs.Count - graph.data.Length);
            }

            float dx = width / graph.data.Length;

            for (int i = 0; i < graph.data.Length; i++)
            {
                float barWidth = (dx - padding) / subGraphs.Count;

                float t = Mathf.Clamp01(graph.data[i].value / maxValue);
                graph.binObjs[i].rectRT.localScale = Vector2.one;
                graph.binObjs[i].rectRT.anchoredPosition = new Vector2(dx * i + padding * 0.5f + barWidth * graph_id, 0);
                graph.binObjs[i].rectRT.sizeDelta = new Vector2(barWidth, height * t);

                graph.binObjs[i].rect.color = graph.color;
            }
        }

        if (axisY)
        {
            if (displayAxisY)
            {
                axisY.range = new Vector2(0, maxValue);
            }
            axisY.gameObject.SetActive(displayAxisY);
        }

        if (binLabels == null) binLabels = new List<BinLabel>();

        int binCount = subGraphs[0].data.Length;
        for (int i = binLabels.Count; i < binCount; i++)
        {
            // Create object
            var newBinLabel = CreateBinLabel(i);

            binLabels.Add(newBinLabel);
        }

        if (binLabels.Count > binCount)
        {
            for (int i = binCount; i < binLabels.Count; i++)
            {
                // Remove objects
                Destroy(binLabels[i].labelRT.gameObject);
            }
            binLabels.RemoveRange(binCount, binLabels.Count - binCount);
        }

        float tdx = width / binCount;
        for (int i = 0; i < binLabels.Count; i++)
        {
            float midPoint = (subGraphs[0].data[i].range.x + subGraphs[0].data[i].range.y) * 0.5f;
            binLabels[i].label.text = string.Format("{0:0.00}", midPoint);
            binLabels[i].labelRT.anchoredPosition = new Vector2(tdx * (i + 0.5f) + padding * 0.5f, 0);
        }

        if (legend)
        {
            legend.gameObject.SetActive(displayLegend);
        }
        else
        {
            if (displayLegend) layoutDirty = true;
        }

        dirty = false;
    }

    BinObj CreateBin(int index)
    {
        GameObject newObj = new GameObject();
        newObj.name = string.Format("Bin {0:000}", index);
        newObj.transform.SetParent(graphingArea.transform);

        GameObject newTextObj = new GameObject();
        newTextObj.name = string.Format("Bin Text {0:000}", index);
        newTextObj.transform.SetParent(newObj.transform);

        var newBinObj = new BinObj();
        newBinObj.rect = newObj.AddComponent<Image>();

        newBinObj.rectRT = newObj.GetComponent<RectTransform>();
        if (newBinObj.rectRT == null) newBinObj.rectRT = newObj.AddComponent<RectTransform>();
        newBinObj.rectRT.anchorMin = Vector2.zero;
        newBinObj.rectRT.anchorMax = Vector2.zero;
        newBinObj.rectRT.pivot = Vector2.zero;

        return newBinObj;
    }

    BinLabel CreateBinLabel(int index)
    {
        var rt = GraphUtils.CreateUIObject(string.Format("Bin Label {0:000}", index), graphingArea);

        var newBinLabel = new BinLabel();
        newBinLabel.labelRT = rt;
        newBinLabel.label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        newBinLabel.label.alignment = TextAlignmentOptions.Center;
        newBinLabel.label.alignment = TextAlignmentOptions.Top;
        newBinLabel.label.fontSize = fontSize;
        newBinLabel.label.color = textColor;

        newBinLabel.labelRT.anchorMin = new Vector2(0.0f, 0.0f);
        newBinLabel.labelRT.anchorMax = new Vector2(0.0f, 0.0f);
        newBinLabel.labelRT.pivot = new Vector2(0.5f, 1.0f);

        return newBinLabel;
    }

    public Vector2 GetRange()
    {
        if (useBinFixedRange) return binRange;

        var subgraph = subGraphs[binMainSubgraph];
        if (subgraph.data != null)
        {
            Vector2 r = new Vector2();
            r.x = subgraph.data[0].range.x;
            r.y = subgraph.data[subgraph.data.Length - 1].range.y;

            return r;
        }

        return Vector2.zero;
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
