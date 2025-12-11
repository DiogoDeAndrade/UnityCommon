using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace UC
{

    public class UIButton : BaseUIControl
    {
        public enum AutoEvent { None, SwitchPanel, CloseThisPanel, ShowCredits = 8, ChangeScene = 10, QuitApplication = 20, UnityAction = 50 };

        [SerializeField] 
        private AutoEvent       interactEvent;
        [SerializeField, ShowIf(nameof(needPanel))] 
        private UIGroup         panel;
        [SerializeField, ShowIf(nameof(needScene)), Scene] 
        private string          sceneName;
        [SerializeField, ShowIf(nameof(needFadeTime))]
        private float           fadeTime = 0.5f;
        [SerializeField, ShowIf(nameof(needCredits))]
        private BigTextScroll   creditsScroll;
        [SerializeField, ShowIf(nameof(needUnityAction))]
        private UnityEvent      unityEvent;

        bool needPanel => interactEvent == AutoEvent.SwitchPanel;
        bool needScene => interactEvent == AutoEvent.ChangeScene;
        bool needFadeTime => (interactEvent != AutoEvent.None);
        bool needCredits => (interactEvent == AutoEvent.ShowCredits);
        bool needUnityAction => (interactEvent == AutoEvent.UnityAction);

        public override void Interact()
        {
            if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);
            NotifyInteract();

            switch (interactEvent)
            {
                case AutoEvent.None:
                    break;
                case AutoEvent.SwitchPanel:
                    if (panel)
                    {
                        CanvasGroup cg = parentGroup.GetComponent<CanvasGroup>();
                        if (cg) cg.FadeOut(fadeTime).SetUnscaledTime(parentGroup.useUnscaledTime);
                        parentGroup.EnableUI(false);
                        cg = panel.GetComponent<CanvasGroup>();
                        if (cg) cg.FadeIn(fadeTime).SetUnscaledTime(parentGroup.useUnscaledTime);
                        panel.EnableUI(true);
                    }
                    break;
                case AutoEvent.CloseThisPanel:
                    {
                        CloseThisPanel();
                    }
                    break;
                case AutoEvent.ChangeScene:
                    FullscreenFader.FadeOut(fadeTime, Color.black, () => SceneManager.LoadScene(sceneName));
                    break;
                case AutoEvent.QuitApplication:
                    FullscreenFader.FadeOut(fadeTime, Color.black, () =>
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    });
                    break;
                case AutoEvent.ShowCredits:
                    {
                        var cg = parentGroup.GetComponent<CanvasGroup>();
                        if (cg) cg.FadeOut(fadeTime).SetUnscaledTime(parentGroup.useUnscaledTime);
                        parentGroup.EnableUI(false);

                        var creditsCanvasGroup = creditsScroll.GetComponent<CanvasGroup>();
                        creditsCanvasGroup.FadeIn(fadeTime);

                        creditsScroll.Reset();

                        creditsScroll.onEndScroll += CreditsScroll_onEndScroll;
                    }
                    break;
                case AutoEvent.UnityAction:
                    unityEvent?.Invoke();
                    break;
                default:
                    break;
            }
        }

        private void CreditsScroll_onEndScroll()
        {
            CanvasGroup cg = parentGroup.GetComponent<CanvasGroup>();
            if (cg) cg.FadeIn(fadeTime).SetUnscaledTime(parentGroup.useUnscaledTime);
            parentGroup.EnableUI(true);

            var creditsCanvasGroup = creditsScroll.GetComponent<CanvasGroup>();
            creditsCanvasGroup.FadeOut(fadeTime);

            creditsScroll.onEndScroll -= CreditsScroll_onEndScroll;
        }

        void CloseThisPanel()
        {
            CanvasGroup cg;

            UIGroup topGroup = parentGroup.GetComponentInParent<UIGroup>();
            if (topGroup)
            {
                cg = topGroup.GetComponent<CanvasGroup>();
                if (cg) cg.FadeIn(fadeTime)?.SetUnscaledTime(parentGroup.useUnscaledTime);
                topGroup.EnableUI(true);
            }
            cg = parentGroup.GetComponent<CanvasGroup>();
            if (cg) cg.FadeOut(fadeTime).SetUnscaledTime(parentGroup.useUnscaledTime);
            parentGroup.EnableUI(false);
        }
    }
}