using System;
using UnityEngine;
using static DialogueData;

public abstract class DialogueDisplay : MonoBehaviour
{
    protected CanvasGroup canvasGroup;

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup)
            canvasGroup.alpha = 0.0f;
        else
            gameObject.SetActive(false);
    }

    public abstract void Display(DialogueElement dialogue);
    public abstract void Clear();
    public abstract void Skip();
    public abstract bool isDisplaying();
}
