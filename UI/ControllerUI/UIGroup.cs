using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UC
{

    public class UIGroup : MonoBehaviour
    {
        [SerializeField]
        protected int playerId;
        [SerializeField]
        protected bool enableOnStart = true;
        [SerializeField]
        protected float moveCooldown = 0.1f;
        [SerializeField]
        protected PlayerInput playerInput;
        [SerializeField, InputPlayer(nameof(playerInput))]
        protected InputControl horizontalControl;
        [SerializeField, InputPlayer(nameof(playerInput))]
        protected InputControl verticalControl;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        protected InputControl interactControl;
        [SerializeField] AudioClip moveSnd;
        [SerializeField] AudioClip selectSnd;

        [SerializeField] protected BaseUIControl initialControl;
        [SerializeField] protected bool _useUnscaledTime = false;
        [SerializeField] protected bool _enableMouseSupport = false;

        protected float                 cooldownTimer;
        protected BaseUIControl         _selectedControl;
        protected bool                  selectedFromMouse;
        protected int                   mouseSkipFrames = 4;
        protected bool                  _verticalReset = true;
        protected bool                  _horizontalReset = true;
        protected bool                  _uiEnable = true;
        protected bool                  forceMouseUpdate = false;
        protected GraphicRaycaster      graphicRaycaster;
        protected List<RaycastResult>   raycasterResults = new();

        public bool enableMouseSupport => _enableMouseSupport;

        public BaseUIControl selectedControl
        {
            get { return _selectedControl; }
            set
            {
                var prevControl = _selectedControl;
                if (_selectedControl != null) _selectedControl.NotifyDeselect();
                _selectedControl = value;
                if (_selectedControl != null) _selectedControl.NotifySelect(prevControl);
            }
        }

        public bool uiEnable => _uiEnable;
        public bool useUnscaledTime => _useUnscaledTime;

        protected virtual void Start()
        {
            if ((playerId != -1) && (playerInput))
            {
                MasterInputManager.SetupInput(playerId, playerInput);
            }

            if (playerInput != null)
            {
                horizontalControl.playerInput = playerInput;
                verticalControl.playerInput = playerInput;
                interactControl.playerInput = playerInput;
            }

            if (initialControl)
            {
                selectedControl = initialControl;
            }

            _uiEnable = enableOnStart;

            if (_enableMouseSupport)
            {
                graphicRaycaster = GetComponent<GraphicRaycaster>();
                if (graphicRaycaster == null) graphicRaycaster = GetComponentInParent<GraphicRaycaster>();

                raycasterResults = new();
            }
        }

        void Update()
        {
            if (!_uiEnable) return;

            if (mouseSkipFrames > 0) mouseSkipFrames--;
            if ((_enableMouseSupport) && ((mouseSkipFrames <= 0) || (forceMouseUpdate)))
            {
                if ((InputControl.HasMouseMovedThisFrame()) || (forceMouseUpdate))
                {
                    var ctrl = GetControlOnPointer();
                    if (forceMouseUpdate)
                    {
                        if (ctrl)
                        {
                            selectedControl = ctrl;
                            selectedFromMouse = (selectedControl != null);
                        }
                        forceMouseUpdate = false;
                    }
                    else
                    {
                        selectedControl = ctrl;
                        selectedFromMouse = (selectedControl != null);
                    }
                }
            }

            if (_selectedControl)
            {
                if (cooldownTimer > 0) cooldownTimer -= GetDeltaTime();
                if ((verticalControl.needPlayerInput) && (playerInput == null)) return;
                if ((horizontalControl.needPlayerInput) && (playerInput == null)) return;

                if (cooldownTimer <= 0.0f)
                {
                    float dy = verticalControl.GetAxis();
                    float dx = horizontalControl.GetAxis();
                    if ((Mathf.Abs(dx) > 0.1f) &&
                        (Mathf.Abs(dy) > 0.1f))
                    {
                        // Diagonal, not allowed
                    }
                    else
                    {
                        if (_verticalReset)
                        {
                            if (dy < -0.5f)
                            {
                                var next = NextSelectable(_selectedControl, c => c.navDown); 
                                _selectedControl = (next) ? (next) : (_selectedControl);
                                cooldownTimer = moveCooldown;
                                _verticalReset = false;
                                if (moveSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveSnd);
                                selectedFromMouse = false;
                            }
                            else if (dy > 0.5f)
                            {
                                var next = NextSelectable(_selectedControl, c => c.navUp);
                                _selectedControl = (next) ? (next) : (_selectedControl);
                                cooldownTimer = moveCooldown;
                                _verticalReset = false;
                                if (moveSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveSnd);
                                selectedFromMouse = false;
                            }
                        }
                        else
                        {
                            if (Mathf.Abs(dy) < 0.1f) _verticalReset = true;
                        }

                        if (Mathf.Abs(dx) < 0.1f) _horizontalReset = true;
                        else
                        {
                            _selectedControl?.MoveHorizontal(dx, _horizontalReset);
                            _horizontalReset = false;
                        }
                    }

                    if (interactControl.IsDown())
                    {
                        bool interact = true;
                        if (interactControl.WasDownFromPointerThisFrame())
                        {
                            if (!_enableMouseSupport) interact = false;
                            else
                            {
                                // Was a mouse click or similar, so we should use the mouse to select the control
                                var ctrl = GetControlOnPointer();
                                if (ctrl)
                                {
                                    _selectedControl = ctrl;
                                    selectedFromMouse = true;
                                }
                                else
                                {
                                    interact = false;
                                }
                            }
                        }

                        if (interact)
                        {
                            if (selectSnd) SoundManager.PlaySound(SoundType.SecondaryFX, selectSnd);
                            _selectedControl?.Interact();
                            OnSelect();
                        }
                    }
                    else if ((interactControl.IsPressed()) && (_selectedControl) && (_selectedControl.isContinuous))
                    {
                        _selectedControl?.Interact();
                        OnSelect();
                    }
                }
            }
        }

        BaseUIControl GetControlOnPointer()
        {
            var screenPos = InputControl.GetScreenMousePosition();

            raycasterResults.Clear();
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            graphicRaycaster.Raycast(data, raycasterResults);
            if (raycasterResults.Count > 0)
            {
                foreach (var ray in raycasterResults)
                {
                    BaseUIControl ctrl = ray.gameObject.GetComponent<BaseUIControl>();
                    if (ctrl == null) ctrl = ray.gameObject.GetComponentInParent<BaseUIControl>();
                    if (ctrl)
                    {
                        if (ctrl.isSelectable)
                        {
                            return selectedControl = ctrl;
                        }
                    }
                }
            }

            return null;
        }

        private BaseUIControl NextSelectable(BaseUIControl from, Func<BaseUIControl, BaseUIControl> step)
        {
            if (from == null) return null;
            var start = from;
            var cur = step(from);
            int hops = 0;

            while (cur != null && !cur.isSelectable && cur != start && hops < 128)
            {
                cur = step(cur);
                hops++;
            }

            // If we found a selectable control, use it; otherwise, stay where we are.
            return (cur != null && cur.isSelectable) ? cur : from;
        }

        public void SetControl(BaseUIControl control)
        {
            _selectedControl = control;
            cooldownTimer = moveCooldown;
            _verticalReset = false;
            if (moveSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveSnd);
        }

        public void EnableUI(bool value)
        {
            if ((_enableMouseSupport) && (value))
            {
                // Need to launch a coroutine, because I'm enabling this and I want to skip a frame before trying to get the 
                // element at the cursor, because the canvas group might not be active yet
                StartCoroutine(WaitFrameAndForceMousePointerUpdate());
            }
            _uiEnable = value;
            var uiControls = GetComponentsInChildren<BaseUIControl>();
            foreach (var uiControl in uiControls)
            {
                if (value) uiControl.NotifyEnable();
                else uiControl.NotifyDisable();
            }

            if (value)
            {
                selectedControl = initialControl;
                cooldownTimer = moveCooldown;
            }
        }

        IEnumerator WaitFrameAndForceMousePointerUpdate()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            while (!canvasGroup.interactable)
            {
                yield return null;
            }
            forceMouseUpdate = true;
        }

        protected virtual void OnSelect()
        {

        }

        public float GetDeltaTime() => (_useUnscaledTime) ? (Time.unscaledDeltaTime) : (Time.deltaTime);

        public void SetPlayerInput(PlayerInput input)
        {
            playerInput = input;
            horizontalControl.playerInput = playerInput;
            verticalControl.playerInput = playerInput;
            interactControl.playerInput = playerInput;
        }
    }
}
