using UnityEngine;
using NaughtyAttributes;
using TMPro;

public class BigTextScroll : MonoBehaviour
{
    public delegate void OnEndScroll();
    public event OnEndScroll onEndScroll;

    [SerializeField, ResizableTextArea]
    private string text;
    [SerializeField]
    private TextMeshProUGUI textPrefab;
    [SerializeField]
    private float           scrollSpeed;

    Vector3         originalPosition;
    RectTransform   rectTransform;
    RectTransform   lastRectTransform;

    void Start()
    {
        var lines = text.Split('\n', System.StringSplitOptions.None);
        foreach (var line in lines)
        {
            var tmp = Instantiate(textPrefab, transform);
            tmp.text = line;
            
            lastRectTransform = tmp.GetComponent<RectTransform>();
        }

        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        rectTransform.anchoredPosition = rectTransform.anchoredPosition + Vector2.up * scrollSpeed * Time.deltaTime;

        float maxY = lastRectTransform.anchoredPosition.y;

        if (rectTransform.anchoredPosition.y > (Mathf.Abs(maxY) + 150.0f))
        {
            onEndScroll?.Invoke();
        }
    }

    public void Reset()
    {
        rectTransform.anchoredPosition = originalPosition;
    }
}
