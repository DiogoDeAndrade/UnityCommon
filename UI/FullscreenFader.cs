using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using System;

public class FullscreenFader : MonoBehaviour
{
    public Color    faderColor;
    public bool     startFaded;
    [ShowIf("startFaded")]
    public bool     autoFadeIn;
    [ShowIf(EConditionOperator.And, "startFaded", "autoFadeIn")]
    public float    fadeInSpeed;

    Image   fader;
    float   fadeInc;
    Action  callback;

    static FullscreenFader fsFader;

    void Awake()
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
                if (callback != null) callback.Invoke();
            }
            else if ((fadeAlpha < 0.0f) && (fadeInc < 0.0f))
            {
                fadeAlpha = 0.0f;
                fadeInc = 0.0f;
                if (callback != null) callback.Invoke();
            }

            fader.color = faderColor.ChangeAlpha(fadeAlpha);
        }
    }

    void _Fade(float targetAlpha, float time, Color targetColor, Action action)
    {
        faderColor = targetColor;
        fadeInc = (targetAlpha - fader.color.a) / time;
        callback = action;
    }

    public static void FadeIn(float time)
    {
        fsFader._Fade(0.0f, time, fsFader.faderColor, null);
    }

    public static void FadeIn(float time, Color color)
    {
        fsFader._Fade(0.0f, time, color, null);
    }

    public static void FadeIn(float time, Color color, Action action)
    {
        fsFader._Fade(0.0f, time, color, action);
    }

    public static void FadeOut(float time)
    {
        fsFader._Fade(1.0f, time, fsFader.faderColor, null);
    }

    public static void FadeOut(float time, Color color)
    {
        fsFader._Fade(1.0f, time, color, null);
    }

    public static void FadeOut(float time, Color color, Action action)
    {
        fsFader._Fade(1.0f, time, color, action);
    }

}
