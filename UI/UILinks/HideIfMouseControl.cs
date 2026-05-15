using UnityEngine;

namespace UC
{

    public class HideIfMouseControl : MonoBehaviour
    {
        UIGroup     parentUI;
        CanvasGroup canvasGroup;
        Renderer    mainRenderer;

        void Start()
        {
            parentUI = GetComponentInParent<UIGroup>();
            canvasGroup = GetComponent<CanvasGroup>();
            mainRenderer = GetComponent<Renderer>();
        }

        // Update is called once per frame
        void Update()
        {
            if ((parentUI) && (parentUI.uiEnable))
            {
                if (parentUI.isMouseActive(2.0f))
                {
                    if (canvasGroup)
                        canvasGroup?.FadeOut(0.2f);
                    else if (mainRenderer)
                        mainRenderer.enabled = false;
                }
                else
                {
                    if (canvasGroup)
                        canvasGroup?.FadeIn(0.2f);
                    else if (mainRenderer)
                        mainRenderer.enabled = true;
                }
            }
        }
    }
}