using NaughtyAttributes;
using UnityEngine;

public class UICopySize : MonoBehaviour
{
    [SerializeField] 
    private RectTransform   sourceRectTransform;
    [SerializeField]
    private bool            padEnable;
    [SerializeField, ShowIf(nameof(padEnable))]
    private Vector2         pad = new Vector2(-1, -1);
    [SerializeField] 
    private bool            minSizeEnable;
    [SerializeField, ShowIf(nameof(minSizeEnable))]
    private Vector2         minSize = new Vector2(-1, -1);

    RectTransform rectTransform;
    void Start()
    {
        rectTransform = transform as RectTransform;
    }

    // Update is called once per frame
    void Update()
    {
        var s = sourceRectTransform.sizeDelta;

        if (padEnable)
        {
            if (pad.x > 0.0f) s.x += pad.x;
            if (pad.y > 0.0f) s.y += pad.y;
        }

        if (minSizeEnable)
        {
            if (minSize.x >= 0.0f) s.x = Mathf.Max(s.x, minSize.x);
            if (minSize.y >= 0.0f) s.y = Mathf.Max(s.y, minSize.y);
        }

        rectTransform.sizeDelta = s;
    }
}
