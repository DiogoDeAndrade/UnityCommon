using UnityEngine;

namespace UC
{

    public class DisplayIfHighlighted : MonoBehaviour
    {
        [SerializeField] private BaseUIControl control;
        [SerializeField] private float fadeDuration = 0.25f;

        CanvasGroup canvasGroup;
        Tweener.BaseInterpolator fader;

        private void Start()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0.0f;
            }
        }

        void Update()
        {
            if (control.isSelected)
            {
                if ((fader == null) || (fader.isFinished))
                    fader = canvasGroup.FadeIn(fadeDuration);
            }
            else
            {
                if ((fader == null) || (fader.isFinished))
                    fader = canvasGroup.FadeOut(fadeDuration);
            }
        }
    }
}