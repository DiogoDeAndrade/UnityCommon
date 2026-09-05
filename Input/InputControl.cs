using NaughtyAttributes;
using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputSystemControl = UnityEngine.InputSystem.InputControl;

namespace UC
{

    [Serializable]
    public partial class InputControl
    {
        public enum InputType { Axis = 0, Button = 1, Key = 2, NewInput = 3, AnyInputEvent = 4, MousePosition = 5, None = 6 };

        [SerializeField]
        private InputType _type;
        public InputType type => _type;
        [SerializeField, InputAxis]
        private string axis = "Horizontal";
        [SerializeField, InputAxis]
        private string buttonPositive = "Right";
        [SerializeField, InputAxis]
        private string buttonNegative = "Left";
        [SerializeField]
        private KeyCode keyPositive = KeyCode.RightArrow;
        [SerializeField]
        private KeyCode keyNegative = KeyCode.LeftArrow;
        [SerializeField]
        private string inputAction = "";

        InputAction action;
        PlayerInput _playerInput;
        bool isVec2;
        float prevValue;


        [AutoStaticsCleanup] private static bool _gamepadCursorMovedThisFrame;
        public static void SetGamepadCursorMoved() => _gamepadCursorMovedThisFrame = true;
        public static void ClearGamepadCursorMoved() => _gamepadCursorMovedThisFrame = false;

        public PlayerInput playerInput { get => _playerInput; set { _playerInput = value; RefreshAction(); } }
        public bool needPlayerInput => _type == InputType.NewInput;

        public float GetAxis()
        {
            float v = 0.0f;

            switch (type)
            {
                case InputType.Axis:
                    v = GetAxis(axis);
                    break;
                case InputType.Button:
                    if ((!string.IsNullOrEmpty(buttonPositive)) && (GetButton(buttonPositive))) v += 1.0f;
                    if ((!string.IsNullOrEmpty(buttonNegative)) && (GetButton(buttonNegative))) v -= 1.0f;
                    break;
                case InputType.Key:
                    if ((keyPositive != KeyCode.None) && (GetKey(keyPositive))) v += 1.0f;
                    if ((keyNegative != KeyCode.None) && (GetKey(keyNegative))) v -= 1.0f;
                    break;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null)
                    {
                        if (isVec2) v = action.ReadValue<Vector2>().x;
                        else v = action.ReadValue<float>();

                        if (v * prevValue < 0)
                        {
                            if (Mathf.Abs(v) > 0.5f)
                            {
                                prevValue = v;
                            }
                            else
                            {
                                v = 0.0f;
                            }
                        }
                        else
                        {
                            prevValue = v;
                        }
                    }
                    break;
                case InputType.AnyInputEvent:
                case InputType.MousePosition:
                    throw (new NotImplementedException($"GetAxis with type={type}"));
                case InputType.None:
                    return 0.0f;
                default:
                    break;
            }

            return v;
        }

        public Vector2 GetAxis2()
        {
            switch (type)
            {
                case InputType.Axis:
                case InputType.Button:
                case InputType.Key:
                case InputType.AnyInputEvent:
                    throw (new NotImplementedException($"GetAxis2 with type={type}"));
                case InputType.None:
                    return Vector2.zero;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null)
                    {
                        if (isVec2)
                        {
                            return action.ReadValue<Vector2>();
                        }
                        else return new Vector2(action.ReadValue<float>(), 0.0f);
                    }
                    break;
                case InputType.MousePosition:
                    return GetMousePixelPosition();
                default:
                    break;
            }

