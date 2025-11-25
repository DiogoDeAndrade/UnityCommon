using NaughtyAttributes;
using System;
using UC;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UC
{
    public class CursorManager : MonoBehaviour
    {
        public interface ICursorGrabData
        {
            public bool Return();
        }

        [SerializeField] 
        private Sprite     defaultCursor;
        [SerializeField] 
        private float      fadeTime = 0.15f;
        [SerializeField] 
        private bool       hwCursor = false;
        [SerializeField] 
        private GameObject attachedObject;
        [SerializeField, ShowIf(nameof(hasCanvas))] 
        private Camera     uiCamera;
        [SerializeField]
        private PlayerInput     playerInput;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        private InputControl    returnAttachedObjectControl;

        bool hasCanvas() => GetComponentInParent<Canvas>() != null;

        Canvas          topLevelCanvas;
        Image           cursorImage;
        RectTransform   rectTransform;
        CanvasGroup     canvasGroup;
        Vector2         defaultSize = Vector2.zero;
        Color           defaultColor = Color.white;

        Sprite          currentCursor;
        Color           currentColor;
        Vector2         currentSize;
        ICursorGrabData _cursorGrabData;

        static CursorManager _instance;

        public ICursorGrabData cursorGrabData
        {
            get { return _cursorGrabData; }
            set { _cursorGrabData = value; }
        }

        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else 
            { 
                Destroy(gameObject);
                return;
            }
        }

        void Start()
        {
            topLevelCanvas = GetComponentInParent<Canvas>();
            cursorImage = GetComponent<Image>();
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = transform as RectTransform;
            if ((canvasGroup) && (defaultCursor))
            {
                canvasGroup.FadeIn(fadeTime);
                defaultSize = rectTransform.sizeDelta;
                defaultColor = (cursorImage) ? (cursorImage.color) : (Color.white);

                SetCursor(defaultCursor, defaultColor, defaultSize);
                SetCursor(true);
            }

            if ((returnAttachedObjectControl.needPlayerInput) && (playerInput == null))
                playerInput = FindFirstObjectByType<PlayerInput>();
            returnAttachedObjectControl.playerInput = playerInput;
        }

        public void SetCursor(CursorDef def)
        {
            if (def != null)
                SetCursor(def.cursor, def.color, def.size);
            else
                SetCursor(null, defaultColor, defaultSize);
        }

        public void SetCursor(Sprite cursor, Color color, Vector2 size)
        {
            if (defaultCursor == null)
            {
                if (cursor)
                {
                    canvasGroup.FadeIn(fadeTime);
                }
                else
                {

                    canvasGroup.FadeOut(fadeTime);
                }
            }

            if ((cursor == null) && (defaultCursor))
            {
                cursor = defaultCursor;
                size = defaultSize;
                color = defaultColor;
            }

            if (cursorImage)
            {
                cursorImage.sprite = cursor;
                cursorImage.color = color;
            }
            if (size != Vector2.zero)
            {
                rectTransform.sizeDelta = size;
            }

            currentCursor = cursor;
            currentColor = color;
            currentSize = size;

            if (hwCursor)
            {
                if (currentCursor)
                {
                    Cursor.SetCursor(currentCursor.texture, new Vector2(currentCursor.pivot.x, currentCursor.rect.height - currentCursor.pivot.y), CursorMode.ForceSoftware);
                }
            }
        }

        public void SetCursor(bool show)
        {
            if (show) canvasGroup.FadeIn(fadeTime);
            else canvasGroup.FadeOut(fadeTime);

            if (hwCursor)
            {
                Cursor.visible = show;
            }
        }

        public GameObject AttachToCursor(Sprite displaySprite, Color displaySpriteColor)
        {
            if (attachedObject == null) return null;

            if (displaySprite == null)
            {
                attachedObject.SetActive(false);
                return attachedObject;
            }

            attachedObject.SetActive(true);

            Image image = attachedObject.GetComponent<Image>();
            if (image)
            {
                image.sprite = displaySprite;
                image.color = displaySpriteColor;
            }
            else
            {
                SpriteRenderer spriteRenderer = attachedObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer)
                {
                    spriteRenderer.sprite = displaySprite;
                    spriteRenderer.color = displaySpriteColor;
                }
            }

            return attachedObject;
        }

        private void Update()
        {
            if ((attachedObject) && (attachedObject.gameObject.activeInHierarchy))
            {
                RectTransform rt = attachedObject.transform as RectTransform;
                if (rt)
                {
                    Camera c = (topLevelCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? (null) : (topLevelCanvas.worldCamera);
                    Vector2 cursorPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform.parent as RectTransform, Input.mousePosition, c, out cursorPos);
                    attachedObject.transform.localPosition = cursorPos;
                }
                else
                {
                    var pt = uiCamera.ScreenToWorldPoint(Input.mousePosition);
                    attachedObject.transform.position = new Vector3(pt.x, pt.y, attachedObject.transform.position.z);
                }
            }

            if (returnAttachedObjectControl.IsDown())
            {
                if (_cursorGrabData != null)
                {
                    if (_cursorGrabData.Return())
                    {
                        _cursorGrabData = null;
                    }                    
                }
            }
        }

        public static CursorManager instance
        {
            get { return _instance; }
        }
    }
}
