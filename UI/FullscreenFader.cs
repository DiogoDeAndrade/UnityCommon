using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using System;

namespace UC
{

    public class FullscreenFader : MonoBehaviour
    {
        [SerializeField] private Color faderColor;
        [SerializeField] private bool startFaded;
        [ShowIf("startFaded")]
        [SerializeField] private bool autoFadeIn;
        [ShowIf(EConditionOperator.And, "startFaded", "autoFadeIn")]
        [SerializeField] private float fadeInSpeed;

        Image fader;
        float currentT;
        Color startColor;
        Color targetColor;
        float fadeInc;
        System.Action callback;

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
                FadeIn(fadeInSpeed, faderColor);
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
                currentT += fadeInc * Time.deltaTime;

                fader.color = Color.Lerp(startColor, targetColor, currentT);

                if (currentT >= 1.0f)
                {
                    if (callback != null) callback.Invoke();
                    fadeInc = 0.0f;
                }
            }
        }

        void _Fade(float targetAlpha, float time, Color targetColor, System.Action action)
        {
            currentT = 0.0f;
            fader.color = targetColor.ChangeAlpha(fader.color.a);
            startColor = fader.color;
            this.targetColor = targetColor.ChangeAlpha(targetAlpha);
            fadeInc = Mathf.Abs((targetAlpha - fader.color.a) / time);
            callback = action;
        }

        public static void FadeIn(float time)
        {
            fsFader._Fade(0.0f, time, fsFader.fader.color, null);
        }

        public static void FadeIn(float time, Color color)
        {
            fsFader._Fade(0.0f, time, color, null);
        }

        public static void FadeIn(float time, Color color, System.Action action)
        {
            fsFader._Fade(0.0f, time, color, action);
        }

        public static void FadeOut(float time)
        {
            fsFader._Fade(1.0f, time, fsFader.fader.color, null);
        }

        public static void FadeOut(float time, Color color)
        {
            fsFader._Fade(1.0f, time, color, null);
        }

        public static void FadeOut(float time, Color color, System.Action action)
        {
            fsFader._Fade(1.0f, time, color, action);
        }

        public static bool hasFader => fsFader != null;

    }
}