using UnityEngine;
using UnityEngine.UI.Extensions;
using TMPro;

static class GraphUtils
{
    public static UILineRenderer CreateLineRenderer(string name, Color color, RectTransform parent)
    {
        var rt = NewGameObjectUI(name, parent);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = parent.sizeDelta;

        var lineRenderer = rt.gameObject.AddComponent<UILineRenderer>();
        lineRenderer.color = color;
        lineRenderer.LineJoins = UILineRenderer.JoinType.Miter;
        lineRenderer.LineThickness = 2;

        return lineRenderer;
    }

    public static TextMeshProUGUI CreateTextRenderer(string name, Color color, float defaultTextSize, RectTransform parent)
    {
        var rt = NewGameObjectUI(name, parent);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(1, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.color = color;
        text.fontSize = defaultTextSize;
        text.alignment = TextAlignmentOptions.Center;
        text.alignment = TextAlignmentOptions.Right;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        return text;
    }

    public static RectTransform NewGameObjectUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();

        return rt;
    }
}
