using UnityEngine;

public class UICopySize : MonoBehaviour
{
    [SerializeField] private RectTransform sourceRectTransform;

    RectTransform rectTransform;
    void Start()
    {
        rectTransform = transform as RectTransform;
    }

    // Update is called once per frame
    void Update()
    {
        rectTransform.sizeDelta = sourceRectTransform.sizeDelta;
    }
}
