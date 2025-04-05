using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UC
{

    [Serializable]
    public class InputControl
    {
        public enum InputType { Axis = 0, Button = 1, Key = 2, NewInput = 3 };

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

        public PlayerInput playerInput { get => _playerInput; set { _playerInput = value; } }

        public float GetAxis()
        {
            float v = 0.0f;

            switch (type)
            {
                case InputType.Axis:
                    v = Input.GetAxis(axis);
                    break;
                case InputType.Button:
                    if ((!string.IsNullOrEmpty(buttonPositive)) && (Input.GetButton(buttonPositive))) v += 1.0f;
                    if ((!string.IsNullOrEmpty(buttonNegative)) && (Input.GetButton(buttonNegative))) v -= 1.0f;
                    break;
                case InputType.Key:
                    if ((keyPositive != KeyCode.None) && (Input.GetKey(keyPositive))) v += 1.0f;
                    if ((keyNegative != KeyCode.None) && (Input.GetKey(keyNegative))) v -= 1.0f;
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
                    throw (new NotImplementedException($"GetAxis2D with type={type}"));
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
                    ret = Mathf.Abs(Input.GetAxis(axis)) > 0.5f;
                    break;
                case InputType.Button:
                    if (!string.IsNullOrEmpty(buttonPositive)) ret = Input.GetButton(buttonPositive);
                    break;
                case InputType.Key:
                    if (keyPositive != KeyCode.None) ret = Input.GetKey(keyPositive);
                    break;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null) ret = action.IsPressed();
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
                    if (!string.IsNullOrEmpty(buttonPositive)) ret = Input.GetButtonDown(buttonPositive);
                    break;
                case InputType.Key:
                    if (keyPositive != KeyCode.None) ret = Input.GetKeyDown(keyPositive);
                    break;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null) ret = action.WasPressedThisFrame();
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
                    if (!string.IsNullOrEmpty(buttonPositive)) ret = Input.GetButtonUp(buttonPositive);
                    break;
                case InputType.Key:
                    if (keyPositive != KeyCode.None) ret = Input.GetKeyUp(keyPositive);
                    break;
                case InputType.NewInput:
                    if (action == null) RefreshAction();
                    if (action != null) ret = action.WasReleasedThisFrame();
                    break;
                default:
                    break;
            }

            return ret;
        }

        void RefreshAction()
        {
            if (_playerInput == null) Debug.LogWarning($"Trying to fetch axis {inputAction}, but player input is not set!");
            else
            {
                if (playerInput.actions == null)
                {
                    Debug.LogWarning($"Player input has no control set!");
                }
                else
                {
                    action = playerInput.actions.FindAction(inputAction);
                    if (action == null)
                    {
                        Debug.LogWarning($"Action '{inputAction}' not found in PlayerInput's InputActionAsset {playerInput.actions.name}.");
                    }
                    else
                    {
                        isVec2 = action.expectedControlType == nameof(Vector2);
                    }
                }
            }
        }

        public void Test()
        {

            if (action == null) RefreshAction();
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
        All = Axis | Button | Key | NewInput
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