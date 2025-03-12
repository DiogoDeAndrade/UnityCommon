using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceBar : MonoBehaviour
{
    public enum DisplayMode { ScaleImageX, DiscreteItems, ScaleImageY };
    public enum UpdateMode { Direct, FeedbackLoop, ConstantSpeed };

    public DisplayMode      displayMode;
    [ShowIf(nameof(isBar))]
    public UpdateMode       updateMode;
    [ShowIf(nameof(needSpeedVar))]
    public float            speed = 1.0f;
    [ShowIf(nameof(isBar))]
    public RectTransform    bar;
    [ShowIf(nameof(isBar))]
    public bool             colorChange;
    [ShowIf(nameof(needColor))]
    public Gradient         color;
    public bool             fadeAfterTime;
    [ShowIf(nameof(fadeAfterTime))]
    public bool             startDisabled;
    [ShowIf(nameof(fadeAfterTime))]
    public float            timeToFade = 2.0f;
    [ShowIf(nameof(fadeAfterTime))]
    public float            timeOfFadeIn = 0.5f;
    [ShowIf(nameof(fadeAfterTime))]
    public float            timeOfFadeOut = 1.0f;
    public bool             respectRotation = false;
    public bool             preserveRelativePositon = false;
    [ShowIf(nameof(preserveRelativePositon))]
    public Transform        relativeTransform;
    public bool             zeroResourceZeroAlpha = false;
    public Image            displayIcon;

    bool isBar => (displayMode == DisplayMode.ScaleImageX) || (displayMode == DisplayMode.ScaleImageY);
    bool needSpeedVar => (isBar) && ((updateMode == UpdateMode.FeedbackLoop) || (updateMode == UpdateMode.ConstantSpeed));
    bool needColor => (isBar) && (colorChange);

    // For alpha change
    CanvasGroup     canvasGroup;

    // For color change
    Image           uiImage;
    SpriteRenderer  spriteImage;

    // Updating
    float               currentT;
    float               prevT;
    float               alpha;
    float               changeTimer;
    List<GameObject>    discreteElements;

    // Keep vars
    Quaternion      initialRotation;
    Vector3         deltaPos;

    ResourceHandler targetResource;

    void Start()
    {
        if (isBar)
        {
            uiImage = bar.GetComponent<Image>();
            spriteImage = bar.GetComponent<SpriteRenderer>();
            prevT = currentT = GetNormalizedResource();
        }
        else if (displayMode == DisplayMode.DiscreteItems)
        {
            discreteElements = new();
            for (int i = 0; i < transform.childCount; i++)
            {
                var go = transform.GetChild(i).gameObject;
                go.SetActive(false);
                discreteElements.Add(go);
            }
            prevT = currentT = GetResourceCount();
        }

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

        if (preserveRelativePositon)
        {
            if (relativeTransform)
            {
                deltaPos = transform.position - relativeTransform.transform.position;
            }
            else preserveRelativePositon = false;
        }        

        UpdateGfx();
    }

    protected virtual void Update()
    {
        if (isBar)
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
        }
        else if (displayMode == DisplayMode.DiscreteItems)
        {
            currentT = GetResourceCount();
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
        if (preserveRelativePositon)
        {
            transform.position = relativeTransform.position + deltaPos;
        }
    }

    protected virtual float GetNormalizedResource()
    {
        if (targetResource)
        {
            return targetResource.normalizedResource;
        }

        return 0.0f;
    }

    protected virtual float GetResourceCount()
    {
        if (targetResource)
        {
            return targetResource.resource;
        }

        return 0.0f;
    }

    void UpdateGfx()
    {
        switch (displayMode)
        {
            case DisplayMode.DiscreteItems:
                {
                    for (int i = 0; i < discreteElements.Count; i++)
                    {
                        discreteElements[i].SetActive(i < currentT);
                    }
                }
                break;
            case DisplayMode.ScaleImageX:
                bar.localScale = new Vector3(currentT, 1.0f, 1.0f);
                break;
            case DisplayMode.ScaleImageY:
                bar.localScale = new Vector3(1.0f, currentT, 1.0f);
                break;
            default:
                break;
        }

        if (needColor)
        {
            Color c = color.Evaluate(currentT);

            if (uiImage) uiImage.color = c;
            if (spriteImage) spriteImage.color = c;
        }
        else
        {
            if (targetResource)
            {
                if (uiImage) uiImage.color = targetResource.type.displayBarColor;
                if (spriteImage) spriteImage.color = targetResource.type.displaySpriteColor;
            }
        }

        if ((displayIcon) && (targetResource))
        {
            displayIcon.sprite = targetResource.type.displaySprite;
            displayIcon.color = targetResource.type.displaySpriteColor;
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

        if ((zeroResourceZeroAlpha) && (GetNormalizedResource() <= 0))
        {
            alpha = 0.0f;
            canvasGroup.alpha = 0.0f;
        }
    }

    public void SetTarget(ResourceHandler target)
    {
        targetResource = target;

        UpdateGfx();
    }
}
