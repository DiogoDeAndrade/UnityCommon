using UnityEngine;
using NaughtyAttributes;
using TMPro;
using UnityEngine.InputSystem;

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
    [SerializeField]
    private PlayerInput     playerInput;
    [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
    protected InputControl backControl;

    Vector3 originalPosition;
    RectTransform   rectTransform;
    RectTransform   lastRectTransform;

    void Start()
    {
        backControl.playerInput = playerInput;

        var lines = text.Split('\n', System.StringSplitOptions.None);
        foreach (var line in lines)
        {
            var tmp = Instantiate(textPrefab, transform);
            if (line == "")
                tmp.text = "<color=#FF000000>||||</color>";
            else 
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

        if ((rectTransform.anchoredPosition.y > (Mathf.Abs(maxY) + 150.0f)) || (backControl.IsDown()))
        {
            onEndScroll?.Invoke();
        }
    }

    public void Reset()
    {
        rectTransform.anchoredPosition = originalPosition;
    }
}
