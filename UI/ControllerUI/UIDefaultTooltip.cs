using TMPro;
using UnityEngine;

namespace UC.Deprecated
{

    public class UIDefaultTooltip : Tooltip
    {
        [SerializeField] private TextMeshProUGUI[] textElements;

        public void Set(BaseUIControl control)
        {
            for (int i = 0; i < textElements.Length; i++)
            {
                var txt = control.TooltipGetText(i);
                if (txt != string.Empty)
                {
                    textElements[i].text = txt;
                    textElements[i].gameObject.SetActive(true);
                }
                else
                {
                    textElements[i].gameObject.SetActive(false);
                }
            }

            canvasGroup.FadeIn(0.1f);
        }
    }
}
