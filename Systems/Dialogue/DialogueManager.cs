using System;
using System.Collections.Generic;
using UnityEngine;
using static DialogueData;

public class DialogueManager : MonoBehaviour
{
    public delegate void OnDialogueStart(string dialogueKey);
    public event OnDialogueStart onDialogueStart;
    public delegate void OnDialogueEnd();
    public event OnDialogueEnd onDialogueEnd;
    public delegate void OnDialogueEvent(string parameter);
    public event OnDialogueEvent onDialogueEvent;

    [SerializeField] private DialogueData       dialogueData;
    [SerializeField] private DialogueDisplay    display;

    DialogueData.Dialogue   currentDialogue = null;
    int                     currentDialogueIndex = -1;
    Dictionary<string, int> dialogueCount = new();
    Dictionary<string, int> dialogueEvents = new();

    static DialogueManager instance = null;

    public static DialogueManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<DialogueManager>();
            }
            return instance;
        }
    }

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    protected bool _StartConversation(string dialogueKey)
    {
        var dialogue = dialogueData.GetDialogue(dialogueKey);
        if (dialogue == null) return false;
        if (((dialogue.flags & DialogueData.DialogueFlags.OneShot) != 0) &&
            dialogueCount.ContainsKey(dialogueKey))
        {
            return false;
        }

        currentDialogue = dialogue;
        currentDialogueIndex = -1;

        if (dialogueCount.ContainsKey(dialogueKey))
            dialogueCount[dialogueKey]++;
        else
            dialogueCount[dialogueKey] = 1;

        NextDialogue();

        onDialogueStart?.Invoke(dialogueKey);

        return true;
    }

    void NextDialogue()
    {
        if (currentDialogue == null)
        {
            EndDialogue();
            return;
        }

        // Check if this has options
        if (currentDialogue != null)
        {
            if ((currentDialogueIndex >= 0) && (currentDialogue.elems.Count > currentDialogueIndex))
            {
                if (currentDialogue.elems[currentDialogueIndex].hasOptions)
                {
                    // Get selected option
                    int selectedOption = display.GetSelectedOption();
                    var option = currentDialogue.elems[currentDialogueIndex].options[selectedOption];
                    StartConversation(option.key);
                    return;
                }
                var nextKey = currentDialogue.nextKey;
                if (!string.IsNullOrEmpty(nextKey))
                {
                    if (nextKey.StartsWith("Quit"))
                    {
                        EndDialogue();

                        var innerSection = nextKey.Substring(5, nextKey.Length - 6);
                        onDialogueEvent?.Invoke(innerSection);
                        dialogueEvents[innerSection] = Time.frameCount;
                    }
                    else
                    {
                        StartConversation(nextKey);
                    }
                    return;
                }
            }
        }

        currentDialogueIndex++;
        if (currentDialogueIndex < currentDialogue.elems.Count)
        {
            display.Display(currentDialogue.elems[currentDialogueIndex]);
        }
        else
        {
            // Check if current dialogue redirects to something
            if ((currentDialogue.conditionalNext != null) &&
                (currentDialogue.conditionalNext.Count > 0))
            {
                var context = GetComponent<UCExpression.IContext>();
                if (context != null)
                {
                    foreach (var condition in currentDialogue.conditionalNext)
                    {
                        if (UCExpression.TryParse(condition.expression, out var expression))
                        {
                            if (expression.Evaluate(context))
                            {
                                _StartConversation(condition.nextKey);
                                return;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentDialogue.nextKey))
            {
                _StartConversation(currentDialogue.nextKey);
                return;
            }

            EndDialogue();
        }
    }

    void EndDialogue()
    {
        display.Clear();
        onDialogueEnd?.Invoke();
        currentDialogue = null;
        currentDialogueIndex = -1;
    }

    private bool _HasDialogueEvent(string dialogueEventName, int frameTolerance)
    {
        if (dialogueEvents.TryGetValue(dialogueEventName, out int lastTime))
        {
            if (Time.frameCount - lastTime <= frameTolerance) return true;
        }
        return false;
    }

    protected virtual void _SetInput(Vector2 moveVector)
    {
        display.SetInput(moveVector);
    }
    private void _Continue()
    {
        if (display.isDisplaying())
        {
            display.Skip();
        }
        else
        {            
            NextDialogue();
        }
    }

    bool _hasMoreText
    {
        get
        {
            if (currentDialogue == null) return false;

            if (currentDialogueIndex >= currentDialogue.elems.Count) return false;

            if ((!string.IsNullOrEmpty(currentDialogue.nextKey)) && (!IsKeyword(currentDialogue.nextKey)) && (HasDialogue(currentDialogue.nextKey))) return true;

            if (currentDialogue.elems[currentDialogueIndex].hasOptions) return true;

            return currentDialogueIndex < currentDialogue.elems.Count - 1;
        }
    }

    bool IsKeyword(string key)
    {
        if (key.StartsWith("Quit")) return true;

        return false;
    }

    public static bool HasDialogue(string dialogueKey)
    {
        if (Instance)
        {
            var dialogue = Instance.dialogueData.GetDialogue(dialogueKey);
            if (dialogue != null)
            {
                if (((dialogue.flags & DialogueData.DialogueFlags.OneShot) != 0) &&
                    Instance.dialogueCount.ContainsKey(dialogueKey))
                {
                    return false;
                }

                return true;
            }
        }

        return false;
    }

    public static bool HasSaidDialogue(string dialogueKey)
    {
        if (Instance)
        {
            if (Instance.dialogueCount.ContainsKey(dialogueKey)) return true;
        }

        return false;
    }

    public static bool StartConversation(string dialogueKey)
    {
        if (Instance == null) return false;

        return Instance._StartConversation(dialogueKey);
    }

    public static void Continue()
    {
        if (Instance == null) return;

        Instance._Continue();
    }

    public static void SetInput(Vector2 moveVector)
    {
        if (Instance == null) return;

        Instance._SetInput(moveVector);
    }

    internal static bool HasDialogueEvent(string dialogueEventName, int frameTolerance)
    {
        if (Instance == null) return false;

        return Instance._HasDialogueEvent(dialogueEventName, frameTolerance);
    }

    public static bool hasMoreText
    {
        get
        {
            if (Instance == null) return false;

            return Instance._hasMoreText;
        }
    }

    public static bool isTalking
    {
        get
        {
            if (Instance == null) return false;

            return Instance.currentDialogue != null;
        }
    }
}
