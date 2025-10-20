using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public static class GamepadExtensions 
{
    public static Gamepad GetGamepad(this PlayerInput playerInput)
    {
        foreach (var device in playerInput.devices)
        {
            if (device is Gamepad pad)
            {
                return pad;
            }
        }
        return null;
    }

    public static void Vibrate(this MonoBehaviour component, Gamepad gamepad, float lo, float hi, float duration)
    {
        component.StartCoroutine(VibrateCR(gamepad, lo, hi, duration));
    }

    static IEnumerator VibrateCR(Gamepad gamepad, float lo, float hi, float duration)
    {
        gamepad.SetMotorSpeeds(lo, hi);
        yield return new WaitForSeconds(duration);
        gamepad.SetMotorSpeeds(0.0f, 0.0f);
    }
}
