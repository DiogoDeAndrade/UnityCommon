using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UC
{

    public class DialogueOptionJRPG : DialogueOption
    {
        [SerializeField] TextMeshProUGUI optionText;
        [SerializeField] Color optionTextNormalColor = Color.white;
        [SerializeField] Color optionTextSelectedColor = Color.yellow;
        [SerializeField] Image selectorBarSelected;
        [SerializeField] Color optionBarNormalColor = Color.white;
        [SerializeField] Color optionBarSelectedColor = Color.yellow;

        public override void Show(string text)
        {
            gameObject.SetActive(true);

            optionText.text = text;
            Deselect();
        }

        public override void Hide()
        {
            gameObject.SetActive(false);
        }

        public override void Select()
        {
            optionText.color = optionTextSelectedColor;
            selectorBarSelected.color = optionBarSelectedColor;
        }

        public override void Deselect()
        {
            optionText.color = optionTextNormalColor;
            selectorBarSelected.color = optionBarNormalColor;
        }
    }
}