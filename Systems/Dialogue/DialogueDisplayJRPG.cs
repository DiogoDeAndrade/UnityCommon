using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using NaughtyAttributes;
using System.Threading;

public class DialogueDisplayJRPG : DialogueDisplay
{
    public enum AppearMethod { All, PerChar };

    [SerializeField] float              fadeTime = 0.25f;
    [SerializeField] RectTransform      speakerContainer;
    [SerializeField] Image              speakerPortrait;
    [SerializeField] TextMeshProUGUI    speakerName;
    [SerializeField] TextMeshProUGUI    dialogueText;
    [SerializeField] Color              dialogueDefaultColor = Color.white;
    [SerializeField] AppearMethod       appearMethod = AppearMethod.All;
    [SerializeField, ShowIf(nameof(needTimePerCharacter))]
    float timePerCharacter = 0.1f;
    [SerializeField, ShowIf(nameof(needTimePerCharacter))]
    float timePerCharacterSkip = 0.05f;
    [SerializeField] GameObject         optionSeparator;
    [SerializeField] DialogueOption[]   options;
    [SerializeField] float              optionCooldown = 0.1f;
    [SerializeField] GameObject         continueStatusObject;
    [SerializeField] GameObject         skipStatusObject;
    [SerializeField] GameObject         doneStatusObject;

    DialogueData.DialogueElement    currentDialogue;
    Coroutine                       showTextCR;
    bool                            skip = false;
    int                             selectedOption;
    float                           optionCooldownTime;

    bool needTimePerCharacter => appearMethod == AppearMethod.PerChar;

    public override void Clear()
    {
        FadeOut();
        currentDialogue = null;
    }

    public override void Display(DialogueData.DialogueElement dialogue)
    {
        if (currentDialogue == dialogue) return;

        ClearOptions();
        currentDialogue = dialogue;
        DisableAllStatus();        

        FadeIn();

        if (dialogue.speaker)
        {
            if (speakerName)
            {
                speakerName.gameObject.SetActive(true);
                speakerName.text = dialogue.speaker.displayName;
                speakerName.color = dialogue.speaker.nameColor;
            }

            if ((dialogue.speaker.displaySprite) && (speakerPortrait != null) && (speakerContainer != null))
            {
                speakerPortrait.sprite = dialogue.speaker.displaySprite;
                speakerPortrait.color = dialogue.speaker.displaySpriteColor;

                speakerContainer.gameObject.SetActive(true);
            }
            else
            {
                if (speakerContainer) speakerContainer.gameObject.SetActive(false);
            }

            dialogueText.color = dialogue.speaker.textColor;
        }
        else
        {
            if (speakerName)
            {
                speakerName.gameObject.SetActive(false);
            }

            if (speakerContainer != null)
            {
                speakerContainer.gameObject.SetActive(false);
            }

            dialogueText.color = dialogueDefaultColor;
        }

        dialogueText.text = dialogue.text;

        if (appearMethod == AppearMethod.PerChar)
        {
            skip = false;
            skipStatusObject?.SetActive(true);

            if (showTextCR != null) StopCoroutine(showTextCR);
            showTextCR = StartCoroutine(ShowTextCharCR());
        }
        else
        {
            if (DialogueManager.hasMoreText) continueStatusObject?.SetActive(true);
            else doneStatusObject?.SetActive(true);

            DisplayOptions();
        }
    }

    IEnumerator ShowTextCharCR()
    {
        for (int i = 0; i < currentDialogue.text.Length; i++)
        {
            if (dialogueText.text[i] == '<')
            {
                // Move forward to skip tag
                i++;
                while ((dialogueText.text[i] != '>') &&
                       (i < currentDialogue.text.Length))
                {
                    i++;
                }
                i++;
            }
            dialogueText.text = currentDialogue.text.Insert(i, "<color=#FFFFFF00>");

            if (!skip)
                yield return new WaitForSeconds(timePerCharacter);
            else if (timePerCharacterSkip > 0)
                yield return new WaitForSeconds(timePerCharacterSkip);
        }

        dialogueText.text = currentDialogue.text;

        DisableAllStatus();
        if (DialogueManager.hasMoreText) continueStatusObject?.SetActive(true);
        else doneStatusObject?.SetActive(true);

        DisplayOptions();

        showTextCR = null;
    }

    void DisplayOptions()
    {
        ClearOptions();

        if (currentDialogue.hasOptions)
        {
            optionSeparator.SetActive(true);
            for (int i = 0; i < currentDialogue.options.Count; i++)
            {
                options[i]?.Show(currentDialogue.options[i].text);
            }

            selectedOption = 0;

            options[selectedOption]?.Select();

            DisableAllStatus();
        }
    }

    void ClearOptions()
    {
        optionSeparator.SetActive(false);
        foreach (var opt in options)
        {
            opt?.Hide();
        }
    }

    void FadeIn()
    {
        if (canvasGroup)
            canvasGroup.FadeIn(fadeTime);
        else
            gameObject.SetActive(true);
    }

    void FadeOut()
    {
        if (canvasGroup)
            canvasGroup.FadeOut(fadeTime);
        else
            gameObject.SetActive(false);
    }

    void DisableAllStatus()
    {
        continueStatusObject?.SetActive(false);
        skipStatusObject?.SetActive(false);
        doneStatusObject?.SetActive(false);
    }

    public override void Skip()
    {
        skip = true;
        DisableAllStatus();
        if (DialogueManager.hasMoreText) continueStatusObject?.SetActive(true);
        else doneStatusObject?.SetActive(true);
    }

    public override bool isDisplaying() => (currentDialogue != null) && (showTextCR != null);

    public override void SetInput(Vector2 moveVector)
    {
        if ((Time.time - optionCooldownTime) < optionCooldown) return;

        if (currentDialogue == null) return;
        if (!currentDialogue.hasOptions) return;

        if (moveVector.y > 0.2f)
        {
            options[selectedOption].Deselect();
            selectedOption--;
            if (selectedOption < 0) selectedOption = currentDialogue.options.Count - 1;
            options[selectedOption].Select();

            optionCooldownTime = Time.time;
        }
        else if (moveVector.y < -0.2f)
        {
            options[selectedOption].Deselect();
            selectedOption = (selectedOption + 1) % currentDialogue.options.Count;
            options[selectedOption].Select();

            optionCooldownTime = Time.time;
        }
    }

    public override int GetSelectedOption() => selectedOption;
}
