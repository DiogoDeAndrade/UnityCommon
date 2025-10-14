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

    void Start()
    {
        text = GetComponent<TextMeshPro>();

        startScale = transform.localScale;
        startPos = transform.localPosition;
        startColor = text.color;

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
            if (colorMultiply) text.color = startColor * colorOverTime.Evaluate(t);
            else text.color = colorOverTime.Evaluate(t);
        }
        if (sizeOverTime != null) transform.localScale = startScale * sizeOverTime.Evaluate(t);

        transform.position = startPos + moveDir * moveDelta * ((moveAnimation == null) ? (t) : (moveAnimation.Evaluate(t)));
    }
}

