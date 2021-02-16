using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.UI.Extensions;
using NaughtyAttributes;

public class LineGraph : MonoBehaviour
{
    public Color    lineColor = Color.red;

    [BoxGroup("FixedLimits"), Label("Enable")]
    public bool     fixedLimits;
    [ShowIf("fixedLimits"), BoxGroup("FixedLimits")]
    public Vector2  limitMin = new Vector2(0,0);
    [ShowIf("fixedLimits"), BoxGroup("FixedLimits")]
    public Vector2  limitMax = new Vector2(100, 100);
    [ShowIf("fixedLimits"), BoxGroup("FixedLimits")]
    public bool     allowExpandX = false;
    [ShowIf("fixedLimits"), BoxGroup("FixedLimits")]
    public bool     allowExpandY = false;

    [SerializeField] TextMeshProUGUI titleElement;
    [SerializeField] UILineRenderer  lineRenderer;
    [SerializeField] TextMeshProUGUI labelMinX;
    [SerializeField] TextMeshProUGUI labelMaxX;
    [SerializeField] TextMeshProUGUI labelMinY;
    [SerializeField] TextMeshProUGUI labelMaxY;
    [SerializeField] TextMeshProUGUI labelAxisX;
    [SerializeField] TextMeshProUGUI labelAxisY;

    string          _title;
    bool            dirty;
    List<Vector2>   dataPoints;
    Vector2         min = new Vector2(float.MaxValue, float.MaxValue);
    Vector2         max = new Vector2(-float.MaxValue, -float.MaxValue);
    Vector2         dataRange;

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
        dataPoints = new List<Vector2>();

        if (titleElement) titleElement.text = _title;
        lineRenderer.color = lineColor;
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

    public void AddDataPoint(float x, float y)
    {
        dataPoints.Add(new Vector2(x, y));
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
        dataPoints.Sort((v1, v2) => v1.x.CompareTo(v2.x));

        float width = lineRenderer.rectTransform.sizeDelta.x;
        float height = lineRenderer.rectTransform.sizeDelta.y;

        Vector2 offset = new Vector2(-width * 0.5f, -height * 0.5f);

        List<Vector2>   points = new List<Vector2>();
        Vector2         displayMin = min;
        Vector2         displayMax = max;

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
        else
        {

        }

        for (int i = 0; i < dataPoints.Count; i++)
        {
            var dataPoint = dataPoints[i];

            if (dataPoint.x < displayMin.x) continue;
            if (dataPoint.x > displayMax.x) break;

            points.Add(new Vector2(offset.x + width * (dataPoint.x - displayMin.x) / (displayMax.x - displayMin.x), offset.y + height * (dataPoint.y - displayMin.y) / (displayMax.y - displayMin.y)));
        }

        lineRenderer.Points = points.ToArray();

        if (labelMinX) labelMinX.text = "" + (int)displayMin.x;
        if (labelMaxX) labelMaxX.text = "" + (int)displayMax.x;
        if (labelMinY) labelMinY.text = "" + (int)displayMin.y;
        if (labelMaxY) labelMaxY.text = "" + (int)displayMax.y;
    }
}
