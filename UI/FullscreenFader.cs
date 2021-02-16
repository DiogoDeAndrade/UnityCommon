using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;

public class FullscreenFader : MonoBehaviour
{
    public Color    faderColor;
    public bool     startFaded;
    [ShowIf("startFaded")]
    public bool     autoFadeIn;
    [ShowIf(EConditionOperator.And, "startFaded", "autoFadeIn")]
    public float    fadeInSpeed;

    Image fader;
    float fadeInc;

    static FullscreenFader fsFader;
    
    void Start()
    {
        if (fsFader == null)
        {
            fsFader = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        fader = GetComponentInChildren<Image>();

        fader.color = faderColor.ChangeAlpha((startFaded) ? (1.0f) : (0.0f));

        if ((startFaded) && (autoFadeIn))
        {
            fadeInc = -1.0f / fadeInSpeed;
        }
        else
        {
            fadeInc = 0;
        }
    }

    void Update()
    {
        if (fadeInc != 0.0f)
        {
            float fadeAlpha = fader.color.a;

            fadeAlpha += fadeInc * Time.deltaTime;
            
            if ((fadeAlpha > 1.0f) && (fadeInc > 0.0f))
            {
                fadeAlpha = 1.0f;
                fadeInc = 0.0f;
            }
            else if ((fadeAlpha < 0.0f) && (fadeInc < 0.0f))
            {
                fadeAlpha = 0.0f;
                fadeInc = 0.0f;
            }

            fader.color = faderColor.ChangeAlpha(fadeAlpha);
        }
    }

    void _Fade(float targetAlpha, float time, Color targetColor)
    {
        faderColor = targetColor;
        fadeInc = (targetAlpha - fader.color.a) / time;
    }

    public static void FadeIn(float time)
    {
        fsFader._Fade(0.0f, time, fsFader.faderColor);
    }

    public static void FadeIn(float time, Color color)
    {
        fsFader._Fade(0.0f, time, color);
    }

    public static void FadeOut(float time)
    {
        fsFader._Fade(1.0f, time, fsFader.faderColor);
    }

    public static void FadeOut(float time, Color color)
    {
        fsFader._Fade(1.0f, time, color);
    }

}
