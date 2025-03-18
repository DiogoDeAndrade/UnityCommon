using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

        // If after NextDialogue, currentDialogue is null, then it means that the text hasn't been able to start
        if (currentDialogue != null)
        {
            onDialogueStart?.Invoke(dialogueKey);
            return true;
        }

        return false;

    }

    void NextDialogue()
    {
        if (currentDialogue == null)
        {
            EndDialogue();
            return;
        }

        // Check if it's an option
        if ((currentDialogueIndex >= 0) && (currentDialogue.elems.Count > currentDialogueIndex))
        {
            if (currentDialogue.elems[currentDialogueIndex].hasOptions)
            {
                // Get selected option
                int selectedOption = display.GetSelectedOption();
                var option = currentDialogue.elems[currentDialogueIndex].options[selectedOption];
                _StartConversation(option.key);
                return;
            }
        }

        // It's not, so move forward - check if there's more text
        currentDialogueIndex++;
        if (currentDialogueIndex < currentDialogue.elems.Count)
        {
            display.Display(currentDialogue.elems[currentDialogueIndex]);
        }
        else
        {
            // Check if current dialogue is done (or has nothing), check if it redirects to something
            if ((currentDialogue.conditionalNext != null) &&
                (currentDialogue.conditionalNext.Count > 0))
            {
                var context = GetComponent<UCExpression.IContext>();
                if (context != null)
                {
                    foreach (var condition in currentDialogue.conditionalNext)
                    {
                        if (string.IsNullOrEmpty(condition.condition))
                        {
                            Execute(condition.nextKey);
                            return;
                        }
                        if (UCExpression.TryParse(condition.condition, out var expression))
                        {
                            if (expression.Evaluate(context))
                            {
                                Execute(condition.nextKey);
                                return;
                            }
                        }
                    }
                }
            }

            EndDialogue();
        }
    }

    private void Execute(DialogueData.NextKeyOrCode nextKey)
    {
        if (nextKey.isCode)
        {
            var context = GetComponent<UCExpression.IContext>();

            foreach (var c in nextKey.code)
            {
                if (c.isFunctionCall)
                {
                    FunctionCall(c, context);
                }
                else 
                {
                    if ((c.expressions == null) || (c.expressions.Count < 1))
                    {
                        throw new UCExpression.ErrorException("Missing expression for assignment!");
                    }

                    if (UCExpression.TryParse(c.expressions[0], out var expression))
                    {
                        if (expression.GetDataType(context) == UCExpression.DataType.Bool)
                            context.SetVariable(c.functionOrVarName, expression.Evaluate(context));
                        else
                            context.SetVariable(c.functionOrVarName, expression.EvaluateNumber(context));
                    }
                }
            }
        }
        else
        {
            if (!_StartConversation(nextKey.nextKey))
            {
                EndDialogue();
            }            
        }
    }

    void FunctionCall(DialogueData.CodeElem code, UCExpression.IContext context)
    {
        var type = context.GetType();
        var methodInfo = type.GetMethod(code.functionOrVarName);
        
        if (methodInfo == null)
        {
            Debug.LogError($"Method \"{code.functionOrVarName}\" not found in context!");
            return;
        }

        // Check parameters, check parameter types
        List<object> args = new();
        ParameterInfo[] parameters = methodInfo.GetParameters();

        if (parameters.Length != code.expressions.Count)
        {
            Debug.LogError($"Invalid number of argument for \"{code.functionOrVarName}\": expected {parameters.Length}, received {code.expressions.Count}!");
        }
        else
        {
            for (int index = 0; index < parameters.Length; index++) 
            {
                ParameterInfo param = parameters[index];

                if (index >= code.expressions.Count)
                {
                    Debug.LogError($"Call to \"{code.functionOrVarName}\" is missing parameter #{index} ({param.Name})!");
                    continue;
                }
                if (UCExpression.TryParse(code.expressions[index], out var expression))
                {
                    Type paramType = param.ParameterType;
                    if (paramType == typeof(bool))
                    {
                        var pType = expression.GetDataType(context);
                        if (pType == UCExpression.DataType.Bool)
                        {
                            args.Add(expression.Evaluate(context));
                        }
                        else
                        {
                            Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} ({code.expressions[index]})!");
                        }
                    }
                    else if (paramType == typeof(float))
                    {
                        var pType = expression.GetDataType(context);
                        if (pType == UCExpression.DataType.Number)
                        {
                            args.Add(expression.EvaluateNumber(context));
                        }
                        else
                        {
                            Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} ({code.expressions[index]})!");
                        }
                    }
                    else
                    {
                        Debug.LogError($"Unsupported type {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\"!");
                    }
                }
                else
                {
                    Debug.LogError($"Failed to parse argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\" ({code.expressions[index]})!");
                    continue;
                }
            }
            if (args.Count == parameters.Length)
            {
                methodInfo.Invoke(context, args.ToArray());
            }
            else
            {
                Debug.LogError($"Failed to call method {code.functionOrVarName}!");
            }
        }
    }

    public void EndDialogue()
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

            if (currentDialogue.elems[currentDialogueIndex].hasOptions) return true;

            if (currentDialogueIndex < currentDialogue.elems.Count - 1) return true;

            var context = GetComponent<UCExpression.IContext>();

            return !string.IsNullOrEmpty(currentDialogue.GetNextDialogue(context));
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
            while (dialogue != null)
            {
                if (((dialogue.flags & DialogueData.DialogueFlags.OneShot) != 0) &&
                    Instance.dialogueCount.ContainsKey(dialogueKey))
                {
                    return false;
                }

                // Check if this is a NULL entry (just a redirect to something)
                if (dialogue.isRedirect)
                {
                    var context = Instance.GetComponent<UCExpression.IContext>();

                    var nextDialogue = dialogue.GetNextDialogue(context);
                    if (string.IsNullOrEmpty(nextDialogue)) return false;

                    dialogue = Instance.dialogueData.GetDialogue(nextDialogue);
                }
                else
                {
                    return true;
                }
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
