using NaughtyAttributes;
using UnityEngine;

public class UIButton : BaseUIControl
{
    public enum AutoEvent { None, SwitchPanel, CloseThisPanel };

    [SerializeField] private AutoEvent interactEvent;
    [SerializeField, ShowIf(nameof(needPanel))] private UIGroup panel;

    bool needPanel => interactEvent == AutoEvent.SwitchPanel;

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
                    if (cg) cg.FadeOut(0.5f);
                    parentGroup.SetUI(false);
                    cg = panel.GetComponent<CanvasGroup>();
                    if (cg) cg.FadeIn(0.5f);
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
                        if (cg) cg.FadeIn(0.5f);
                        topGroup.SetUI(true);
                    }
                    cg = parentGroup.GetComponent<CanvasGroup>();
                    if (cg) cg.FadeOut(0.5f);
                    parentGroup.SetUI(false);
                }
                break;
            default:
                break;
        }
    }
}
