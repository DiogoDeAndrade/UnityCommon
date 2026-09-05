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
        protected Dictionary<string, int>   dialogueEvents = new();

        // All the stateful things a dialogue does (one-shots, sticky rolls, visit counts, "local."
        // variables) live in a DialogueState. There's always this global one; a conversation can be
        // started with its own instance instead, and then "local" means that instance while "global"
        // still means this one. Hand it to the save system through GlobalState.
        protected DialogueState             globalState = new();
        // The state the current conversation was started with - null when none was passed, in which
        // case the global one is used for everything
        protected DialogueState             currentLocalState = null;

        // Where the conversation has been: one entry per node that actually displayed something
        // (pure redirects don't count - jumping "back" into one would just redirect again), the
        // current node last. "-> History(-n)" indexes this from the end.
        protected List<(DialogueData data, DialogueData.Dialogue dialogue)> history = new();
        // "{Marker=<name>}" nodes, recorded when entered - "-> Marker(<name>)" jumps back to them.
        // Like the history, this only lives for the duration of the conversation.
        protected Dictionary<string, (DialogueData data, DialogueData.Dialogue dialogue)> markers = new();

        // The state the current conversation resolves "local" against
        protected DialogueState activeState => currentLocalState ?? globalState;

        // The DialogueState of whatever conversation is running (the global one when none was
        // passed, or when nothing is running) - this is what "local.<var>" routes to
        public static DialogueState ActiveOrGlobalState => (Instance != null) ? (Instance.activeState) : (null);

        // The global DialogueState - the thing to serialize in a save game (DialogueState.SerializeThis /
        // DeserializeThis), or to replace on load
        public static DialogueState GlobalState
        {
            get => (Instance != null) ? (Instance.globalState) : (null);
            set { if (Instance != null) Instance.globalState = value ?? new DialogueState(); }
        }

        // A count summed over the active local state and the global one. Any given key is only ever
        // recorded in one of the two (which one depends on the node's {Global} tag), so the sum is
        // simply "the count, wherever it lives".
        public static int GetStateCount(string stateKey)
        {
            if (Instance == null) return 0;

            int count = Instance.globalState.GetCount(stateKey);
            if (Instance.currentLocalState != null) count += Instance.currentLocalState.GetCount(stateKey);

            return count;
        }

        // The state a node's stateful bits should use, given its own flags and the conversation's
        // local state (which may be null - then everything is global anyway)
        static DialogueState GetScopedState(DialogueData.Dialogue dialogue, DialogueState localState, DialogueState globalState)
        {
            bool global = (dialogue.flags & DialogueData.DialogueFlags.Global) != 0;
            return (global) ? (globalState) : (localState ?? globalState);
        }

        // Same, for the things that carry their own scope (one-shot options, one-shot code blocks):
        // an explicit Local/Global on the item wins over the node's default
        static DialogueState GetScopedState(DialogueData.OneShotScope scope, DialogueData.Dialogue dialogue, DialogueState localState, DialogueState globalState)
        {
            switch (scope)
            {
                case DialogueData.OneShotScope.Global: return globalState;
                case DialogueData.OneShotScope.Local: return localState ?? globalState;
                default: return GetScopedState(dialogue, localState, globalState);
            }
        }

        DialogueState ScopedState(DialogueData.Dialogue dialogue) => GetScopedState(dialogue, currentLocalState, globalState);
        DialogueState ScopedState(DialogueData.OneShotScope scope, DialogueData.Dialogue dialogue) => GetScopedState(scope, dialogue, currentLocalState, globalState);

        // The state EvaluateNext/FilterOptions keep their sticky rolls in - null when the node wants
        // a fresh roll every time
        DialogueState RollState(DialogueData.Dialogue dialogue) => (dialogue.alwaysRoll) ? (null) : (ScopedState(dialogue));

        (DialogueData, DialogueData.Dialogue) FindDialogue(string dialogueKey)
        {
            if (dialogueData != null)
            {
                foreach (var data in dialogueData)
                {
                    if (data == null) continue;
                    var d = data.FindDialogue(dialogueKey);
                    if (d != null) return (data, d);
                }
            }

            return (null, null);
        }

        protected bool _StartConversation(string dialogueKey)
        {
            // "-> End" is the reserved "we're done here" target: whatever code the option carried has
            // already run, the conversation just closes. (Which is why no node may be named End.)
            if (dialogueKey == "End")
            {
                EndDialogue();
                return true;
            }

            // "History(-1)" / "Marker(name)" aren't keys at all - they resolve against where this
            // conversation has already been
            if (TryResolveSpecialTarget(dialogueKey, out var specialData, out var specialDialogue))
            {
                if (specialDialogue == null) return false;

                return _StartConversation(specialData, specialDialogue);
            }

            // Search order: the current file, then whatever it includes (transitively), and only
            // then the manager's global list - so a file triggered directly doesn't need to be
            // registered anywhere for its own keys and its includes' keys to work
            DialogueData            dialogueData = null;
            DialogueData.Dialogue   dialogue = null;
            if (currentDialogueData != null)
            {
                (dialogueData, dialogue) = currentDialogueData.FindDialogueInHierarchy(dialogueKey);
            }
            if (dialogue == null)
            {
                (dialogueData, dialogue) = FindDialogue(dialogueKey);
            }
            if (dialogue == null)
            {
                if (currentDialogueData)
                    DebugHelpers.LogWarning($"Can't find dialogue key {dialogueKey} in {currentDialogueData.name}, its includes, nor in global dialogues!");
                else
                    DebugHelpers.LogWarning($"Can't find dialogue key {dialogueKey} in global dialogues!");
                return false;
            }

            return _StartConversation(dialogueData, dialogue);
        }

        static readonly Regex historyTargetRegex = new Regex(@"^History\(\s*(-?\d+)\s*\)$", RegexOptions.Compiled);
        static readonly Regex markerTargetRegex = new Regex(@"^Marker\(\s*([A-Za-z0-9:_-]+)\s*\)$", RegexOptions.Compiled);

        // Recognizes the special jump targets. Returns false when the key is an ordinary key; true
        // with a null dialogue when it *was* special but couldn't resolve (no such marker, not enough
        // history), which behaves like jumping to an undefined key.
        bool TryResolveSpecialTarget(string dialogueKey, out DialogueData data, out DialogueData.Dialogue dialogue)
        {
            data = null;
            dialogue = null;

            var historyMatch = historyTargetRegex.Match(dialogueKey);
            if (historyMatch.Success)
            {
                int offset = int.Parse(historyMatch.Groups[1].Value);
                if (offset > 0)
                {
                    DebugHelpers.LogWarning($"History({offset}) - history offsets are zero or negative (History(0) restarts the current node, History(-1) goes back one)!");
                    return true;
                }

                // The current node is the last entry, so History(0) is it and History(-n) counts back
                int index = history.Count - 1 + offset;
                if (index < 0)
                {
                    DebugHelpers.LogWarning($"History({offset}) goes back further than this conversation has been ({history.Count} node(s) deep)!");
                    return true;
                }

                (data, dialogue) = history[index];

                // Going back rewinds the trail - the nodes we're leaving behind are dropped, so
                // going back twice keeps going *back*, instead of ping-ponging between two nodes.
                // The target itself is dropped too: it re-records itself on entry.
                history.RemoveRange(index, history.Count - index);

                return true;
            }

            var markerMatch = markerTargetRegex.Match(dialogueKey);
            if (markerMatch.Success)
            {
                string markerName = markerMatch.Groups[1].Value;
                if (!markers.TryGetValue(markerName, out var entry))
                {
                    DebugHelpers.LogWarning($"Marker({markerName}) - no node with {{Marker={markerName}}} has been visited in this conversation!");
                    return true;
                }

                (data, dialogue) = entry;

                return true;
            }

            return false;
        }

        protected bool _StartConversation(DialogueData dialogueData, string dialogueKey = "")
        {
            if (dialogueKey == "")
            {
                return _StartConversation(dialogueData, dialogueData.GetFirstDialogue());
            }

            // The key can live in the file itself or in anything it includes
            var (foundData, dialogue) = dialogueData.FindDialogueInHierarchy(dialogueKey);
            if (dialogue == null)
            {
                DebugHelpers.LogWarning($"Can't find dialogue key {dialogueKey} in {dialogueData.name} nor its includes!");
                return false;
            }

            return _StartConversation(foundData, dialogue);
        }

        // The state a node's visit count lives in. Normally the node's default scope ({Global} or
        // not), but a one-shot flag drags the count into *its* scope - the check and the recording
        // have to agree on where the count is, or {GlobalOneShot} on an otherwise-local node would
        // check a count that never gets written.
        static DialogueState GetVisitState(DialogueData.Dialogue dialogue, DialogueState localState, DialogueState globalState)
        {
            if ((dialogue.flags & DialogueData.DialogueFlags.GlobalOneShot) != 0) return globalState;
            if ((dialogue.flags & DialogueData.DialogueFlags.OneShot) != 0) return localState ?? globalState;

            return GetScopedState(dialogue, localState, globalState);
        }

        // True when the node's one-shot has already been spent
        static bool IsOneShotSpent(DialogueData.Dialogue dialogue, DialogueState localState, DialogueState globalState)
        {
            bool oneShot = (dialogue.flags & (DialogueData.DialogueFlags.OneShot | DialogueData.DialogueFlags.GlobalOneShot)) != 0;
            if (!oneShot) return false;

            return GetVisitState(dialogue, localState, globalState).GetCount(dialogue.name) > 0;
        }

        protected bool _StartConversation(DialogueData dialogueData, DialogueData.Dialogue dialogue)
        {
            if (dialogue == null) return false;

            var dialogueKey = dialogue.name;

            if (IsOneShotSpent(dialogue, currentLocalState, globalState))
            {
                return false;
            }

            // First node of a fresh conversation: the entry file is now known, so its markers (and
            // its includes') can be registered before anything runs
            if (markersNeedInit)
            {
                markersNeedInit = false;
                PreRegisterMarkers(dialogueData);
            }

            if ((currentDialogue != null) && (currentDialogue != dialogue))
            {
                onDialogueEnd?.Invoke();
            }

            currentDialogueData = dialogueData;
            currentDialogue = dialogue;
            currentDialogueIndex = -1;
            currentDisplayedElement = null;

            // The visit count is what one-shots, Visits() and HasSeen() read. Which state it lands in
            // is the node's call ({Global}, one-shot flags); GetStateCount sums both, so queries
            // don't care.
            GetVisitState(dialogue, currentLocalState, globalState).IncrementCount(dialogueKey);

            // Only nodes that show something make it into the history - jumping "back" into a pure
            // redirect would just redirect again
            if (!dialogue.isRedirect)
            {
                history.Add((dialogueData, dialogue));
            }

            if (!string.IsNullOrEmpty(dialogue.marker))
            {
                markers[dialogue.marker] = (dialogueData, dialogue);
            }

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
                        DebugHelpers.LogWarning($"Selected option {selectedOption} isn't an available option of dialogue \"{currentDialogue.name}\"!");
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

                    // A "{OneShot}*" option spends itself on being picked - next time FilterOptions
                    // sees the recorded pick and drops (or greys) it
                    if (option.isOneShot)
                    {
                        ScopedState(option.oneShot, currentDialogue).IncrementCount(currentDialogue.OptionPickStateKey(option));
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

                    var nextKey = currentDialogue.EvaluateNext(context, (code) => ExecuteCode(code, context), RollState(currentDialogue));
                    if (!string.IsNullOrEmpty(nextKey))
                    {
                        if (!_StartConversation(nextKey)) EndDialogue();
                        return;
                    }
                }

                EndDialogue();
            }
        }

        // Applies option guards ("{<expr>}*", "{50%}*", "{OneShot}*" and combinations): evaluates
        // each option's condition, checks its one-shot, and rolls its chance, returning a copy of the
        // element with what survived. Condition failures are kept - marked unavailable - when the
        // dialogue has the ShowInvalid flag, so the UI can grey them out; a spent one-shot vanishes
        // unless the dialogue has {ShowOneShot}, in which case it is also greyed; chance failures are
        // always dropped outright, since a hidden roll is nothing the player can reason about (and is
        // only rolled once the condition has passed). Chance rolls are sticky: the outcome is stored
        // in the DialogueState the first time and read back afterwards, unless the node has
        // {AlwaysRoll} - then it's rolled fresh on every display, the old behaviour. Elements without
        // guarded options pass through untouched.
        DialogueData.DialogueElement FilterOptions(DialogueData.DialogueElement element)
        {
            if (!element.hasOptions) return element;
            if (!element.options.Exists(o => o.hasCondition || o.hasChance || o.isOneShot)) return element;

            bool showInvalid = (currentDialogue.flags & DialogueData.DialogueFlags.ShowInvalid) != 0;
            bool showOneShot = (currentDialogue.flags & DialogueData.DialogueFlags.ShowOneShot) != 0;
            var context = GetComponent<Expression.IContext>();
            var rollState = RollState(currentDialogue);

            var filtered = new DialogueData.DialogueElement
            {
                speaker = element.speaker,
                text = element.text,
                attributes = element.attributes
            };

            foreach (var option in element.options)
            {
                bool conditionOk = EvaluateOptionCondition(option, context);
                bool oneShotSpent = option.isOneShot &&
                    (ScopedState(option.oneShot, currentDialogue).GetCount(currentDialogue.OptionPickStateKey(option)) > 0);

                // A spent one-shot is gone, full stop, unless {ShowOneShot} keeps it greyed out
                if (oneShotSpent && (!showOneShot)) continue;

                bool available = conditionOk && (!oneShotSpent);
                if (!available)
                {
                    // Unavailable but possibly still shown: a spent one-shot got here because
                    // ShowOneShot wants it visible; a failed condition needs ShowInvalid
                    bool keepVisible = oneShotSpent || ((!conditionOk) && showInvalid);
                    if (!keepVisible) continue;
                }

                if (available && option.hasChance && (!RollOptionChance(option, rollState))) continue;

                filtered.options.Add(new DialogueData.Option
                {
                    text = option.text,
                    sourceText = option.stateText,
                    key = option.key,
                    code = option.code,
                    condition = option.condition,
                    chance = option.chance,
                    oneShot = option.oneShot,
                    available = available
                });
            }

            return filtered;
        }

        // The "{50%}*" roll, made sticky through the DialogueState: once rolled, the same outcome
        // comes back on every later visit. A null state ({AlwaysRoll}) rolls fresh each time.
        bool RollOptionChance(DialogueData.Option option, DialogueState rollState)
        {
            if (rollState == null)
            {
                return UnityEngine.Random.Range(0.0f, 100.0f) < option.chance;
            }

            var stateKey = currentDialogue.OptionChanceStateKey(option);
            if (rollState.TryGetChanceRoll(stateKey, out bool stored)) return stored;

            bool passed = UnityEngine.Random.Range(0.0f, 100.0f) < option.chance;
            rollState.SetChanceRoll(stateKey, passed);

            return passed;
        }

        // A condition that can't be evaluated (no context, parse error, unknown function) makes the
        // option available, loudly - hiding content over a typo would be the worse failure mode
        bool EvaluateOptionCondition(DialogueData.Option option, Expression.IContext context)
        {
            if (!option.hasCondition) return true;

            if (context == null)
            {
                DebugHelpers.LogWarning($"Can't evaluate option condition \"{option.condition}\" in dialogue \"{currentDialogue.name}\" - no context!");
                return true;
            }

            if (!Expression.TryParse(option.condition, out var expression))
            {
                DebugHelpers.LogWarning($"Can't parse option condition \"{option.condition}\"!");
                return true;
            }

            try
            {
                return expression.EvaluateBool(context);
            }
            catch (Expression.ErrorException e)
            {
                DebugHelpers.LogWarning($"Option condition \"{option.condition}\" in dialogue \"{currentDialogue.name}\": {e.Message}");
                return true;
            }
        }

        static bool HasSelectableOption(DialogueData.DialogueElement element)
        {
            return (element != null) && element.hasOptions && element.options.Exists(o => o.available);
        }

        void RunEntryCode()
        {
            if (!currentDialogue.hasEntryCode) return;

            var context = GetComponent<Expression.IContext>();
            bool anyRan = false;

            for (int i = 0; i < currentDialogue.entryBlocks.Count; i++)
            {
                var block = currentDialogue.entryBlocks[i];
                if ((block.code == null) || (block.code.Count == 0)) continue;

                // A "{OneShot}{" block spends itself on running; a plain "{" block runs every time
                if (block.oneShot != DialogueData.OneShotScope.None)
                {
                    var state = ScopedState(block.oneShot, currentDialogue);
                    var blockKey = currentDialogue.EntryBlockStateKey(i);

                    if (state.GetCount(blockKey) > 0) continue;
                    state.IncrementCount(blockKey);
                }

                ExecuteCode(block.code, context);
                anyRan = true;
            }

            // The aggregate HasRun() reads, kept in the node's default scope beside its visit count
            if (anyRan)
            {
                ScopedState(currentDialogue).IncrementCount(currentDialogue.EntryCodeStateKey());
            }
        }

        // Runs the current dialogue's code blocks and throws away wherever it would have redirected to.
        // It's the same walk NextDialogue does when a node runs out of text, so the code that runs is
        // exactly the code that would have run had the beat been dismissed without an option.
        void RunCode()
        {
            if ((currentDialogue.conditionalNext == null) ||
                (currentDialogue.conditionalNext.Count == 0)) return;

            var context = GetComponent<Expression.IContext>();

            currentDialogue.EvaluateNext(context, (code) => ExecuteCode(code, context), RollState(currentDialogue));
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
                DebugHelpers.LogError($"Dialogue \"{currentDialogue?.name}\" has code to run, but there's no Expression.IContext component on {gameObject.name}!");
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
                        DebugHelpers.LogWarning($"Can't parse expression \"{c.expressions[0]}\"!");
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
                DebugHelpers.LogError($"Method \"{code.functionOrVarName}\" not found in context!");
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
                DebugHelpers.LogError($"Invalid number of argument for \"{code.functionOrVarName}\": expected {mandatoryParameters}, received {code.expressions.Count}!");
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
                                DebugHelpers.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} ({code.expressions[index]})!");
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
                                DebugHelpers.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} (\"{code.expressions[index]}\")!");
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
                                DebugHelpers.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} (\"{code.expressions[index]}\")!");
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
                                DebugHelpers.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\", received {pType} ({code.expressions[index]})!");
                            }
                        }
                        else
                        {
                            DebugHelpers.LogError($"Unsupported type {paramType} for argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\"!");
                        }
                    }
                    else
                    {
                        DebugHelpers.LogError($"Failed to parse argument #{index} ({param.Name}) for call to \"{code.functionOrVarName}\" ({code.expressions[index]})!");
                        continue;
                    }
                }
                if (args.Count >= mandatoryParameters)
                {
                    methodInfo.Invoke(context, args.ToArray());
                }
                else
                {
                    DebugHelpers.LogError($"Failed to call method {code.functionOrVarName}!");
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
                DebugHelpers.LogWarning($"Dialogue \"{currentDialogue?.name}\" has ${{...}} in its text, but there's no Expression.IContext component on {gameObject.name} to evaluate it with!");
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
                    expanded.options.Add(new DialogueData.Option { text = ExpandText(option.text, context), sourceText = option.stateText, key = option.key, code = option.code, condition = option.condition, chance = option.chance, oneShot = option.oneShot, available = option.available });
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
                            DebugHelpers.LogWarning($"Dialogue text expression \"{source}\" has no value - is the variable set on the context?");
                            return match.Value;
                    }
                }
                catch (Expression.ErrorException e)
                {
                    DebugHelpers.LogWarning($"Dialogue text expression \"{source}\": {e.Message}");
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

            // History and markers only mean anything inside a conversation, and the local state
            // belongs to whoever passed it - a new conversation brings its own (or none)
            history.Clear();
            markers.Clear();
            currentLocalState = null;
        }

        // Called by the public entry points when a conversation is started from outside: adopts the
        // state the caller passed (null = everything global) and forgets the previous conversation's
        // trail. Node-to-node jumps inside a conversation never come through here.
        void BeginConversationState(DialogueState state)
        {
            currentLocalState = state;
            history.Clear();
            markers.Clear();
            // The actual entry file isn't known until the first node resolves, so the marker
            // pre-registration waits for it
            markersNeedInit = true;
        }

        bool markersNeedInit = false;

        // Markers declared in the conversation's entry file - and in everything it includes - are
        // available from the start, not only after their node has been visited. That's what lets a
        // shared file exit "forward" through "-> Marker(name)" into a node of whoever called it: the
        // caller declares {Marker=name} on its wrap-up node and never has to visit it first. The
        // entry file's own declarations win over its includes' (so a shared file can carry a
        // fallback exit and the caller overrides it), and actually visiting a marked node re-points
        // the marker as usual.
        void PreRegisterMarkers(DialogueData data, HashSet<DialogueData> visited = null)
        {
            if (data == null) return;

            visited ??= new HashSet<DialogueData>();
            if (!visited.Add(data)) return;

            // Includes first, own declarations last - last write wins
            for (int i = 0; i < data.IncludeRefs.Count; i++)
            {
                PreRegisterMarkers(data.GetResolvedInclude(i), visited);
            }

            foreach (var dialogue in data.GetAllDialogues())
            {
                if (!string.IsNullOrEmpty(dialogue.marker))
                {
                    markers[dialogue.marker] = (data, dialogue);
                }
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

                var nextKey = currentDialogue.GetNextDialogue(context, RollState(currentDialogue));
                // A redirect to the reserved End target is a way of stopping, not more text
                return (!string.IsNullOrEmpty(nextKey)) && (nextKey != "End");
            }
        }

        // Key lookup the way a running conversation would do it: the given file and its includes
        // first (when there is one), the manager's global list otherwise
        (DialogueData, DialogueData.Dialogue) ResolveDialogue(DialogueData data, string dialogueKey)
        {
            if (data != null)
            {
                var result = data.FindDialogueInHierarchy(dialogueKey);
                if (result.dialogue != null) return result;
            }

            return FindDialogue(dialogueKey);
        }

        // Whether starting this conversation would actually show something, one-shots included -
        // checked against the same state the conversation would be started with (null = global, like
        // StartConversation). Pass the DialogueData to scope the lookup the way
        // StartConversation(data, ...) would.
        public static bool HasDialogue(string dialogueKey, DialogueState state = null) => HasDialogue(null, dialogueKey, state);

        public static bool HasDialogue(DialogueData data, string dialogueKey, DialogueState state = null)
        {
            if (Instance == null) return false;

            var (dialogueData, dialogue) = Instance.ResolveDialogue(data, dialogueKey);
            var context = Instance.GetComponent<Expression.IContext>();

            // Follows redirect-only nodes to see whether the chain ends anywhere real. The peek
            // never rolls or runs code, but it does read sticky outcomes, so a conversation whose
            // roll was already made answers for the branch it actually took.
            int guard = 0;
            while (dialogue != null)
            {
                if (++guard > 64)
                {
                    DebugHelpers.LogWarning($"HasDialogue(\"{dialogueKey}\"): redirect chain never ends!");
                    return false;
                }

                if (IsOneShotSpent(dialogue, state, Instance.globalState)) return false;

                if (!dialogue.isRedirect) return true;

                var rollState = (dialogue.alwaysRoll) ? (null) : (GetScopedState(dialogue, state, Instance.globalState));
                var nextDialogue = dialogue.GetNextDialogue(context, rollState);
                if (string.IsNullOrEmpty(nextDialogue)) return false;

                (dialogueData, dialogue) = Instance.ResolveDialogue(dialogueData, nextDialogue);
            }

            return false;
        }

        // Whether the node was ever visited. With a state, that state plus the global one are
        // checked; without one, whatever conversation is running (or just the global state).
        public static bool HasSaidDialogue(string dialogueKey, DialogueState state = null)
        {
            if (Instance == null) return false;

            if (state != null)
            {
                return (state.GetCount(dialogueKey) + Instance.globalState.GetCount(dialogueKey)) > 0;
            }

            return GetStateCount(dialogueKey) > 0;
        }

        // state is the conversation's DialogueState: one-shots, sticky rolls and "local." variables
        // resolve against it. Null uses the global state for everything - which is exactly the old
        // behaviour, so existing callers don't change.
        public static bool StartConversation(string dialogueKey, DialogueState state = null)
        {
            if (Instance == null)
            {
                DebugHelpers.LogError($"Can't start conversation {dialogueKey} - No DialogueManager present!");
                return false;
            }

            Instance.BeginConversationState(state);

            bool started = Instance._StartConversation(dialogueKey);
            if (!started) Instance.currentLocalState = null;

            return started;
        }

        public static bool StartConversation(DialogueData dialogue, string key = "", DialogueState state = null)
        {
            if (Instance == null)
            {
                DebugHelpers.LogError($"Can't start conversation {dialogue.name} - No DialogueManager present!");
                return false;
            }

            Instance.BeginConversationState(state);

            bool started = Instance._StartConversation(dialogue, key);
            if (!started) Instance.currentLocalState = null;

            return started;
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