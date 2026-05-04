using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        private Sprite          defaultCursor;
        [SerializeField] 
        private float           fadeTime = 0.15f;
        [SerializeField] 
        private bool            hwCursor = false;
        [SerializeField, HideIf(nameof(hwCursor))]
        private Transform       softCursor;
        [SerializeField]
        private bool            linkToUIGroups;
        [SerializeField] 
        private GameObject      defaultAttachedObject;
        [SerializeField, ShowIf(nameof(hasCanvas))] 
        private Camera          uiCamera;
        [SerializeField]
        private Hypertag        playerTag;
        [SerializeField]
        private PlayerInput     playerInput;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        private InputControl    returnAttachedObjectControl;
        [SerializeField]
        private bool            gamepadCursor;
        [SerializeField, ShowIf(nameof(gamepadCursor))]
        private float           gamepadCursorAcceleration;
        [SerializeField, ShowIf(nameof(gamepadCursor))]
        private float           gamepadCursorMaxVelocity;
        [SerializeField, InputPlayer(nameof(playerInput))]
        private InputControl    gamepadCursorControl;


        bool hasCanvas() => GetComponentInParent<Canvas>() != null;

        Canvas          topLevelCanvas;
        Image           cursorImage;
        RectTransform   rectTransform;
        CanvasGroup     canvasGroup;
        Vector2         defaultSize = Vector2.zero;
        Color           defaultColor = Color.white;
        GameObject      currentAttachedObject;

        Sprite          currentCursor;
        Color           currentColor;
        Vector2         currentSize;
        ICursorGrabData _cursorGrabData;
        Vector2         gamepadCursorVelocity;
        Vector2?        gamepadCursorPosition;
        bool            gamepadWasMoving;
        List<UIGroup>   allUIGroups;
        float           allUIGroupsRefreshTimer;

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
            if ((cursorImage == null) && (softCursor)) cursorImage = softCursor.GetComponent<Image>();
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

            if (playerTag)
            {
                var p = playerTag.FindFirst<PlayerInput>();
                if (p) playerInput = p;
            }

            if (((returnAttachedObjectControl.needPlayerInput) || ((gamepadCursor) && (gamepadCursorControl.needPlayerInput))) && (playerInput == null))
                playerInput = FindFirstObjectByType<PlayerInput>();

            returnAttachedObjectControl.playerInput = playerInput;
            gamepadCursorControl.playerInput = playerInput;

            if (defaultAttachedObject)
            {
                defaultAttachedObject.SetActive(false);
            }
        }

        public void SetDefaultCursor()
        {
            SetCursor(defaultCursor, defaultColor, defaultSize);
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
                var normalizedPivot = cursor.pivot / cursor.rect.size;
                cursorImage.rectTransform.pivot = normalizedPivot;
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
            else
            {
                Cursor.visible = false;
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

        public GameObject AttachPrefabToCursor(GameObject prefab)
        {
            if ((currentAttachedObject != null) && (currentAttachedObject != defaultAttachedObject))
            {
                Destroy(currentAttachedObject);
                currentAttachedObject = null;
            }

            if (prefab)
            {
                currentAttachedObject = Instantiate(prefab, transform);
            }

            return currentAttachedObject;
        }
        public void DetachFromCursor()
        {
            if ((currentAttachedObject == defaultAttachedObject) && (defaultAttachedObject != null))
            {
                defaultAttachedObject.SetActive(false);
            }
            else if (currentAttachedObject != null) 
            {
                Destroy(currentAttachedObject);
                currentAttachedObject = null;
            }
        }


        public GameObject AttachToCursor(Sprite displaySprite, Color displaySpriteColor)
        {
            if (defaultAttachedObject == null) return null;

            if (displaySprite == null)
            {
                defaultAttachedObject.SetActive(false);
                return defaultAttachedObject;
            }

            defaultAttachedObject.SetActive(true);

            Image image = defaultAttachedObject.GetComponent<Image>();
            if (image)
            {
                image.sprite = displaySprite;
                image.color = displaySpriteColor;
            }
            else
            {
                SpriteRenderer spriteRenderer = defaultAttachedObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer)
                {
                    spriteRenderer.sprite = displaySprite;
                    spriteRenderer.color = displaySpriteColor;
                }
            }

            currentAttachedObject = defaultAttachedObject;

            return defaultAttachedObject;
        }

        private void Update()
        {
            if (linkToUIGroups)
            {
                allUIGroupsRefreshTimer -= Time.unscaledDeltaTime;
                if (allUIGroupsRefreshTimer <= 0)
                {
                    allUIGroups = new(FindObjectsByType<UIGroup>(FindObjectsSortMode.None));
                    allUIGroupsRefreshTimer = 2.0f;
                }

                bool activate = false;
                bool enableGamepad = false;
                foreach (var uiGroup in allUIGroups)
                {
                    if ((uiGroup.uiEnable) && (uiGroup.enableMouseSupport))
                    {
                        activate = true;

                        if (uiGroup.enableGamepadCursor) enableGamepad = true;
                    }
                }

                SetCursor(activate);
                this.gamepadCursor = enableGamepad && activate;
            }

            if ((currentAttachedObject) && (currentAttachedObject.gameObject.activeInHierarchy))
            {
                RectTransform rt = currentAttachedObject.transform as RectTransform;
                if (rt)
                {
                    Camera c = (topLevelCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? (null) : (topLevelCanvas.worldCamera);
                    Vector2 cursorPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform.parent as RectTransform, InputControl.GetScreenMousePosition(), c, out cursorPos);
                    currentAttachedObject.transform.localPosition = cursorPos;
                }
                else
                {
                    var pt = uiCamera.ScreenToWorldPoint(InputControl.GetScreenMousePosition());
                    currentAttachedObject.transform.position = new Vector3(pt.x, pt.y, currentAttachedObject.transform.position.z);
                }
            }
            if ((softCursor) && (!hwCursor) && (softCursor.gameObject.activeInHierarchy))
            {
                RectTransform rt = softCursor.transform as RectTransform;
                if (rt)
                {
                    Camera c = (topLevelCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? (null) : (topLevelCanvas.worldCamera);
                    Vector2 cursorPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform.parent as RectTransform, InputControl.GetScreenMousePosition(), c, out cursorPos);
                    softCursor.transform.localPosition = cursorPos;
                }
                else
                {
                    var pt = uiCamera.ScreenToWorldPoint(InputControl.GetScreenMousePosition());
                    softCursor.transform.position = new Vector3(pt.x, pt.y, softCursor.transform.position.z);
                }
            }

            InputControl.ClearGamepadCursorMoved();
            if (gamepadCursor)
            {
                UpdateGamepadCursor();
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

        void UpdateGamepadCursor()
        {
            var input = gamepadCursorControl.GetAxis2();

            float screenScale = Screen.height;

            // Initialize tracked position from actual mouse on first use
            if (!gamepadCursorPosition.HasValue)
                gamepadCursorPosition = InputControl.GetScreenMousePosition();

            // Snap velocity to zero when reversing direction
            if (Mathf.Abs(input.x) > 0.01f)
            {
                if (input.x * gamepadCursorVelocity.x < 0f)
                    gamepadCursorVelocity.x = 0f;
                gamepadCursorVelocity.x += input.x * gamepadCursorAcceleration * screenScale * Time.unscaledDeltaTime;
            }
            else
            {
                gamepadCursorVelocity.x = 0f;
            }

            if (Mathf.Abs(input.y) > 0.01f)
            {
                if (input.y * gamepadCursorVelocity.y < 0f)
                    gamepadCursorVelocity.y = 0f;
                gamepadCursorVelocity.y += input.y * gamepadCursorAcceleration * screenScale * Time.unscaledDeltaTime;
            }
            else
            {
                gamepadCursorVelocity.y = 0f;
            }

            float maxSpeed = gamepadCursorMaxVelocity * screenScale;
            gamepadCursorVelocity.x = Mathf.Clamp(gamepadCursorVelocity.x, -maxSpeed, maxSpeed);
            gamepadCursorVelocity.y = Mathf.Clamp(gamepadCursorVelocity.y, -maxSpeed, maxSpeed);

            if (gamepadCursorVelocity.sqrMagnitude > 0.01f)
            {
                var pos = gamepadCursorPosition.Value + gamepadCursorVelocity * Time.unscaledDeltaTime;
                pos.x = Mathf.Clamp(pos.x, 0, Screen.width);
                pos.y = Mathf.Clamp(pos.y, 0, Screen.height);
                gamepadCursorPosition = pos;

                InputState.Change(Mouse.current.position, pos);
                InputState.Change(Mouse.current.delta, Vector2.zero);
                gamepadWasMoving = true;
                InputControl.SetGamepadCursorMoved();
            }
            else if (gamepadWasMoving)
            {
                // Sync the OS cursor to where the gamepad left it
                Mouse.current.WarpCursorPosition(gamepadCursorPosition.Value);
                InputState.Change(Mouse.current.delta, Vector2.zero);
                gamepadWasMoving = false;
            }

            // Sync tracked position if real mouse moves
            if (InputControl.HasMouseMovedThisFrame())
                gamepadCursorPosition = InputControl.GetScreenMousePosition();
        }

        public static CursorManager instance
        {
            get { return _instance; }
        }
    }
}
