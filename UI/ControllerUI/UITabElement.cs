using UnityEngine;

namespace UC
{
    public class UITabElement : BaseUIControl
    {
        [SerializeField] private UIGroup uiGroup;
        [SerializeField] private float   fadeInOutTime = 0.25f;

        public override void NotifySelect(BaseUIControl prevControl)
        {
            base.NotifySelect(prevControl);

            if (uiGroup)
            {
                uiGroup.EnableUI(true);
                CanvasGroup canvasGroup = uiGroup.GetComponent<CanvasGroup>();
                canvasGroup?.FadeIn(fadeInOutTime);
            }
        }

        public override void NotifyDeselect()
        {
            base.NotifyDeselect();

            if (uiGroup)
            {
                uiGroup.EnableUI(false);
                CanvasGroup canvasGroup = uiGroup.GetComponent<CanvasGroup>();
                canvasGroup?.FadeOut(fadeInOutTime);
            }
        }
    }
}
