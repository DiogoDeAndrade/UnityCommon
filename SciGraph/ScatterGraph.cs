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

public class ScatterGraph : MonoBehaviour
{
    public enum DotShape { Pixel, Rect, Circle, Plus };

    public bool             background = true;
    [ShowIf("background")]
    public Color            backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.85f);
    public bool             displayLegend = false;
    public Color            textColor = Color.yellow;
    public float            titleTextSize = 14;
    public DotShape         dotShape = DotShape.Pixel;
    [ShowIf("dotShapeNeedsSize")]
    public int              dotShapeSize = 1;

    [BoxGroup("Graph Area")]
    public Vector2  minGraphArea = new Vector2(0.15f, 0.15f);
    [BoxGroup("Graph Area")]
    public Vector2  maxGraphArea = new Vector2(0.95f, 0.85f);
    [BoxGroup("Graph Area")]
    public Color    graphBackgroundColor = new Color(0,0,0,0);
    [BoxGroup("Graph Area")]
    public bool     fixedLimit;
    [ShowIf("fixedLimit"), BoxGroup("Graph Area")]
    public Vector2  rangeX;
    [ShowIf("fixedLimit"), BoxGroup("Graph Area")]
    public Vector2  rangeY;
    [ShowIf("fixedLimit"), BoxGroup("Graph Area")]
    public bool     allowExpandX;
    [ShowIf("fixedLimit"), BoxGroup("Graph Area")]
    public bool     allowExpandY;
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

    [BoxGroup("Performance"), Range(1, 8)]
    public          int     resolutionScale = 1;

    string _title;
    bool                dirty;
    bool                layoutDirty;
    TextMeshProUGUI     titleElement;
    RectTransform       rectTransform;
    Rect                prevRect;
    RectTransform       graphingArea;
    Axis                axisX;
    Axis                axisY;
    Legend              legend;
    Texture2D           texture;
    Color[]             bitmap;
    RawImage            rawImage;

    class Subgraph
    {
        public string           name;
        public List<Vector2>    data;
        public Color            color;
    }

    List<Subgraph>         subGraphs;

    bool dotShapeNeedsSize => (dotShape == DotShape.Rect) || (dotShape == DotShape.Circle) || (dotShape == DotShape.Plus);

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

        var rawImageRT = GraphUtils.CreateUIObject("Plot", graphingArea);
        rawImageRT.anchorMin = new Vector2(0, 0);
        rawImageRT.anchorMax = new Vector2(1, 1);
        rawImageRT.pivot = new Vector2(0.5f, 0.5f);
        rawImageRT.offsetMin = rawImageRT.offsetMax = Vector2.zero;
        rawImage = rawImageRT.gameObject.AddComponent<RawImage>();

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
            RefreshPlot();
        }
    }

    public int AddSubgraph(Color color, string name)
    {
        var subgraph = new Subgraph();
        subgraph.name = name;
        subgraph.color = color;

        if (subGraphs == null) subGraphs = new List<Subgraph>();
        subGraphs.Add(subgraph);

        UpdateLegend();

        return subGraphs.Count - 1;
    }

    public void SetData(int subgraph_id, List<Vector2> data)
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

    void RefreshPlot()
    {
        int width = (int)graphingArea.rect.width;
        int height = (int)graphingArea.rect.height;

        int texWidth = width * resolutionScale;
        int texHeight = height * resolutionScale;

        if ((texture == null) || (texture.width != texWidth) || (texture.height != texHeight))
        {
            texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            bitmap = new Color[texture.width * texture.height];

            rawImage.texture = texture;
        }

        float minX = float.MaxValue;
        float maxX = -float.MaxValue;
        float minY = float.MaxValue;
        float maxY = -float.MaxValue;

        foreach (var graph in subGraphs)
        {
            if (graph.data != null)
            {
                foreach (var dataPoint in graph.data)
                {
                    minX = Mathf.Min(minX, dataPoint.x);
                    maxX = Mathf.Max(maxX, dataPoint.x);
                    minY = Mathf.Min(minY, dataPoint.y);
                    maxY = Mathf.Max(maxY, dataPoint.y);
                }
            }
        }

        if (fixedLimit)
        {
            if (allowExpandX)
            {
                if (minX < rangeX.x) rangeX.x = minX;
                if (maxX > rangeX.y) rangeX.y = maxX;
            }
            if (allowExpandY)
            {
                if (minY < rangeY.x) rangeY.x = minY;
                if (maxY > rangeY.y) rangeY.y = maxY;
            }
            minX = rangeX.x;
            minY = rangeY.x;
            maxX = rangeX.y;
            maxY = rangeY.y;
        }

        for (int i = 0; i < texWidth * texHeight; i++)
        {
            bitmap[i] = graphBackgroundColor;
        }

        for (int graph_id = 0; graph_id < subGraphs.Count; graph_id++)
        {
            var graph = subGraphs[graph_id];
            if (graph.data == null) continue;

            foreach (var dataPoint in graph.data)
            {
                // Draw point in scatter plot
                int u = (int)(texWidth * ((dataPoint.x - minX) / (maxX - minX)));
                if ((u < 0) || (u >= texWidth)) continue;
                int v = (int)(texHeight * ((dataPoint.y - minY) / (maxY - minY)));
                if ((v < 0) || (v >= texHeight)) continue;

                switch (dotShape)
                {
                    case DotShape.Pixel:
                        bitmap[u + texWidth * v] = graph.color;
                        break;
                    case DotShape.Rect:
                        for (int vv = Mathf.Max(0, v - dotShapeSize); vv <= Mathf.Min(texHeight - 1, v + dotShapeSize); vv++)
                        {
                            for (int uu = Mathf.Max(0, u - dotShapeSize); uu <= Mathf.Min(texWidth - 1, u + dotShapeSize); uu++)
                            {
                                bitmap[uu + texWidth * vv] = graph.color;
                            }
                        }
                        break;
                    case DotShape.Circle:
                        float sqrSize = dotShapeSize * dotShapeSize;
                        for (int vv = Mathf.Max(0, v - dotShapeSize - 1); vv <= Mathf.Min(texHeight - 1, v + dotShapeSize + 1); vv++)
                        {
                            for (int uu = Mathf.Max(0, u - dotShapeSize - 1); uu <= Mathf.Min(texWidth- 1, u + dotShapeSize + 1); uu++)
                            {
                                float dx = uu - u;
                                float dy = vv - v;
                                if ((dx * dx + dy * dy) < sqrSize)
                                {
                                    bitmap[uu + texWidth * vv] = graph.color;
                                }
                            }
                        }
                        break;
                    case DotShape.Plus:
                        for (int uu = Mathf.Max(0, u - dotShapeSize); uu <= Mathf.Min(texWidth - 1, u + dotShapeSize); uu++) bitmap[uu + texWidth * v] = graph.color;
                        for (int vv = Mathf.Max(0, v - dotShapeSize); vv <= Mathf.Min(texHeight - 1, v + dotShapeSize); vv++) bitmap[u + texWidth * vv] = graph.color;
                        break;
                }
            }
        }

        texture.SetPixels(bitmap);
        texture.Apply(true, false);

        if (axisX)
        {
            if (displayAxisX)
            {
                axisX.range = new Vector2(minX, maxX);
            }
            axisY.gameObject.SetActive(displayAxisY);
        }
        if (axisY)
        {
            if (displayAxisY)
            {
                axisY.range = new Vector2(minY, maxY);
            }
            axisY.gameObject.SetActive(displayAxisY);
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

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Handles.Label(transform.position, "ScatterGraph: " + name);
        }
#endif
    }

}
