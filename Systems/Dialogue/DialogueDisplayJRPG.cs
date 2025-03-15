using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using NaughtyAttributes;

public class DialogueDisplayJRPG : DialogueDisplay
{
    public enum AppearMethod { All, PerChar };

    [SerializeField] float              fadeTime = 0.25f;
    [SerializeField] RectTransform      speakerContainer;
    [SerializeField] Image              speakerPortrait;
    [SerializeField] TextMeshProUGUI    speakerName;
    [SerializeField] TextMeshProUGUI    dialogueText;
    [SerializeField] AppearMethod       appearMethod = AppearMethod.All;
    [SerializeField, ShowIf(nameof(needTimePerCharacter))]
    float timePerCharacter = 0.1f;
    [SerializeField, ShowIf(nameof(needTimePerCharacter))]
    float timePerCharacterSkip = 0.05f;
    [SerializeField] GameObject         continueStatusObject;
    [SerializeField] GameObject         skipStatusObject;
    [SerializeField] GameObject         doneStatusObject;

    DialogueData.DialogueElement    currentDialogue;
    Coroutine                       showTextCR;
    bool                            skip = false;

    bool needTimePerCharacter => appearMethod == AppearMethod.PerChar;

    public override void Clear()
    {
        FadeOut();
        currentDialogue = null;
    }

    public override void Display(DialogueData.DialogueElement dialogue)
    {
        if (currentDialogue == dialogue) return;

        currentDialogue = dialogue;
        DisableAllStatus();        

        FadeIn();

        if (speakerName)
        {
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

        dialogueText.text = dialogue.text;
        dialogueText.color = dialogue.speaker.textColor;

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
        }
    }

    IEnumerator ShowTextCharCR()
    {
        for (int i = 0; i < currentDialogue.text.Length; i++)
        {
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

        showTextCR = null;
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
}
