using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CombatTextManager : MonoBehaviour
{
    static CombatTextManager instance;

    class TextElem
    {
        public Color            startColor;
        public Color            endColor;
        public float            elapsedTime;
        public float            totalTime;
        public float            speedModifier;
        public float            number;
        public bool             isNumber;
        public string           baseText;
        public GameObject       ownerObject;
        public RectTransform    textTransform;
        public TextMeshProUGUI  textObject;
    }

    public TextMeshProUGUI textPrefab;
    public float           defaultTime = 1.0f;
    public Vector2         movementVector;
    public float           fadeRate = 1;

    [SerializeField] private Camera uiCamera;

    List<TextElem> textList;
    Canvas         canvas;
    RectTransform  rectTransform;
    Vector2        screenToCanvasSizes;
    CanvasScaler   canvasScaler;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        textList = new List<TextElem>();
        canvas = GetComponentInParent<Canvas>();
        canvasScaler = canvas.GetComponent<CanvasScaler>();
        rectTransform = transform as RectTransform;
        if (uiCamera == null)
        {
            uiCamera = canvas.worldCamera;
            if (uiCamera == null)
            {
                uiCamera = Camera.main;
            }
        }

        screenToCanvasSizes.x = canvasScaler.referenceResolution.x / Screen.width;
        screenToCanvasSizes.y = canvasScaler.referenceResolution.y / Screen.height;
    }

    void Update()
    {
        foreach (var tElem in textList)
        {
            tElem.elapsedTime += Time.deltaTime;
            
            if (tElem.elapsedTime >= tElem.totalTime)
            {
                Destroy(tElem.textObject.gameObject);
            }
            else
            {
                float t = Mathf.Pow(tElem.elapsedTime / tElem.totalTime, fadeRate);
                Color c = Color.Lerp(tElem.startColor, tElem.endColor, t);

                tElem.textObject.color = c;

                tElem.textTransform.anchoredPosition += movementVector * tElem.speedModifier * Time.deltaTime;
            }
        }

        textList.RemoveAll((t) => t.elapsedTime >= t.totalTime);    
    }

    TextElem NewText(GameObject ownerObject, Vector2 offset)
    {
        var tmp = new TextElem();

        tmp.number = 0.0f;
        tmp.ownerObject = ownerObject;
        tmp.elapsedTime = 0.0f;
        tmp.textObject = Instantiate(textPrefab, transform);
        tmp.textTransform = tmp.textObject.GetComponent<RectTransform>();

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, ownerObject.transform.position + offset.xy0());

        // Convert the screen point to local coordinates in the RectTransform
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, uiCamera, out Vector2 localPoint);

        tmp.textTransform.anchoredPosition = localPoint;

        textList.Add(tmp);

        return tmp;
    }

    TextElem FindNumberTextOfColor(Color c, GameObject ownerObject, Vector2 offset)
    {
        foreach (var tElem in textList)
        {
            if (tElem.isNumber)
            {
                if ((tElem.startColor == c) && (tElem.ownerObject == ownerObject))
                {
                    return tElem;
                }
            }
        }

        return NewText(ownerObject, offset);
    }

    void _SpawnText(GameObject ownerObject, Vector2 offset, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        TextElem newText = NewText(ownerObject, offset);
        newText.isNumber = false;
        newText.baseText = text;
        newText.startColor = startColor;
        newText.endColor = endColor;
        newText.speedModifier = moveSpeedModifier;
        newText.totalTime = (time > 0.0f) ? (time) : (defaultTime);

        newText.textObject.text = newText.baseText;
        newText.textObject.color = startColor;
    }

    void _SpawnText(GameObject ownerObject, float value, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        TextElem newText = FindNumberTextOfColor(startColor, ownerObject, Vector2.zero);
        newText.isNumber = true;
        newText.number += value;
        newText.baseText = text;
        newText.startColor = startColor;
        newText.endColor = endColor;
        newText.speedModifier = moveSpeedModifier;
        newText.totalTime = (time > 0.0f) ? (time) : (defaultTime);

        newText.textObject.text = string.Format(text, newText.number);
        newText.textObject.color = startColor;
    }

    void _SpawnText(GameObject ownerObject, Vector2 offset, float value, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        TextElem newText = FindNumberTextOfColor(startColor, ownerObject, offset);
        newText.isNumber = true;
        newText.number += value;
        newText.baseText = text;
        newText.startColor = startColor;
        newText.endColor = endColor;
        newText.speedModifier = moveSpeedModifier;
        newText.totalTime = (time > 0.0f) ? (time) : (defaultTime);

        newText.textObject.text = string.Format(text, newText.number);
        newText.textObject.color = startColor;
    }

    public static void SpawnText(GameObject ownerObject, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        instance._SpawnText(ownerObject, Vector2.zero, text, startColor, endColor, time, moveSpeedModifier);
    }
    public static void SpawnText(GameObject ownerObject, Vector2 offset, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        instance._SpawnText(ownerObject, offset, text, startColor, endColor, time, moveSpeedModifier);
    }

    public static void SpawnText(GameObject ownerObject, float value, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        instance._SpawnText(ownerObject, Vector2.zero, value, text, startColor, endColor, time, moveSpeedModifier);
    }

    public static void SpawnText(GameObject ownerObject, Vector2 offset, float value, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        instance._SpawnText(ownerObject, offset, value, text, startColor, endColor, time, moveSpeedModifier);
    }
}
