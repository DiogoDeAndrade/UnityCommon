using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UC
{

    public class DialogueManager : Singleton<DialogueManager>
    {
        public delegate void OnDialogueStart(string dialogueKey);
        public event OnDialogueStart onDialogueStart;
        public delegate void OnDialogueEnd();
        public event OnDialogueEnd onDialogueEnd;

        [SerializeField] private DialogueData[] dialogueData;
        [SerializeField] private DialogueDisplay display;

        protected DialogueData              currentDialogueData = null;
        protected DialogueData.Dialogue     currentDialogue = null;
        protected int                       currentDialogueIndex = -1;
        // The copy of the current element actually handed to the display: guarded options have been
        // filtered/rolled by FilterOptions, so this is the only list the display's selection index
        // is valid in - the asset's own element still has every option
        protected DialogueData.DialogueElement currentDisplayedElement = null;
        protected Dictionary<string, int>   dialogueCount = new();
        protected Dictionary<string, int>   dialogueEvents = new();

        (DialogueData, DialogueData.Dialogue) FindDialogue(string dialogueKey)
        {
            if (dialogueData != null)
            {
                foreach (var data in dialogueData)
                {
                    if (data == null) continue;
                    var d = data.GetDialogue(dialogueKey);
                    if (d != null) return (data, d);
                }
            }

            return (null, null);
        }

        protected bool _StartConversation(string dialogueKey)
        {
            DialogueData            dialogueData = null;
            DialogueData.Dialogue   dialogue = null;
            if (currentDialogueData != null)
            {
                dialogue = currentDialogueData.GetDialogue(dialogueKey);
                if (dialogue != null)
                {
                    dialogueData = currentDialogueData;
                }
            }
            if (dialogue == null)
            {
                (dialogueData, dialogue) = FindDialogue(dialogueKey);
            }
            if (dialogue == null)
            {
                if (currentDialogueData)
                    Debug.LogWarning($"Can't find dialogue key {dialogueKey} in {currentDialogueData.name} nor in global dialogues!");
                else
                    Debug.LogWarning($"Can't find dialogue key {dialogueKey} in global dialogues!");
                return false;
            }

            return _StartConversation(dialogueData, dialogue);
        }

        protected bool _StartConversation(DialogueData dialogueData, string dialogueKey = "")
        {
            var dialogue = (dialogueKey == "") ? (dialogueData.GetFirstDialogue()) : (dialogueData.GetDialogue(dialogueKey));

            return _StartConversation(dialogueData, dialogue);
        }

        protected bool _StartConversation(DialogueData dialogueData, DialogueData.Dialogue dialogue)
        { 
            if (dialogue == null) return false;

            var dialogueKey = dialogue.name;

            if (((dialogue.flags & DialogueData.DialogueFlags.OneShot) != 0) &&
                dialogueCount.ContainsKey(dialogueKey))
            {
                return false;
            }

            if ((currentDialogue != null) && (currentDialogue != dialogue))
            {
                onDialogueEnd?.Invoke();
            }

            currentDialogueData = dialogueData;
            currentDialogue = dialogue;
            currentDialogueIndex = -1;
            currentDisplayedElement = null;

            if (dialogueCount.ContainsKey(dialogueKey))
                dialogueCount[dialogueKey]++;
            else
                dialogueCount[dialogueKey] = 1;

            onDialogueStart?.Invoke(dialogueKey);

            // "{ ... }" blocks run on entering the node, before NextDialogue puts anything on screen, so
            // a beat that says "+1000 ammo" and the number on the HUD agree while it's being read
            RunEntryCode();

            NextDialogue();

            return (currentDialogue != null);

        }

        void NextDialogue()
        {
            if (currentDialogue == null)
            {
                EndDialogue();
                return;
            }

            // Check if it's an option. If every option was filtered out (or only unavailable ones
            // remain, under ShowInvalid), the beat behaves like plain text and falls through.
            if ((currentDialogueIndex >= 0) && (currentDialogue.elems.Count > currentDialogueIndex))
            {
                var displayedElement = currentDisplayedElement ?? currentDialogue.elems[currentDialogueIndex];
                if (HasSelectableOption(displayedElement))
                {
                    // Get selected option
                    int selectedOption = display.GetSelectedOption();
                    if ((selectedOption < 0) || (selectedOption >= displayedElement.options.Count) ||
                        (!displayedElement.options[selectedOption].available))
                    {
                        Debug.LogWarning($"Selected option {selectedOption} isn't an available option of dialogue \"{currentDialogue.name}\"!");
                        return;
                    }
                    var option = displayedElement.options[selectedOption];

                    // Picking an option is also a way of dismissing the beat, so the node's code has to
                    // run here as well - otherwise a "=>{ ... }" block on a node with options would be
                    // unreachable. Where to go next is the option's call, not the code block's.
                    RunCode();

                    // ...and then whatever this particular option carries ("*<text>=>{ ... }-><key>")
                    if (option.hasCode)
                    {
                        var optionContext = GetComponent<Expression.IContext>();
                        ExecuteCode(option.code, optionContext);
                    }

                    _StartConversation(option.key);
                    return;
                }
            }

            // Check if it should select a random sentence
            if ((currentDialogue.flags & DialogueData.DialogueFlags.Random) != 0)
            {
                if (currentDialogueIndex == -1) currentDialogueIndex = UnityEngine.Random.Range(0, currentDialogue.elems.Count);
                else
                {
                    EndDialogue();
                    return;
                }
            }
            else
            {
                // It's not, so move forward - check if there's more text
                currentDialogueIndex++;
            }

            if (currentDialogueIndex < currentDialogue.elems.Count)
            {
                currentDisplayedElement = ExpandText(FilterOptions(currentDialogue.elems[currentDialogueIndex]));
                display.Display(currentDisplayedElement);
            }
            else
            {
                // Check if current dialogue is done (or has nothing), check if it redirects to something.
                // Any code blocks along the way are run by EvaluateNext through the callback.
                if ((currentDialogue.conditionalNext != null) &&
                    (currentDialogue.conditionalNext.Count > 0))
                {
                    // A context is only needed to run code and to evaluate conditions - a plain "=>Key"
                    // redirect doesn't need one, so a missing context is not a reason to skip the walk
                    // (it used to be, which silently turned every redirect into an end of dialogue).
                    var context = GetComponent<Expression.IContext>();

                    var nextKey = currentDialogue.EvaluateNext(context, (code) => ExecuteCode(code, context));
                    if (!string.IsNullOrEmpty(nextKey))
                    {
                        if (!_StartConversation(nextKey)) EndDialogue();
                        return;
                    }
                }

                EndDialogue();
            }
        }

        // Applies option guards ("{<expr>}*", "{50%}*", "{50% && <expr>}*"): evaluates each option's
        // condition and rolls its chance, returning a copy of the element with what survived.
        // Condition failures are kept - marked unavailable - when the dialogue has the ShowInvalid
        // flag, so the UI can grey them out; chance failures are always dropped outright, since a
        // hidden roll is nothing the player can reason about (and is only rolled once the condition
        // has passed). The roll happens once per display, so the options stay put while the player
        // picks one. Elements without guarded options pass through untouched.
        DialogueData.DialogueElement FilterOptions(DialogueData.DialogueElement element)
        {
            if (!element.hasOptions) return element;
            if (!element.options.Exists(o => o.hasCondition || o.hasChance)) return element;

            bool showInvalid = (currentDialogue.flags & DialogueData.DialogueFlags.ShowInvalid) != 0;
            var context = GetComponent<Expression.IContext>();

            var filtered = new DialogueData.DialogueElement
            {
                speaker = element.speaker,
                text = element.text,
                attributes = element.attributes
            };

            foreach (var option in element.options)
            {
                bool available = EvaluateOptionCondition(option, context);

                if ((!available) && (!showInvalid)) continue;
                if (available && option.hasChance && (UnityEngine.Random.Range(0.0f, 100.0f) >= option.chance)) continue;

                filtered.options.Add(new DialogueData.Option
                {
                    text = option.text,
                    key = option.key,
                    code = option.code,
                    condition = option.condition,
                    chance = option.chance,
                    available = available
                });
            }

            return filtered;
        }

        // A condition that can't be evaluated (no context, parse error, unknown function) makes the
        // option available, loudly - hiding content over a typo would be the worse failure mode
        bool EvaluateOptionCondition(DialogueData.Option option, Expression.IContext context)
        {
            if (!option.hasCondition) return true;

            if (context == null)
            {
                Debug.LogWarning($"Can't evaluate option condition \"{option.condition}\" in dialogue \"{currentDialogue.name}\" - no context!");
                return true;
            }

            if (!Expression.TryParse(option.condition, out var expression))
            {
                Debug.LogWarning($"Can't parse option condition \"{option.condition}\"!");
                return true;
            }

            try
            {
                return expression.EvaluateBool(context);
            }
            catch (Expression.ErrorException e)
            {
                Debug.LogWarning($"Option condition \"{option.condition}\" in dialogue \"{currentDialogue.name}\": {e.Message}");
                return true;
            }
        }

        static bool HasSelectableOption(DialogueData.DialogueElement element)
        {
            return (element != null) && element.hasOptions && element.options.Exists(o => o.available);
        }

        void RunEntryCode()
        {
            if ((currentDialogue.entryCode == null) ||
                (currentDialogue.entryCode.Count == 0)) return;

            var context = GetComponent<Expression.IContext>();

            ExecuteCode(currentDialogue.entryCode, context);
        }

        // Runs the current dialogue's code blocks and throws away wherever it would have redirected to.
        // It's the same walk NextDialogue does when a node runs out of text, so the code that runs is
        // exactly the code that would have run had the beat been dismissed without an option.
        void RunCode()
        {
            if ((currentDialogue.conditionalNext == null) ||
                (currentDialogue.conditionalNext.Count == 0)) return;

            var context = GetComponent<Expression.IContext>();

            currentDialogue.EvaluateNext(context, (code) => ExecuteCode(code, context));
        }

        private void ExecuteCode(DialogueData.NextKeyOrCode nextKey, Expression.IContext context)
        {
            if (!nextKey.isCode) return;

            ExecuteCode(nextKey.code, context);
        }

        private void ExecuteCode(List<DialogueData.CodeElem> code, Expression.IContext context)
        {
            if ((code == null) || (code.Count == 0)) return;

            if (context == null)
            {
                Debug.LogError($"Dialogue \"{currentDialogue?.name}\" has code to run, but there's no Expression.IContext component on {gameObject.name}!");
                return;
            }

            foreach (var c in code)
            {
                if (c.type == DialogueData.CodeElem.Type.FunctionCall)
                {
                    FunctionCall(c, context);
                }
                else if (c.type == DialogueData.CodeElem.Type.Attribution)
                {
                    if ((c.expressions == null) || (c.expressions.Count < 1))
                    {
                        throw new Expression.ErrorException("Missing expression for assignment!");
                    }

                    if (Expression.TryParse(c.expressions[0], out var expression))
                    {
                        if (expression.GetDataType(context) == Expression.DataType.Bool)
                            context.SetVariable(c.functionOrVarName, expression.EvaluateBool(context));
                        else
                            context.SetVariable(c.functionOrVarName, expression.EvaluateNumber(context));
                    }
                    else
                    {
                        Debug.LogWarning($"Can't parse expression \"{c.expressions[0]}\"!");
                    }
                }
            }
        }

        void FunctionCall(DialogueData.CodeElem code, Expression.IContext context)
        {
            var type = context.GetType();
            var methodInfo = type.GetMethod(code.functionOrVarName,
                                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (methodInfo == null)
            {
                Debug.LogError($"Method \"{code.functionOrVarName}\" not found in context!");
                return;
            }

            // Check parameters, check parameter types
            List<object> args = new();
            ParameterInfo[] parameters = methodInfo.GetParameters();

            // Count mandatory parameters
            int mandatoryParameters = parameters.Length;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].HasDefaultValue)
                {
                    mandatoryParameters = i;
                    break;
                }
            }

            if (mandatoryParameters > code.expressions.Count)
            {
                Debug.LogError($"Invalid number of argument for \"{code.functionOrVarName}\": expected {mandatoryParameters}, received {code.expressions.Count}!");
            }
            else
            {
                for (int index = 0; index < parameters.Length; index++)
                {
                    ParameterInfo param = parameters[index];

                    if (index >= code.expressions.Count)
                    {
                        // Optional parameter that wasn't given a value - Invoke needs the actual default,
                        // a null would fail to convert on a value type
                        args.Add((param.HasDefaultValue) ? (param.DefaultValue) : (null));
                        continue;
                    }
                    if (Expression.TryParse(code.expressions[index], out var expression))
                    {
                        Type paramType = param.ParameterType;
                        if (paramType == typeof(bool))
                        {
                            var pType = expression.GetDataType(context);
                            if ((pType == Expression.DataType.Bool) || (pType == Expression.DataType.Undefined))
                            {
                                args.Add(expression.EvaluateBool(context));
                            }
                            else
                            {
                                Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} ({code.expressions[index]})!");
                            }
                        }
                        else if (paramType == typeof(float))
                        {
                            var pType = expression.GetDataType(context);
                            if ((pType == Expression.DataType.Number) || (pType == Expression.DataType.Undefined))
                            {
                                args.Add(expression.EvaluateNumber(context));
                            }
                            else
                            {
                                Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} (\"{code.expressions[index]}\")!");
                            }
                        }
                        else if (paramType == typeof(int))
                        {
                            var pType = expression.GetDataType(context);
                            if ((pType == Expression.DataType.Number) || (pType == Expression.DataType.Undefined))
                            {
                                args.Add((int)expression.EvaluateNumber(context));
                            }
                            else
                            {
                                Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} (\"{code.expressions[index]}\")!");
                            }
                        }
                        else if (paramType == typeof(string))
                        {
                            var pType = expression.GetDataType(context);
                            if ((pType == Expression.DataType.String) || (pType == Expression.DataType.Undefined))
                            {
                                args.Add(expression.EvaluateString(context));
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
                if (args.Count >= mandatoryParameters)
                {
                    methodInfo.Invoke(context, args.ToArray());
                }
                else
                {
                    Debug.LogError($"Failed to call method {code.functionOrVarName}!");
                }
            }
        }

        // -----------------------------------------------------------------------------------------
        // "${expression}" inside text. Evaluated against the context at the moment the element is
        // shown, so a line can read "${survivorCount} of the ${sentCount} return" and the game only
        // has to SetVariable the two numbers beforehand. Anything the Expression parser accepts is
        // allowed, not just a name: "${sentCount - survivorCount}" works as well. Because it happens
        // per element, a code block that changes a variable mid-conversation is reflected in the
        // lines that follow it.
        // -----------------------------------------------------------------------------------------

        static readonly Regex textExpressionRegex = new Regex(@"\$\{([^}]*)\}", RegexOptions.Compiled);

        // Returns the element with every "${...}" in its text, option texts and attribute values
        // replaced. The parsed elements belong to the DialogueData asset and are reused by every
        // conversation, so an element with something to expand is copied and the asset is never
        // written to; one with nothing to expand is returned as is.
        DialogueData.DialogueElement ExpandText(DialogueData.DialogueElement element)
        {
            if (!NeedsExpansion(element)) return element;

            var context = GetComponent<Expression.IContext>();
            if (context == null)
            {
                Debug.LogWarning($"Dialogue \"{currentDialogue?.name}\" has ${{...}} in its text, but there's no Expression.IContext component on {gameObject.name} to evaluate it with!");
                return element;
            }

            var expanded = new DialogueData.DialogueElement
            {
                speaker = element.speaker,
                text = ExpandText(element.text, context)
            };
            if (element.options != null)
            {
                foreach (var option in element.options)
                {
                    expanded.options.Add(new DialogueData.Option { text = ExpandText(option.text, context), key = option.key, code = option.code, condition = option.condition, chance = option.chance, available = option.available });
                }
            }
            if (element.attributes != null)
            {
                foreach (var attribute in element.attributes)
                {
                    expanded.attributes.Add(new DialogueData.Attribute { name = attribute.name, value = ExpandText(attribute.value, context) });
                }
            }

            return expanded;
        }

        static bool NeedsExpansion(DialogueData.DialogueElement element)
        {
            if (HasTextExpression(element.text)) return true;
            if (element.options != null)
            {
                foreach (var option in element.options) if (HasTextExpression(option.text)) return true;
            }
            if (element.attributes != null)
            {
                foreach (var attribute in element.attributes) if (HasTextExpression(attribute.value)) return true;
            }

            return false;
        }

        static bool HasTextExpression(string text) => (!string.IsNullOrEmpty(text)) && (text.Contains("${"));

        // Expands every "${...}" in a string. Public so a display or context can run it over text of
        // its own (a title built elsewhere, say) with the same rules.
        public static string ExpandText(string text, Expression.IContext context)
        {
            if (!HasTextExpression(text)) return text;

            return textExpressionRegex.Replace(text, (match) =>
            {
                string source = match.Groups[1].Value.Trim();

                // Anything that can't be evaluated is left exactly as written, so a typo shows up on
                // screen as "${survivorCont}" instead of quietly vanishing
                if (source.Length == 0) return match.Value;
                if (!Expression.TryParse(source, out var expression)) return match.Value;

                try
                {
                    switch (expression.GetDataType(context))
                    {
                        case Expression.DataType.Number: return FormatNumber(expression.EvaluateNumber(context));
                        case Expression.DataType.Bool: return (expression.EvaluateBool(context)) ? ("true") : ("false");
                        case Expression.DataType.String: return expression.EvaluateString(context);
                        default:
                            Debug.LogWarning($"Dialogue text expression \"{source}\" has no value - is the variable set on the context?");
                            return match.Value;
                    }
                }
                catch (Expression.ErrorException e)
                {
                    Debug.LogWarning($"Dialogue text expression \"{source}\": {e.Message}");
                    return match.Value;
                }
            });
        }

        // A count reads as "3", not "3.0"; anything fractional keeps up to two decimals
        static string FormatNumber(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        public void EndDialogue()
        {
            display.Clear();
            currentDisplayedElement = null;
            if (currentDialogue != null)
            {
                currentDialogue = null;
                currentDialogueData = null;
                currentDialogueIndex = -1;

                onDialogueEnd?.Invoke();
            }
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

                if (HasSelectableOption(currentDisplayedElement ?? currentDialogue.elems[currentDialogueIndex])) return true;

                if (currentDialogueIndex < currentDialogue.elems.Count - 1) return true;

                var context = GetComponent<Expression.IContext>();

                return !string.IsNullOrEmpty(currentDialogue.GetNextDialogue(context));
            }
        }

        public static bool HasDialogue(string dialogueKey)
        {
            if (Instance)
            {
                (var dialogueData, var dialogue) = Instance.FindDialogue(dialogueKey);
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
                        var context = Instance.GetComponent<Expression.IContext>();

                        var nextDialogue = dialogue.GetNextDialogue(context);
                        if (string.IsNullOrEmpty(nextDialogue)) return false;

                        (dialogueData, dialogue) = Instance.FindDialogue(nextDialogue);
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
            if (Instance == null)
            {
                Debug.LogError($"Can't start conversation {dialogueKey} - No DialogueManager present!");
                return false;
            }

            return Instance._StartConversation(dialogueKey);
        }

        public static bool StartConversation(DialogueData dialogue, string key = "")
        {
            if (Instance == null)
            {
                Debug.LogError($"Can't start conversation {dialogue.name} - No DialogueManager present!");
                return false;
            }

            return Instance._StartConversation(dialogue, key);
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
}