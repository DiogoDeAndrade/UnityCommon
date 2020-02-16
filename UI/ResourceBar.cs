using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceBar : MonoBehaviour
{
    public enum DisplayMode { ScaleImage };
    public enum UpdateMode { Direct, FeedbackLoop, ConstantSpeed };

    public UpdateMode       updateMode;
    [ShowIf("NeedSpeedVar")]
    public float            speed = 1.0f;
    public DisplayMode      displayMode;
    public RectTransform    bar;
    public bool             colorChange;
    [ShowIf("colorChange")]
    public Gradient         color;
    public bool             fadeAfterTime;
    [ShowIf("fadeAfterTime")]
    public bool             startDisabled;
    [ShowIf("fadeAfterTime")]
    public float            timeToFade = 2.0f;
    [ShowIf("fadeAfterTime")]
    public float            timeOfFadeIn = 0.5f;
    [ShowIf("fadeAfterTime")]
    public float            timeOfFadeOut = 1.0f;
    public bool             respectRotation = false;

    public bool NeedSpeedVar() { return (updateMode == UpdateMode.FeedbackLoop) || (updateMode == UpdateMode.ConstantSpeed); }

    // For alpha change
    CanvasGroup     canvasGroup;

    // For color change
    Image           uiImage;
    SpriteRenderer  spriteImage;

    // Updating
    float           currentT;
    float           prevT;
    float           alpha;
    float           changeTimer;

    // Keep vars
    Quaternion      initialRotation;

    void Start()
    {
        uiImage = bar.GetComponent<Image>();
        spriteImage = bar.GetComponent<SpriteRenderer>();
        prevT = currentT = GetNormalizedResource();
        canvasGroup = GetComponent<CanvasGroup>();
        changeTimer = 0.0f;
        initialRotation = transform.rotation;
        if (fadeAfterTime)
        {
            if (startDisabled)
            {
                alpha = 0.0f;
                changeTimer = timeToFade + timeOfFadeOut;
            }
            else
            {
                alpha = 1.0f;
                changeTimer = 0.0f;
            }
            canvasGroup.alpha = alpha;
        }

        UpdateGfx();
    }

    void Update()
    {
        float tt = GetNormalizedResource();

        switch (updateMode)
        {
            case UpdateMode.Direct:
                currentT = tt;
                break;
            case UpdateMode.FeedbackLoop:
                currentT = currentT + (tt - currentT) * speed;
                break;
            case UpdateMode.ConstantSpeed:
                float dist = tt - currentT;
                float inc = Mathf.Sign(dist) * speed * Time.deltaTime;
                if (Mathf.Abs(dist) < Mathf.Abs(inc)) currentT = tt;
                else currentT = currentT + inc;
                break;
            default:
                break;
        }

        if (currentT != prevT)
        {
            changeTimer = 0.0f;

            UpdateGfx();
        }
        else
        {
            changeTimer += Time.deltaTime;
        }

        RunFade();

        prevT = currentT;
    }

    private void LateUpdate()
    {
        if (!respectRotation)
        {
            transform.rotation = initialRotation;
        }
    }

    protected virtual float GetNormalizedResource()
    {
        return 0.0f;
    }

    void UpdateGfx()
    {
        switch (displayMode)
        {
            case DisplayMode.ScaleImage:
                bar.localScale = new Vector3(currentT, 1.0f, 1.0f);
                break;
            default:
                break;
        }

        if (colorChange)
        {
            Color c = color.Evaluate(currentT);

            if (uiImage) uiImage.color = c;
            if (spriteImage) spriteImage.color = c;
        }
    }

    void RunFade()
    {
        if (fadeAfterTime)
        {
            if (changeTimer > timeToFade)
            {
                if (alpha > 0.0f)
                {
                    if (timeOfFadeIn > 0.0f) alpha = Mathf.Clamp01(alpha - Time.deltaTime / timeOfFadeOut);
                    else alpha = 0.0f;

                    canvasGroup.alpha = alpha;
                }
            }
            else
            {
                if (alpha < 1.0f)
                {
                    if (timeOfFadeIn > 0.0f) alpha = Mathf.Clamp01(alpha + Time.deltaTime / timeOfFadeIn);
                    else alpha = 1.0f;

                    canvasGroup.alpha = alpha;
                }
            }
        }
    }
}
