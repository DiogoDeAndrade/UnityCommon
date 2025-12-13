using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

namespace UC
{
    [Serializable]
    public struct CombatTextDef
    {
        public CombatTextDef(CombatTextDef src)
        {
            startColor = src.startColor;
            endColor = src.endColor;
            fadeTime = src.fadeTime;
            totalTime = src.totalTime;
            scaleModifier = src.scaleModifier;
            speedModifier = src.speedModifier;
        }
        public CombatTextDef(Color color)
        {
            this.startColor = color;
            this.endColor = color.ChangeAlpha(0.0f);
            this.totalTime = CombatTextManager.defaultTime;

            fadeTime = CombatTextManager.defaultTime * 0.5f;
            this.scaleModifier = 1.0f;
            this.speedModifier = 1.0f;
        }
        public CombatTextDef(Color color, float totalTime)
        {
            this.startColor = color;
            this.endColor = color.ChangeAlpha(0.0f);
            this.totalTime = totalTime;

            fadeTime = totalTime * 0.5f;
            this.scaleModifier = 1.0f;
            this.speedModifier = 1.0f;
        }
        public CombatTextDef(Color startColor, Color endColor, float totalTime)
        {
            this.startColor = startColor;
            this.endColor = endColor;
            this.totalTime = totalTime;

            fadeTime = totalTime * 0.5f;
            this.scaleModifier = 1.0f;
            this.speedModifier = 1.0f;
        }

        public Color startColor;
        public Color endColor;
        public float fadeTime;
        public float totalTime;
        public float scaleModifier;
        public float speedModifier;

        public CombatTextDef ChangeColor(Color c)
        {
            return new CombatTextDef(this)
            {
                startColor = c,
                endColor = c.ChangeAlpha(0.0f)
            };
        }

        public CombatTextDef ChangeScale(float s)
        {
            return new CombatTextDef(this)
            {
                scaleModifier = s
            };
        }
    }

    public class CombatTextManager : MonoBehaviour
    {
        static CombatTextManager instance;

        class TextElem
        {
            public CombatTextDef    def;
            public float            elapsedTime;
            public float            number;
            public bool             isNumber;
            public string           baseText;
            public GameObject       ownerObject;
            public RectTransform    textTransform;
            public TextMeshProUGUI  textObject;
        }

        public TextMeshProUGUI  textPrefab;
        public float            _defaultTime = 1.0f;
        public Vector2          movementVector;
        public float            fadeRate = 1;

        [SerializeField] private Camera uiCamera;

        List<TextElem> textList;
        Canvas canvas;
        RectTransform rectTransform;
        Vector2 screenToCanvasSizes;
        CanvasScaler canvasScaler;

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

                if (tElem.elapsedTime >= tElem.def.totalTime)
                {
                    Destroy(tElem.textObject.gameObject);
                }
                else
                {
                    float t = (tElem.def.totalTime == tElem.def.fadeTime) ? 0.0f : (Mathf.Pow(Mathf.Clamp01((tElem.elapsedTime - tElem.def.fadeTime) / (tElem.def.totalTime - tElem.def.fadeTime)), fadeRate));

                    Color c = Color.Lerp(tElem.def.startColor, tElem.def.endColor, t);

                    tElem.textObject.color = c;
                    tElem.textTransform.localScale = Vector3.one * Mathf.Lerp(tElem.def.scaleModifier, 1.0f, Mathf.Clamp01(2.0f * tElem.elapsedTime / tElem.def.totalTime));
                    tElem.textTransform.anchoredPosition += movementVector * tElem.def.speedModifier * Time.deltaTime;
                }
            }

            textList.RemoveAll((t) => t.elapsedTime >= t.def.totalTime);
        }

        TextElem NewText(GameObject ownerObject, Vector2 offset)
        {
            var tmp = new TextElem();

            tmp.number = 0.0f;
            tmp.ownerObject = ownerObject;
            tmp.elapsedTime = 0.0f;
            tmp.textObject = Instantiate(textPrefab, transform);
            tmp.textTransform = tmp.textObject.GetComponent<RectTransform>();

            var ctSpawnPoint = ownerObject.GetComponentInChildren<CombatTextSpawnPoint>();
            var position = (ctSpawnPoint == null) ? (ownerObject.transform.position + offset.xy0()) : (ctSpawnPoint.transform.position);

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, position);

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
                    if ((tElem.def.startColor == c) && (tElem.ownerObject == ownerObject))
                    {
                        return tElem;
                    }
                }
            }

            return NewText(ownerObject, offset);
        }

        void _SpawnText(GameObject ownerObject, Vector2 offset, string text, CombatTextDef def)
        {
            TextElem newText = NewText(ownerObject, offset);
            newText.isNumber = false;
            newText.baseText = text;
            newText.def = def;

            newText.textObject.text = newText.baseText;
            newText.textObject.color = def.startColor;
        }

        void _SpawnText(GameObject ownerObject, float value, string text, CombatTextDef def)
        {
            TextElem newText = FindNumberTextOfColor(def.startColor, ownerObject, Vector2.zero);
            newText.isNumber = true;
            newText.number += value;
            newText.baseText = text;
            newText.def = def;

            newText.textObject.text = string.Format(text, newText.number);
            newText.textObject.color = def.startColor;
        }

        void _SpawnText(GameObject ownerObject, Vector2 offset, float value, string text, CombatTextDef def)
        {
            TextElem newText = FindNumberTextOfColor(def.startColor, ownerObject, offset);
            newText.isNumber = true;
            newText.number += value;
            newText.baseText = text;
            newText.def = def;

            newText.textObject.text = string.Format(text, newText.number);
            newText.textObject.color = def.startColor;
        }

        public static void SpawnText(GameObject ownerObject, string text, CombatTextDef def)
        {
            instance._SpawnText(ownerObject, Vector2.zero, text, def);
        }
        public static void SpawnText(GameObject ownerObject, Vector2 offset, string text, CombatTextDef def)
        {
            instance._SpawnText(ownerObject, offset, text, def);
        }

        public static void SpawnText(GameObject ownerObject, float value, string text, CombatTextDef def)
        {
            instance._SpawnText(ownerObject, Vector2.zero, value, text, def);
        }

        public static void SpawnText(GameObject ownerObject, Vector2 offset, float value, string text, CombatTextDef def)
        {
            instance._SpawnText(ownerObject, offset, value, text, def);
        }

        public static float defaultTime => instance?._defaultTime ?? 1.0f;
    }
}