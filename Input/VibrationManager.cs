using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class VibrationManager : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;

    Gamepad     gamepad;
    Coroutine   vibrationCR;

    // Update is called once per frame
    void Update()
    {
        if (gamepad == null)
        {
            gamepad = playerInput.GetGamepad();
        }
    }

    private void OnDestroy()
    {
        StopVibration();
    }

    public void StopVibration()
    {
        gamepad?.SetMotorSpeeds(0.0f, 0.0f);
    }

    public void Vibrate(float lo, float hi, float time)
    {
        if (vibrationCR != null)
        {
            StopVibration();
            StopCoroutine(vibrationCR);
            vibrationCR = null;
        }

        if (gamepad != null)
        {
            vibrationCR = StartCoroutine(VibrateCR(lo, hi, time));
        }
    }

    IEnumerator VibrateCR(float lo, float hi, float time)
    {
        gamepad?.SetMotorSpeeds(lo, hi);
        yield return new WaitForSeconds(time);
        gamepad?.SetMotorSpeeds(0.0f, 0.0f);
        vibrationCR = null;
    }
}