            return Vector2.zero;
        }

        public bool IsPressed()
        {
            bool ret = false;

            switch (type)
            {
                case InputType.Axis:
                    ret = Mathf.Abs(GetAxis(axis)) > 0.5f;
                    break;
                case InputType.Button:
                    if (!string.IsNullOrEmpty(buttonPositive)) ret = GetButton(buttonPositive);
                    break;
                case InputType.Key:
                    if (keyPositive != KeyCode.None) ret = GetKey(keyPositive);
                    break;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null) ret = action.IsPressed();
                    break;
                case InputType.AnyInputEvent:
                    ret = isAnyInputPressed;
                    break;
                case InputType.MousePosition:
                    ret = isAnyInputPressed;
                    break;
                case InputType.None:
                    ret = false; 
                    break;
                default:
                    break;
            }

            return ret;
        }

        public bool IsDown()
        {
            bool ret = false;

            switch (type)
            {
                case InputType.Axis:
                    ret = false;
                    break;
                case InputType.Button:
                    if (!string.IsNullOrEmpty(buttonPositive)) ret = GetButtonDown(buttonPositive);
                    break;
                case InputType.Key:
                    if (keyPositive != KeyCode.None) ret = GetKeyDown(keyPositive);
                    break;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null) ret = action.WasPressedThisFrame();
                    break;
                case InputType.AnyInputEvent:
                    ret = isAnyInputPressed;
                    break;
                case InputType.None:
                    ret = false;
                    break;
                default:
                    break;
            }

            return ret;
        }

        public bool IsUp()
        {
            bool ret = false;

            switch (type)
            {
                case InputType.Axis:
                    ret = false;
                    break;
                case InputType.Button:
                    if (!string.IsNullOrEmpty(buttonPositive)) ret = GetButtonUp(buttonPositive);
                    break;
                case InputType.Key:
                    if (keyPositive != KeyCode.None) ret = GetKeyUp(keyPositive);
                    break;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null) ret = action.WasReleasedThisFrame();
                    break;
                case InputType.AnyInputEvent:
                    throw (new NotImplementedException($"IsUp with type={type}"));
                case InputType.None:
                    ret = false;
                    break;
                default:
                    break;
            }

            return ret;
        }

        void RefreshAction()
        {
            if (_type != InputType.NewInput) return;

            if (_playerInput == null) DebugHelpers.LogWarning($"Trying to fetch axis {inputAction}, but player input is not set!");
            else
            {
                if (playerInput.actions == null)
                {
                    DebugHelpers.LogWarning($"Player input has no control set!");
                }
                else
                {
                    action = playerInput.actions.FindAction(inputAction);
                    if (action == null)
                    {
                        DebugHelpers.LogWarning($"Action '{inputAction}' not found in PlayerInput's InputActionAsset {playerInput.actions.name}.");
                    }
                    else
                    {
                        isVec2 = action.expectedControlType == nameof(Vector2);
                    }
                }
            }
        }

        public bool IsMouseLike()
        {
            // Only makes sense for New Input System actions
            if (_type != InputType.NewInput)
                return false;

            if (action == null)
                RefreshAction();

            if (action == null)
                return false;

            // Prefer the currently active control (last one that actuated the action)
            var control = action.activeControl;
            if (control != null)
                return IsPointerControl(control);

            // Fallback: look at all bound controls for this action
            foreach (var c in action.controls)
            {
                if (IsPointerControl(c))
                    return true;
            }

            return false;
        }

        public bool WasDownFromPointerThisFrame()
        {
            if (_type != InputType.NewInput)
                return false;

            if (action == null)
                RefreshAction();

            if (action == null)
                return false;

            // Only meaningful if the action was pressed this frame
            if (!action.WasPressedThisFrame())
                return false;

            // The control that most recently actuated this action
            var c = action.activeControl;
            if (c == null)
                return false;

            return IsPointerControl(c);
        }

        public bool WasDownFromKeyboardThisFrame()
        {
            if (_type != InputType.NewInput)
                return false;

            if (action == null)
                RefreshAction();

            if (action == null)
                return false;

            if (!action.WasPressedThisFrame())
                return false;

            var c = action.activeControl;
            if (c == null)
                return false;

            return c.device is Keyboard;
        }

        public InputDevice GetDownDeviceThisFrame()
        {
            if (_type != InputType.NewInput)
                return null;

            if (action == null)
                RefreshAction();

            if (action == null)
                return null;

            if (!action.WasPressedThisFrame())
                return null;

            return action.activeControl?.device;
        }

        private static bool IsPointerControl(InputSystemControl control)
        {
            if (control == null)
                return false;

            var device = control.device;

            // Mouse, touch, pen, or generic pointer device
            return device is Mouse
                || device is Touchscreen
                || device is Pen
                || device is Pointer;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool isAnyInputPressed
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                    return Input.anyKeyDown;
#else
                foreach (var device in InputSystem.devices)
                {
                    foreach (var control in device.allControls)
                    {
                        if (control is ButtonControl button && button.wasPressedThisFrame)
                        {
                            return true;
                        }
                    }
                }

                return false;
#endif
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static float GetAxis(string axisName)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxis(axisName);
#else
            throw new NotImplementedException("InputControl.GetAxis not implemented if legacy input manager is turned off!");
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static Vector2 GetMousePixelPosition()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            Vector3 p = Input.mousePosition;
            return new Vector2(p.x, p.y);
#elif ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return default;
            }

            // position is a Vector2 in screen pixels, bottom-left origin (same convention as legacy)
            return mouse.position.ReadValue();
