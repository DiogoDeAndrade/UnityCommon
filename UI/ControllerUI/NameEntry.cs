using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UC;

public class NameEntry : MonoBehaviour
{
    public delegate void OnNameEntryComplete(string name);
    public event OnNameEntryComplete onNameEntryComplete;

    [SerializeField] private NameEntryElement   letterPrefab;
    [SerializeField] private int                maxLetters = 8;
    [SerializeField] private bool               allowEmpty = true;
    [SerializeField, Header("Input")]
    private PlayerInput playerInput;
    [SerializeField]
    private UC.InputControl moveVertical;
    [SerializeField, InputButton]
    private UC.InputControl selectButton;
    [SerializeField, InputButton]
    private UC.InputControl deleteButton;
    [Header("Repeat Tuning")]
    [Tooltip("Ignore tiny stick movement.")]
    [SerializeField] private float deadZone = 0.2f;

    [Tooltip("Delay before the first repeat kicks in (after the initial immediate step).")]
    [SerializeField] private float initialDelay = 0.35f;

    [Tooltip("Base repeat delay when stick is barely past the deadzone.")]
    [SerializeField] private float baseRepeatDelay = 0.14f;

    [Tooltip("Minimum possible delay when fully held + accelerated.")]
    [SerializeField] private float minRepeatDelay = 0.045f;

    [Tooltip("How quickly the repeat delay slides toward the target (per second).")]
    [SerializeField] private float accelTowardTarget = 2.5f;

    [Tooltip("Extra acceleration while held to give that 'ramp up' feel (per second).")]
    [SerializeField] private float holdAcceleration = 0.75f;

    [Tooltip("Exponent for analog curve. 1 = linear, >1 = needs stronger tilt for speedup.")]
    [SerializeField] private float analogCurve = 1.2f;

    CanvasGroup canvasGroup;
    bool        prevEnabled;
    
    List<NameEntryElement>  letters;
    NameEntryElement        currentLetter;

    // Repeat state
    int holdDir = 0;                  // -1 (down), 0 (neutral), +1 (up)
    float holdMag = 0f;               // magnitude 0..1
    float currentDelay;               // current delay between steps (dynamic)
    float targetDelay;                // delay driven by analog magnitude
    float nextStepAt;                 // time for next step
    bool waitingInitial;              // are we in the initial delay phase after the first step?

    void Start()
    {
        canvasGroup = GetComponentInParent<CanvasGroup>();
        prevEnabled = canvasGroup.alpha > 0.5f;

        moveVertical.playerInput = playerInput;
        selectButton.playerInput = playerInput;
        deleteButton.playerInput = playerInput;
    }

    void Update()
    {
        bool currentEnabled = canvasGroup.alpha > 0.5f;

        if (currentEnabled)
        {
            if (currentEnabled != prevEnabled)
            {
                if (currentLetter == null)
                {
                    NextLetter();
                }
            }

            HandleAnalogRepeat();

            if (selectButton.IsDown())
            {
                if (currentLetter.letter == "OK")
                {
                    // Stop entry
                    StopEntry();
                }
                else if (currentLetter.letter == "\u2190")
                {
                    DeleteLetter();
                }
                else
                {
                    NextLetter();
                }
            }
            if (deleteButton.IsDown())
            {
                DeleteLetter();
            }
        }
        else
        {
            if (currentEnabled != prevEnabled)
            {
                if (currentLetter != null)
                {
                    Destroy(currentLetter.gameObject);
                    if (letters != null)
                    {
                        foreach (var l in letters)
                        {
                            Destroy(l.gameObject);
                        }
                    }
                }
            }

            // Reset repeat state when hidden/disabled
            ResetRepeat();
        }

        prevEnabled = currentEnabled;
    }

    private void CurrentLetter_onSelect()
    {
        NextLetter();
    }

    private void CurrentLetter_onFinish()
    {
        StopEntry();
    }

    private void CurrentLetter_onBackspace()
    {
        DeleteLetter();
    }

