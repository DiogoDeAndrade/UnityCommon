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
            followOwner = src.followOwner;
        }
        public CombatTextDef(Color color)
        {
            this.startColor = color;
            this.endColor = color.ChangeAlpha(0.0f);
            this.totalTime = CombatTextManager.defaultTime;

            fadeTime = CombatTextManager.defaultTime * 0.5f;
            this.scaleModifier = 1.0f;
            this.speedModifier = 1.0f;
            this.followOwner = true;
        }
        public CombatTextDef(Color color, float totalTime)
        {
            this.startColor = color;
            this.endColor = color.ChangeAlpha(0.0f);
            this.totalTime = totalTime;

            fadeTime = totalTime * 0.5f;
            this.scaleModifier = 1.0f;
            this.speedModifier = 1.0f;
            this.followOwner = true;
        }
        public CombatTextDef(Color startColor, Color endColor, float totalTime)
        {
            this.startColor = startColor;
            this.endColor = endColor;
            this.totalTime = totalTime;

            fadeTime = totalTime * 0.5f;
            this.scaleModifier = 1.0f;
            this.speedModifier = 1.0f;
            this.followOwner = true;
        }

        public Color startColor;
        public Color endColor;
        public float fadeTime;
        public float totalTime;
        public float scaleModifier;
        public float speedModifier;
        // Should this text ride along with the object that spawned it? Independent of the manager's cameraFollow: this one is about tracking a moving character,
        // that one is about staying put over the battlefield while the view scrolls.
        public bool followOwner;

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

        public CombatTextDef ChangeFollowOwner(bool f)
        {
            return new CombatTextDef(this)
            {
                followOwner = f
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
            public Transform        anchorTransform;
            public Vector3          spawnWorldPosition;
            public Vector3          followOffset;
            public Vector2          basePoint;
            public Vector2          drift;
            public RectTransform    textTransform;
            public TextMeshProUGUI  textObject;
        }

        [SerializeField]
        private TextMeshProUGUI  textPrefab;
        [SerializeField]
        private float           _defaultTime = 1.0f;
        [SerializeField]
        private Vector2         movementVector;
        [SerializeField]
        private float           fadeRate = 1;
        [SerializeField]
        private bool            cameraFollow = true;
        [SerializeField] 
        private Camera          uiCamera;

        List<TextElem> textList;
        Canvas canvas;
        RectTransform rectTransform;
        Vector2 screenToCanvasSizes;
        CanvasScaler canvasScaler;

        static int suppressCount;

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

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                // Anything mid-suppression has no manager left to un-suppress it on the way out.
                suppressCount = 0;
            }
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
                    tElem.drift += movementVector * tElem.def.speedModifier * Time.deltaTime;
                    tElem.textTransform.anchoredPosition = ComputeAnchoredPosition(tElem) + tElem.drift;
                }
            }

            textList.RemoveAll((t) => t.elapsedTime >= t.def.totalTime);
        }

        Vector2 ComputeAnchoredPosition(TextElem tElem)
        {
            bool follows = tElem.def.followOwner && (tElem.anchorTransform != null);

            // Nothing to recompute, and this runs per text per frame - skip the projections entirely.
            if ((!follows) && (!cameraFollow)) return tElem.basePoint;

            if (follows)
            {
                tElem.followOffset = tElem.anchorTransform.position - tElem.spawnWorldPosition;
            }

            var worldPosition = tElem.spawnWorldPosition + tElem.followOffset;

            // Follow the camera panning
            if (cameraFollow) return WorldToLocal(worldPosition) - GetAnchorOffset(tElem.textTransform);

            // Camera-independent: only the anchor's own movement is allowed to shift the baked point,
            // so convert the delta rather than the absolute position.
            return tElem.basePoint + (WorldToLocal(worldPosition) - WorldToLocal(tElem.spawnWorldPosition));
        }

        Vector2 WorldToLocal(Vector3 worldPosition)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);

            // Convert the screen point to local coordinates in the RectTransform
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, uiCamera, out Vector2 localPoint);

            return localPoint;
        }

        // ScreenPointToLocalPointInRectangle hands back a point in the container's local space, whose origin is the container's pivot. anchoredPosition, on
        // the other hand, is measured from the spawned text's own anchor. Those two only coincide when the text prefab is anchored dead centre, and nothing
        // says it has to be - anchor it bottom-centre and every number lands half a screen too low. So work out where the text's anchor actually sits in
        // container-local space, and convert between the two whatever the prefab happens to be anchored to.
        //
        // This is only about the anchor. The prefab's own pivot is left alone: it decides how the text sits over that point, which is a look, not a bug.
        Vector2 GetAnchorOffset(RectTransform textTransform)
        {
            var anchorCenter = (textTransform.anchorMin + textTransform.anchorMax) * 0.5f;
            var rect = rectTransform.rect;

            return new Vector2(rect.xMin + rect.width * anchorCenter.x, rect.yMin + rect.height * anchorCenter.y);
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

            tmp.anchorTransform = (ctSpawnPoint == null) ? (ownerObject.transform) : (ctSpawnPoint.transform);
            tmp.spawnWorldPosition = position;
            tmp.followOffset = Vector3.zero;
            tmp.drift = Vector2.zero;
            tmp.basePoint = WorldToLocal(position) - GetAnchorOffset(tmp.textTransform);

            tmp.textTransform.anchoredPosition = tmp.basePoint;

            textList.Add(tmp);

            return tmp;
        }

        TextElem FindNumberTextOfColor(Color c, GameObject ownerObject, Vector2 offset)
        {
            foreach (var tElem in textList)
            {
                if (tElem.isNumber)
                {
                    // ReferenceEquals, not ==: Unity's operator reports a destroyed object as equal to null, so two dead characters' numbers would
                    // compare equal to each other and the second one would merge into the first one's text.
                    if ((tElem.def.startColor == c) && (ReferenceEquals(tElem.ownerObject, ownerObject)))
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
            if (!CanSpawn(ownerObject)) return;
            instance._SpawnText(ownerObject, Vector2.zero, text, def);
        }
        public static void SpawnText(GameObject ownerObject, Vector2 offset, string text, CombatTextDef def)
        {
            if (!CanSpawn(ownerObject)) return;
            instance._SpawnText(ownerObject, offset, text, def);
        }

        public static void SpawnText(GameObject ownerObject, float value, string text, CombatTextDef def)
        {
            if (!CanSpawn(ownerObject)) return;
            instance._SpawnText(ownerObject, Vector2.zero, value, text, def);
        }

        public static void SpawnText(GameObject ownerObject, Vector2 offset, float value, string text, CombatTextDef def)
        {
            if (!CanSpawn(ownerObject)) return;
            instance._SpawnText(ownerObject, offset, value, text, def);
        }

        static bool CanSpawn(GameObject ownerObject) => (instance != null) && (ownerObject != null) && (suppressCount == 0);

        public static float defaultTime => instance?._defaultTime ?? 1.0f;

        // Mute combat text for a stretch of code that moves resources around for bookkeeping rather than for something that happened in the fiction - spawning a unit,
        // restoring a save. Prefer the Suppress() scope; the explicit pair is here for when the block is not a lexical one.
        // Use it like this:
        // using (CombatTextManager.Suppress())
        // {
        //      healthHandler?.SetMaxResource(maxHealth);
        //      healthHandler?.ResetResource();
        // }
        public static bool isSuppressed => suppressCount > 0;

        public static void BeginSuppress() => suppressCount++;

        public static void EndSuppress() => suppressCount = Mathf.Max(0, suppressCount - 1);

        public static SuppressScope Suppress()
        {
            BeginSuppress();
            return new SuppressScope();
        }

        public struct SuppressScope : IDisposable
        {
            public void Dispose() => EndSuppress();
        }
    }
}
