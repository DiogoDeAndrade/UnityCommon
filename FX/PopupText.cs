using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class PopupText : MonoBehaviour
{
    [SerializeField] 
    private float           duration = 1.0f;
    [SerializeField]
    private Gradient        colorOverTime;
    [SerializeField]
    private bool            colorMultiply;
    [SerializeField]
    private AnimationCurve  sizeOverTime;
    [SerializeField]
    private float           moveDelta = 0.0f;
    [SerializeField]
    private AnimationCurve  moveAnimation;
    [SerializeField]
    private Vector3         moveDir = Vector3.up;
    [SerializeField]
    private bool            destroyOnEnd;

    Vector3         startScale;
    Vector3         startPos;
    Color           startColor;
    float           timer = 0.0f;
    TextMeshPro     text;
    TextMeshProUGUI textUI;
    RectTransform   rt;

    void Awake()
    {
        text = GetComponent<TextMeshPro>();
        textUI = GetComponent<TextMeshProUGUI>();
        rt = transform as RectTransform;

        startScale = transform.localScale;
        startPos = (text) ? (transform.localPosition) : (rt.anchoredPosition);
        startColor = (text) ? (text.color) : (textUI.color);

        UpdateVisuals();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            if (destroyOnEnd) Destroy(gameObject);
            return;
        }

        UpdateVisuals();
    }

    void UpdateVisuals()
    { 
        float t = Mathf.Clamp01(timer / duration);

        if (colorOverTime != null)
        {
            if (colorMultiply)
            {
                if (text) text.color = startColor * colorOverTime.Evaluate(t);
                if (textUI) textUI.color = startColor * colorOverTime.Evaluate(t);
            }
            else
            {
                if (text) text.color = colorOverTime.Evaluate(t);
                if (textUI) textUI.color = colorOverTime.Evaluate(t);
            }
        }
        if (sizeOverTime != null) transform.localScale = startScale * sizeOverTime.Evaluate(t);

        if (text)
        {
            transform.localPosition = startPos + moveDir * moveDelta * ((moveAnimation == null) ? (t) : (moveAnimation.Evaluate(t)));
        }
        else if (textUI)
        {
            var rt = transform as RectTransform;
            rt.anchoredPosition = startPos + moveDir * moveDelta * ((moveAnimation == null) ? (t) : (moveAnimation.Evaluate(t)));
        }
    }

    public void SetText(string s)
    {
        if (text) text.text = s;
        if (textUI) textUI.text = s;
    }
}