    void NextLetter()
    {
        if (currentLetter)
        {
            currentLetter.Deactivate();
            if (letters == null) letters = new();
            letters.Add(currentLetter);
        }

        if ((letters != null) && (letters.Count >= maxLetters))
        {
            // Stop entry
            StopEntry();
        }
        else
        {
            currentLetter = Instantiate(letterPrefab, transform);
            currentLetter.Activate();
            currentLetter.onFinish += CurrentLetter_onFinish;
            currentLetter.onSelect += CurrentLetter_onSelect;
            currentLetter.onBackspace += CurrentLetter_onBackspace;
        }
    }

    void DeleteLetter()
    {
        // Delete this letter and go back to last
        if ((letters != null) && (letters.Count > 0))
        {
            Destroy(currentLetter.gameObject);
            currentLetter = letters.PopLast();
            currentLetter.Activate();
        }
    }

    void StopEntry()
    {
        var name = CollectName();
        if ((name != "") || (allowEmpty))
        {
            onNameEntryComplete?.Invoke(name);
        }
    }

    string CollectName()
    {
        string name = "";
        if (letters != null)
        {
            foreach (var l in letters)
            {
                name += l.letter;
            }
        }
        return name;
    }   

    void HandleAnalogRepeat()
    {
        float raw = moveVertical.GetAxis(); // -1..+1
        int dir = 0;
        float mag = 0f;

        // Deadzone and direction
        if (raw > deadZone)
        {
            dir = +1;
            mag = Mathf.InverseLerp(deadZone, 1f, raw);
        }
        else if (raw < -deadZone)
        {
            dir = -1;
            mag = Mathf.InverseLerp(deadZone, 1f, -raw);
        }

        // Apply response curve to magnitude (so small tilts don't spam)
        mag = Mathf.Pow(mag, analogCurve);

        // If neutral, reset state so next tilt triggers an immediate step again.
        if (dir == 0)
        {
            ResetRepeat();
            return;
        }

        // If direction changed or we just started holding, do an immediate step.
        if (dir != holdDir)
        {
            holdDir = dir;
            holdMag = mag;

            DoStep(holdDir); // immediate
            waitingInitial = true;

            // First repeat happens after initialDelay blended by magnitude (stronger push = shorter wait)
            float initial = Mathf.Lerp(initialDelay, baseRepeatDelay, holdMag);
            currentDelay = initial;          // start here
            targetDelay = TargetDelayFromMag(holdMag);
            nextStepAt = Time.time + initial;
            return;
        }

        // Same direction: update magnitude and target
        holdMag = mag;
        targetDelay = TargetDelayFromMag(holdMag);

        // Smoothly accelerate the repeat delay toward target
        currentDelay = Mathf.MoveTowards(currentDelay, targetDelay, accelTowardTarget * Time.deltaTime);

        // Also apply a gentle ramp-down while held, to get that “speeds up over time” feel
        currentDelay = Mathf.Max(minRepeatDelay, currentDelay - holdAcceleration * Time.deltaTime);

        // Time to fire a repeat?
        if (Time.time >= nextStepAt)
        {
            DoStep(holdDir);
            // After the first post-immediate repeat, we're no longer in the initial wait
            if (waitingInitial) waitingInitial = false;

            // Schedule next based on the (now-accelerated) delay
            nextStepAt = Time.time + currentDelay;
        }
    }

    float TargetDelayFromMag(float mag01)
    {
        // Stronger push => shorter delay.
        // Lerp from baseRepeatDelay (soft push) to minRepeatDelay (full push).
        return Mathf.Lerp(baseRepeatDelay, minRepeatDelay, Mathf.Clamp01(mag01));
    }

    void DoStep(int dir)
    {
        if (dir > 0) currentLetter?.LetterUp();
        else if (dir < 0) currentLetter?.LetterDown();
    }

    void ResetRepeat()
    {
        holdDir = 0;
        holdMag = 0f;
        waitingInitial = false;
        currentDelay = 0f;
        targetDelay = 0f;
        nextStepAt = 0f;
    }
}
