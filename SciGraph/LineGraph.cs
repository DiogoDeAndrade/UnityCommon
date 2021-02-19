using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UI.Extensions;
using NaughtyAttributes;

public class LineGraph : MonoBehaviour
{
    public          bool    background = true;
    [ShowIf("background")]
    public          Color   backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.75f);
    public          bool    displayCurrentValue = false;
    public          bool    displayCaption = false;
    public          Color   textColor = Color.white;
    public          float   labelTextSize = 10;
    public          float   titleTextSize = 14;

    [BoxGroup("Graph Area")]
    public          Vector2 minGraphArea = new Vector2(0.15f, 0.15f);
    [BoxGroup("Graph Area")]
    public          Vector2 maxGraphArea = new Vector2(0.95f, 0.8f);
    [BoxGroup("Graph Area")]
    public bool     fixedLimits;
    [ShowIf("fixedLimits"), BoxGroup("Graph Area")]
    public Vector2  limitMin = new Vector2(0,0);
    [ShowIf("fixedLimits"), BoxGroup("Graph Area")]
    public Vector2  limitMax = new Vector2(100, 100);
    [ShowIf("fixedLimits"), BoxGroup("Graph Area")]
    public bool     allowExpandX = false;
    [ShowIf("fixedLimits"), BoxGroup("Graph Area")]
    public bool     allowExpandY = false;

    [BoxGroup("Axis")]
    public          bool    displayAxisX = true;
    [BoxGroup("Axis")]
    public          bool    displayAxisY = true;
    [BoxGroup("Axis")]
    public          float   axisTextSize = 12;
    [BoxGroup("Axis")]
    public          string  labelAxisX = "AxisX";
    [BoxGroup("Axis")]
    public          string  labelAxisY = "AxisY";

    string          _title;
    bool            dirty;
    Vector2         min = new Vector2(float.MaxValue, float.MaxValue);
    Vector2         max = new Vector2(-float.MaxValue, -float.MaxValue);
    Vector2         dataRange;
    Axis            axisX;
    Axis            axisY;
    RectTransform   rectTransform;
    RectTransform   graphingArea;
    TextMeshProUGUI titleElement;

    struct SubLineGraph
    {
        public string           name;
        public UILineRenderer   lineRenderer;
        public List<Vector2>    dataPoints;
        public UILineRenderer   currentValueLine;
        public TextMeshProUGUI  currentValueLabel;
        public RectTransform    currentValueLabelRT;
    }

    List<SubLineGraph>    subGraphs;

    public string title
    {
        get { return _title; }
        set { if (titleElement != null) titleElement.text = value; _title = value; }
    }

    void Awake()
    {
        if (background)
        {
            Image img = gameObject.AddComponent<Image>();
            img.color = backgroundColor;
        }

        rectTransform = GetComponent<RectTransform>();

        titleElement = GraphUtils.CreateTextRenderer("Title", textColor, titleTextSize, rectTransform);
        titleElement.alignment = TextAlignmentOptions.Center;
        titleElement.alignment = TextAlignmentOptions.Top;
        RectTransform rt = titleElement.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1.0f);

        if (titleElement) titleElement.text = _title;

        graphingArea = GraphUtils.NewGameObjectUI("LineContainer", rectTransform);
        graphingArea.anchorMin = new Vector2(0, 0);
        graphingArea.anchorMax = new Vector2(1, 1);
        graphingArea.pivot = new Vector2(0.5f, 0.5f);
        graphingArea.offsetMin = graphingArea.offsetMax = Vector2.zero;
    }

    // Update is called once per frame
    void Update()
    {
        if (dirty)
        {
            RefreshLines();
            dirty = false;
        }
    }

    public int AddSubgraph(Color color, string name)
    {
        var subgraph = new SubLineGraph();
        subgraph.name = name;
        subgraph.dataPoints = new List<Vector2>();

        subgraph.lineRenderer = GraphUtils.CreateLineRenderer(name, color, graphingArea);
        subgraph.currentValueLine = GraphUtils.CreateLineRenderer("Current " + name, color.ChangeAlpha(0.25f), graphingArea);
        subgraph.currentValueLabel = GraphUtils.CreateTextRenderer("Value" + name, color, labelTextSize, graphingArea);
        subgraph.currentValueLabelRT = subgraph.currentValueLabel.GetComponent<RectTransform>();

        if (subGraphs == null) subGraphs = new List<SubLineGraph>();
        subGraphs.Add(subgraph);

        return subGraphs.Count - 1;
    }

    public void AddDataPoint(int subgraph_id, float x, float y)
    {
        subGraphs[subgraph_id].dataPoints.Add(new Vector2(x, y));
        min.x = Mathf.Min(min.x, x);
        min.y = Mathf.Min(min.y, y);
        max.x = Mathf.Max(max.x, x);
        max.y = Mathf.Max(max.y, y);
        dataRange = max - min;
        if (dataRange.x == 0) dataRange.x = 1;
        if (dataRange.y == 0) dataRange.y = 1;
        dirty = true;
    }

    void RefreshLines()
    {
        // Create graph area
        graphingArea.sizeDelta = new Vector2(rectTransform.rect.width * (maxGraphArea.x - minGraphArea.x),
                                             rectTransform.rect.height * (maxGraphArea.y - minGraphArea.y));
        graphingArea.anchoredPosition = new Vector2(rectTransform.rect.width * minGraphArea.x,
                                                    rectTransform.rect.height * minGraphArea.y);
        graphingArea.offsetMin = new Vector2(rectTransform.rect.width * minGraphArea.x,
                                             rectTransform.rect.height * minGraphArea.y);
        graphingArea.offsetMax = new Vector2(-rectTransform.rect.width * (1 - maxGraphArea.x),
                                             -rectTransform.rect.height * (1 - maxGraphArea.y));

        // Get limits
        Vector2 displayMin = min;
        Vector2 displayMax = max;

        if (fixedLimits)
        {
            displayMin = limitMin;
            displayMax = limitMax;

            if (allowExpandX)
            {
                limitMax.x = Mathf.Max(limitMax.x, max.x);
            }
            if (allowExpandY)
            {
                limitMax.y = Mathf.Max(limitMax.y, max.y);
            }
        }

        foreach (var sg in subGraphs)
        {
            sg.dataPoints.Sort((v1, v2) => v1.x.CompareTo(v2.x));

            float width = sg.lineRenderer.rectTransform.rect.width;
            float height = sg.lineRenderer.rectTransform.rect.height;

            Vector2 offset = new Vector2(-width * 0.5f, -height * 0.5f);

            List<Vector2> points = new List<Vector2>();
            for (int i = 0; i < sg.dataPoints.Count; i++)
            {
                var dataPoint = sg.dataPoints[i];

                if (dataPoint.x < displayMin.x) continue;
                if (dataPoint.x > displayMax.x) break;

                points.Add(new Vector2(offset.x + width * (dataPoint.x - displayMin.x) / (displayMax.x - displayMin.x), 
                                       offset.y + height * (dataPoint.y - displayMin.y) / (displayMax.y - displayMin.y)));
            }

            sg.lineRenderer.Points = points.ToArray();

            if (displayCurrentValue)
            {
                sg.currentValueLine.enabled = true;
                sg.currentValueLabel.enabled = true;

                if (sg.dataPoints.Count > 0)
                {
                    float valueY = sg.dataPoints[sg.dataPoints.Count - 1].y;
                    float y = offset.y + height * (valueY - displayMin.y) / (displayMax.y - displayMin.y);

                    sg.currentValueLine.Points = new Vector2[]
                    {
                        new Vector2(offset.x, y),
                        new Vector2(offset.x + width, y),
                    };

                    sg.currentValueLabel.text = string.Format("{0:0.0}", valueY);
                    sg.currentValueLabelRT.anchoredPosition = new Vector2(-5, y);
                }
            }
            else
            {
                sg.currentValueLine.enabled = false;
                sg.currentValueLabel.enabled = false;
            }
        }

        if (displayAxisX)
        {
            if (axisX == null)
            {
                RectTransform rt = GraphUtils.NewGameObjectUI("AxisX", graphingArea);

                axisX = rt.gameObject.AddComponent<Axis>();
                axisX.orientation = Axis.Orientation.Horizontal;
                axisX.textColor = textColor;
                axisX.textSize = axisTextSize;                
            }
            axisX.range = new Vector2(displayMin.x, displayMax.x);
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
                RectTransform rt = GraphUtils.NewGameObjectUI("AxisY", graphingArea);

                axisY = rt.gameObject.AddComponent<Axis>();
                axisY.orientation = Axis.Orientation.Vertical;
                axisY.textColor = textColor;
                axisY.textSize = axisTextSize;
            }
            axisY.range = new Vector2(displayMin.y, displayMax.y);
            axisY.labelAxis = labelAxisY;
        }
        else
        {
            if (axisY != null) axisY.gameObject.SetActive(false);
        }
    }
}
