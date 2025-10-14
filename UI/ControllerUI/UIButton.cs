using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UC
{

    public class UIButton : BaseUIControl
    {
        public enum AutoEvent { None, SwitchPanel, CloseThisPanel, ChangeScene = 10, QuitApplication = 20 };

        [SerializeField] 
        private AutoEvent   interactEvent;
        [SerializeField, ShowIf(nameof(needPanel))] 
        private UIGroup     panel;
        [SerializeField, ShowIf(nameof(needScene)), Scene] 
        private string      sceneName;

        bool needPanel => interactEvent == AutoEvent.SwitchPanel;
        bool needScene => interactEvent == AutoEvent.ChangeScene;

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
                        if (cg) cg.FadeOut(0.5f).SetUnscaledTime(parentGroup.useUnscaledTime);
                        parentGroup.SetUI(false);
                        cg = panel.GetComponent<CanvasGroup>();
                        if (cg) cg.FadeIn(0.5f).SetUnscaledTime(parentGroup.useUnscaledTime);
                        panel.SetUI(true);
                    }
                    break;
                case AutoEvent.CloseThisPanel:
                    {
                        CanvasGroup cg;

                        UIGroup topGroup = parentGroup.GetComponentInParent<UIGroup>();
                        if (topGroup)
                        {
                            cg = topGroup.GetComponent<CanvasGroup>();
                            if (cg) cg.FadeIn(0.5f)?.SetUnscaledTime(parentGroup.useUnscaledTime);
                            topGroup.SetUI(true);
                        }
                        cg = parentGroup.GetComponent<CanvasGroup>();
                        if (cg) cg.FadeOut(0.5f).SetUnscaledTime(parentGroup.useUnscaledTime);
                        parentGroup.SetUI(false);
                    }
                    break;
                case AutoEvent.ChangeScene:
                    FullscreenFader.FadeOut(0.5f, Color.black, () => SceneManager.LoadScene(sceneName));
                    break;
                case AutoEvent.QuitApplication:
                    FullscreenFader.FadeOut(0.5f, Color.black, () =>
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    });
                    break;
                default:
                    break;
            }
        }
    }
}