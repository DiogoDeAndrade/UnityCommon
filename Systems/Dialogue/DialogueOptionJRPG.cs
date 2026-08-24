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
        [SerializeField] Color optionTextUnavailableColor = Color.gray;
        [SerializeField] Image selectorBarSelected;
        [SerializeField] Color optionBarNormalColor = Color.white;
        [SerializeField] Color optionBarSelectedColor = Color.yellow;
        [SerializeField] Color optionBarUnavailableColor = Color.gray;

        bool available = true;

        public override void Show(string text, bool available)
        {
            gameObject.SetActive(true);

            this.available = available;
            optionText.text = text;
            Deselect();
        }

        public override void Hide()
        {
            gameObject.SetActive(false);
        }

        public override void Select()
        {
            if (!available) return;

            optionText.color = optionTextSelectedColor;
            selectorBarSelected.color = optionBarSelectedColor;
        }

        public override void Deselect()
        {
            optionText.color = (available) ? (optionTextNormalColor) : (optionTextUnavailableColor);
            selectorBarSelected.color = (available) ? (optionBarNormalColor) : (optionBarUnavailableColor);
        }
    }
}