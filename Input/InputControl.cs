using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class InputControl
{
    public enum InputType { Axis = 0, Button = 1, Key = 2, NewInput = 3 };

    [SerializeField] 
    private InputType   type;
    [SerializeField, InputAxis]
    private string      axis = "Horizontal";
    [SerializeField, InputAxis]
    private string      buttonPositive = "Right";
    [SerializeField, InputAxis]
    private string      buttonNegative = "Left";
    [SerializeField]
    private KeyCode     keyPositive = KeyCode.RightArrow;
    [SerializeField]
    private KeyCode     keyNegative = KeyCode.LeftArrow;
    [SerializeField]
    private string      inputAction = "";

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
                                
                InputAction action = new();
                break;
            default:
                break;
        }

        return v;
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
                break;
            default:
                break;
        }

        return true;
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
                break;
            default:
                break;
        }

        return true;
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
                break;
            default:
                break;
        }

        return true;
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