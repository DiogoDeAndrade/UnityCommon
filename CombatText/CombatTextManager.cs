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

    List<TextElem> textList;
    Canvas         canvas;
    Camera         uiCamera;
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
        uiCamera = canvas.worldCamera;
        if (uiCamera == null)
        {
            uiCamera = Camera.main;
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

    TextElem NewText(GameObject ownerObject)
    {
        var tmp = new TextElem();

        tmp.number = 0.0f;
        tmp.ownerObject = ownerObject;
        tmp.elapsedTime = 0.0f;
        tmp.textObject = Instantiate(textPrefab, transform);
        tmp.textTransform = tmp.textObject.GetComponent<RectTransform>();

        Vector3 pos2d = uiCamera.WorldToScreenPoint(ownerObject.transform.position);
        pos2d.x *= screenToCanvasSizes.x;
        pos2d.y *= screenToCanvasSizes.y;

        tmp.textTransform.anchoredPosition = pos2d;

        textList.Add(tmp);

        return tmp;
    }

    TextElem FindNumberTextOfColor(Color c, GameObject ownerObject)
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

        return NewText(ownerObject);
    }

    void _SpawnText(GameObject ownerObject, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        TextElem newText = NewText(ownerObject);
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
        TextElem newText = FindNumberTextOfColor(startColor, ownerObject);
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
        instance._SpawnText(ownerObject, text, startColor, endColor, time, moveSpeedModifier);
    }

    public static void SpawnText(GameObject ownerObject, float value, string text, Color startColor, Color endColor, float time = 0.0f, float moveSpeedModifier = 1.0f)
    {
        instance._SpawnText(ownerObject, value, text, startColor, endColor, time, moveSpeedModifier);
    }
}