#else
        return default;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool HasMouseMovedThisFrame()
        {
            // This is for gamepad-based movement (triggered from CursorManager)
            if (_gamepadCursorMovedThisFrame) return true;

#if ENABLE_LEGACY_INPUT_MANAGER
            // This is frame-based and does not require manual position caching
            return Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0f ||
                   Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0f;
#elif ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
                return false;

            // delta is reset every frame
            return mouse.delta.ReadValue() != Vector2.zero;
#else
        return false;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static Vector2 GetMouseScrollDelta()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta;
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetMouseScrollDelta not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetKey(KeyCode keyCode)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(keyCode);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetKey not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetKeyUp(KeyCode keyCode)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(keyCode);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetKeyUp not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetKeyDown(KeyCode keyCode)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetKeyDown not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetMouseButton(int mouseButton)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(mouseButton);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetMouseButton not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetMouseButtonDown(int mouseButton)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(mouseButton);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetMouseButtonDown not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetMouseButtonUp(int mouseButton)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(mouseButton);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetMouseButtonUp not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetButton(string buttonName)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetButton(buttonName);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetButton not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetButtonUp(string buttonName)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetButtonUp(buttonName);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetButtonUp not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ProjectAuditor", "PAR0027", Justification = "InputControl deliberately supports the legacy Input Manager")]
        public static bool GetButtonDown(string buttonName)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetButtonDown(buttonName);
#elif ENABLE_INPUT_SYSTEM
            throw new NotImplementedException("InputControl.GetButtonDown not implemented if legacy input manager is turned off!");
#else
        return false;
#endif
        }
    }

    [Flags]
    public enum AllowInput
    {
        None = 0,
        Axis = 1 << 0,
        Button = 1 << 1,
        Key = 1 << 2,
        NewInput = 1 << 3,
        AnyInputEvent = 1 << 4,
        MousePosition = 1 << 5,
        NoInput = 1 << 6,
        All = Axis | Button | Key | NewInput | AnyInputEvent | MousePosition | NoInput
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class AllowInputAttribute : PropertyAttribute
    {
        public AllowInput AllowedInputs { get; private set; }

        public AllowInputAttribute(AllowInput allowedInputs = AllowInput.All)
        {
            AllowedInputs = allowedInputs;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class InputButtonAttribute : PropertyAttribute
    {
        // No additional properties required for this attribute
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class InputPlayerAttribute : PropertyAttribute
    {
        public string PlayerInputFieldName { get; }

        public InputPlayerAttribute(string playerInputFieldName)
        {
            PlayerInputFieldName = playerInputFieldName;
        }
    }
}