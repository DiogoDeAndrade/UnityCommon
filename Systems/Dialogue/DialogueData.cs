using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{

    [CreateAssetMenu(fileName = "Dialogue Data", menuName = "Unity Common/Dialogue/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [Flags]
        public enum DialogueFlags
        {
            None = 0,
            // The node only plays once, tracked in the conversation's DialogueState (so a POI that
            // passed its own state gets its own once). GlobalOneShot tracks in the global state
            // instead - once anywhere is once everywhere.
            OneShot = 1,
            Random = 2,
            // Options whose condition failed are still handed to the display, marked unavailable,
            // instead of being dropped - so the player can see what the choice would have been.
            ShowInvalid = 4,
            GlobalOneShot = 8,
            // Default scope for everything stateful in this node (rolls, and anything that doesn't
            // name its own scope): Global stores in the global DialogueState instead of the
            // conversation's. Local is the default and changes nothing - it exists to be written.
            Global = 16,
            Local = 32,
            // Weighted groups and option chances are normally rolled once and the outcome kept in
            // the DialogueState; this rolls them fresh on every entry instead.
            AlwaysRoll = 64,
            // A spent one-shot option normally vanishes; with this it stays visible, greyed out,
            // the way ShowInvalid keeps failed conditions visible.
            ShowOneShot = 128
        };

        // Where a one-shot mark is stored: None = not a one-shot at all; Local = the conversation's
        // DialogueState (which is the global one when none was passed); Global = always the global one.
        public enum OneShotScope { None, Local, Global };

        [Serializable]
        public class Option
        {
            public string text;
            public string key;
            // "{<expr>}*<text>": the option only exists while the expression is true. Empty = always.
            public string condition;
            // "{50%}*<text>" / "{50% && <expr>}*<text>": chance (in percent) of the option appearing
            // at all, rolled once each time the element is displayed. Negative = no roll. Unlike the
            // weights of a "{25%}=>" group this is an absolute probability, not a share of a group.
            public float chance = -1.0f;
            // Set by the manager on the copies it hands to the display: false when the condition
            // failed but the dialogue's ShowInvalid flag kept the option visible, so the UI can grey
            // it out. Options that fail their chance roll are dropped outright instead - a hidden
            // roll is nothing the player can reason about.
            public bool available = true;
            // Code written as part of the option itself ("*<text>=>{ ... }-><key>"). It runs when this
            // option is picked, before going to key - which is the only way to attach a side effect to
            // one specific choice instead of to the whole beat.
            public List<CodeElem> code;
            // "{OneShot}*<text>" / "{GlobalOneShot}*<text>": the option can only be picked once,
            // tracked in the DialogueState by the option's text. Combinable with the other guard
            // parts: "{OneShot && HasRations(500)}*...".
            public OneShotScope oneShot = OneShotScope.None;

            // Set by the manager on displayed copies whose text was "${...}"-expanded: the text as
            // written in the file, which is what state keys are built from - the expanded text can
            // change between visits, the written one can't.
            [NonSerialized] public string sourceText;

            public bool hasCode => (code != null) && (code.Count > 0);
            public bool hasCondition => !string.IsNullOrEmpty(condition);
            public bool hasChance => chance >= 0.0f;
            public bool isOneShot => oneShot != OneShotScope.None;
            public string stateText => string.IsNullOrEmpty(sourceText) ? text : sourceText;
        }

        // Free-form metadata attached to an element with "@name=value". The dialogue system doesn't
        // interpret these, it just carries them - it's up to the display/context to decide what
        // something like "@image=portrait_01" means for that particular game.
        [Serializable]
        public class Attribute
        {
            public string name;
            public string value;
        }

        [Serializable]
        public class DialogueElement
        {
            public Speaker speaker;
            public string text;
            public List<Option> options = new List<Option>();
            public List<Attribute> attributes = new List<Attribute>();

            public bool hasOptions => (options != null) && (options.Count > 0);
            public bool hasAttributes => (attributes != null) && (attributes.Count > 0);

            public bool HasAttribute(string name) => FindAttribute(name) != null;

            public string GetAttribute(string name, string defaultValue = null)
            {
                var attribute = FindAttribute(name);
                return (attribute != null) ? (attribute.value) : (defaultValue);
            }

            public float GetAttributeNumber(string name, float defaultValue = 0.0f)
            {
                var attribute = FindAttribute(name);
                if (attribute == null) return defaultValue;

                return float.TryParse(attribute.value, System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
            }

            // An attribute with no value at all ("@hidden") counts as true, so flags can be written without "=yes"
            public bool GetAttributeBool(string name, bool defaultValue = false)
            {
                var attribute = FindAttribute(name);
                if (attribute == null) return defaultValue;
                if (string.IsNullOrEmpty(attribute.value)) return true;

                return (attribute.value == "true") || (attribute.value == "yes") || (attribute.value == "1");
            }

            private Attribute FindAttribute(string name)
            {
                if (attributes == null) return null;

                return attributes.Find(a => string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        [Serializable]
        public class CodeElem
        {
            public enum Type { FunctionCall, Attribution };

            public Type type;
            public string functionOrVarName;
            public List<string> expressions;
        }

        [Serializable]
        public class NextKeyOrCode
        {
            public string nextKey;
            public List<CodeElem> code;

            public bool isCode => (code != null) && (code.Count > 0);
        }

        [Serializable]
        public class DialogueCondition
        {
            public string condition; // condition as a string, to be parsed later
            public NextKeyOrCode nextKey;

            // Set by the "{25%}=>Something" form: this entry is part of a weighted random group instead
            // of being guarded by an expression. Weights are relative and normalized inside the group.
            public bool isRandom;
            public float weight;
        }

        // One "{ ... }" block, in the order it was written. A block prefixed "{OneShot}{" (or
        // "{GlobalOneShot}{") only runs the first time, tracked in the DialogueState.
        [Serializable]
        public class EntryBlock
        {
            public OneShotScope oneShot = OneShotScope.None;
            public List<CodeElem> code = new();
        }

        [Serializable]
        public class Dialogue
        {
            public string name;
            public DialogueFlags flags;
            // "{Marker=<name>}": entering this node records it under <name>, and "-> Marker(<name>)"
            // from anywhere in the conversation - other files included - jumps back to it.
            public string marker;
            public List<DialogueElement> elems = new();

            // new support for conditional next keys
            public List<DialogueCondition> conditionalNext = new();

            // Code written as "{ ... }" with no arrow. It runs when the node is entered, before any of
            // its text is shown - as opposed to "=>{ ... }", which runs on the way out. Kept as
            // separate blocks because each can carry its own one-shot marker.
            public List<EntryBlock> entryBlocks = new();

            public bool isRedirect => (elems == null) || (elems.Count == 0);
            public bool hasEntryCode => (entryBlocks != null) && (entryBlocks.Count > 0);
            public bool alwaysRoll => (flags & DialogueFlags.AlwaysRoll) != 0;

            // State key of the n-th weighted "{25%}=>" group of this node (n in order of appearance)
            public string GroupStateKey(int groupOrdinal) => $"{name}#group{groupOrdinal}";
            public string OptionPickStateKey(Option option) => $"{name}#opt:{option.stateText}";
            public string OptionChanceStateKey(Option option) => $"{name}#chance:{option.stateText}";
            public string EntryBlockStateKey(int blockIndex) => $"{name}#code{blockIndex}";
            // Aggregate, bumped whenever any of the node's entry code runs - what HasRun() reads
            public string EntryCodeStateKey() => $"{name}#code";

            // Resolves where this dialogue goes next without causing any side effect: code blocks are
            // skipped instead of run, and random groups resolve to their stored sticky outcome when
            // rollState has one - or to their first entry, never rolling. This is what the "is there
            // anything to say?" queries need.
            public string GetNextDialogue(Expression.IContext context, DialogueState rollState = null) => EvaluateNext(context, null, rollState);

            // Walks the conditionalNext list in order and returns the key it ends up redirecting to:
            //   - a random group is a run of consecutive "{25%}=>" entries; one of them is picked by
            //     weight (or the first one, when peeking) and the rest of the run is skipped
            //   - a code block doesn't redirect anywhere, so it's executed and the walk *continues*,
            //     which is what makes "=>{ ... }" followed by "=>SomeKey" work
            //   - the first entry that resolves to a key wins
            // Passing a null codeExecutor makes this a peek (see GetNextDialogue).
            // rollState makes weighted groups sticky: the chosen branch is stored in it the first time
            // and read back afterwards (unless the node has {AlwaysRoll}). Null = roll fresh each time.
            public string EvaluateNext(Expression.IContext context, Action<NextKeyOrCode> codeExecutor, DialogueState rollState = null)
            {
                if (conditionalNext == null) return null;

                bool peek = (codeExecutor == null);
                int index = 0;
                int groupOrdinal = 0;

                if (alwaysRoll) rollState = null;

                while (index < conditionalNext.Count)
                {
                    DialogueCondition condition;

                    if (conditionalNext[index].isRandom)
                    {
                        int groupEnd = index;
                        while ((groupEnd < conditionalNext.Count) && (conditionalNext[groupEnd].isRandom)) groupEnd++;

                        condition = SelectRandom(index, groupEnd, peek, rollState, GroupStateKey(groupOrdinal));
                        groupOrdinal++;
                        index = groupEnd;

                        if (condition == null) continue;
                    }
                    else
                    {
                        condition = conditionalNext[index];
                        index++;

                        if (!string.IsNullOrEmpty(condition.condition))
                        {
                            if (context == null)
                            {
                                DebugHelpers.LogWarning($"Can't evaluate \"{condition.condition}\" in dialogue \"{name}\" - no context!");
                                continue;
                            }

                            if (Expression.TryParse(condition.condition, out var expression))
                            {
                                if (!expression.EvaluateBool(context)) continue;
                            }
                            else
                            {
                                DebugHelpers.LogWarning($"Can't parse expression \"{condition.condition}\"!");
                                continue;
                            }
                        }
                    }

                    if (condition.nextKey.isCode)
                    {
                        codeExecutor?.Invoke(condition.nextKey);
                        continue;
                    }

                    return condition.nextKey.nextKey;
                }

                return null;
            }

            private DialogueCondition SelectRandom(int startIndex, int endIndex, bool peek, DialogueState rollState, string stateKey)
            {
                // A stored outcome wins, for peeks too - so "what would happen" and "what happens"
                // agree once the roll has been made
                if ((rollState != null) && (rollState.TryGetGroupRoll(stateKey, out var storedKey)))
                {
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if ((conditionalNext[i].nextKey?.nextKey == storedKey) && (conditionalNext[i].weight > 0.0f))
                            return conditionalNext[i];
                    }

                    // The stored branch is gone or was weighted out (the file was edited) - roll again
                    rollState.ClearGroupRoll(stateKey);
                }

                if (peek)
                {
                    // Deterministic stand-in for the roll: the first entry that could actually be
                    // picked. A peek never rolls, so it also never *stores* - committing an outcome is
                    // something only actually walking the dialogue may do.
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if (conditionalNext[i].weight > 0.0f) return conditionalNext[i];
                    }
                    return null;
                }

                float totalWeight = 0.0f;
                for (int i = startIndex; i < endIndex; i++) totalWeight += Mathf.Max(0.0f, conditionalNext[i].weight);

                if (totalWeight <= 0.0f)
                {
                    DebugHelpers.LogWarning($"Random group in dialogue \"{name}\" has no positive weight!");
                    return null;
                }

                float roll = UnityEngine.Random.Range(0.0f, totalWeight);
                DialogueCondition lastValid = null;

                for (int i = startIndex; i < endIndex; i++)
                {
                    float entryWeight = Mathf.Max(0.0f, conditionalNext[i].weight);
                    // Skipped rather than just weighted out, so a roll of exactly 0 can't land on it
                    if (entryWeight <= 0.0f) continue;

                    lastValid = conditionalNext[i];

                    roll -= entryWeight;
                    if (roll <= 0.0f) break;
                }

                // Remember the outcome so the next entry takes the same branch. A "{25%}=>{ ... }"
                // code entry has no key to remember it by, so a group that picked one stays unrolled -
                // it will roll again next time.
                if ((rollState != null) && (lastValid != null) && (!string.IsNullOrEmpty(lastValid.nextKey?.nextKey)))
                {
                    rollState.SetGroupRoll(stateKey, lastValid.nextKey.nextKey);
                }

                return lastValid;
            }
        }

        [SerializeField] private List<Dialogue> dialogues = new();

        // 'include("Name")' lines: names always survive the import; refs are the same includes
        // resolved to actual assets by the importer, so they are pulled into builds and can be
        // followed without any asset-database lookup. A ref can be null when the other file wasn't
        // imported yet (fresh project, include cycles) - "Unity Common/Dialogue/Update References"
        // reimports everything and errors on whatever still doesn't resolve.
        [SerializeField] private List<string> includeNames = new();
        [SerializeField] private List<DialogueData> includeRefs = new();

        public IReadOnlyList<string> IncludeNames => includeNames;
        public IReadOnlyList<DialogueData> IncludeRefs => includeRefs;

        // Importer-only: stores the asset the include resolved to
        public void SetIncludeRef(int index, DialogueData data)
        {
            if ((index >= 0) && (index < includeRefs.Count)) includeRefs[index] = data;
        }

        public IEnumerable<Dialogue> GetAllDialogues() => dialogues;

        // Where the file being parsed came from, and how far into it we are. Only meaningful during an
        // import - neither is serialized, so at runtime SourceRef falls back to a plain line number.
        private string sourcePath = "";
        private int sourceLine = 0;

        // Unity's console turns "<a href=... line=...>" into a link that opens the file on that line, so
        // a parse error can be clicked instead of hunted for
        private static string SourceRef(string path, int line)
        {
            if (string.IsNullOrEmpty(path)) return $"line {line}";

            return $"<a href=\"{path}\" line=\"{line}\">{path}:{line}</a>";
        }

        private string SourceRef() => SourceRef(sourcePath, sourceLine);

        private Dictionary<string, Speaker> speakerCache = new();
        private Dictionary<string, Dialogue> dialogueCache = new();
        private List<string> keys = null;

        public static DialogueData Import(string filename)
        {
            var newObject = ScriptableObject.CreateInstance<DialogueData>();

            try
            {
                newObject._Import(filename);
            }
            catch (Exception e)
            {
                DebugHelpers.LogError($"Failed to load {SourceRef(filename, 1)}: {e.Message}");
                return null;
            }

            return newObject;
        }

        void _Import(string filename)
        {
            dialogues.Clear();
            includeNames.Clear();
            includeRefs.Clear();
            speakerCache = new();
            dialogueCache = new();
            keys = null;
            sourcePath = filename;
            sourceLine = 0;

            var content = System.IO.File.ReadAllText(filename);

            ParseTextAsset(content);

            RefreshKeys();
        }

        private void ParseTextAsset(string text)
        {
            // A List and not an array because a code block written on a single line is expanded into the
            // multi-line form and pushed back in, right after the line it came from
            List<string> lines = new(text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
            // Which line of the file each entry of "lines" came from. Expanding a single-line block adds
            // entries that all came from the same line, so this stops being 1:1 with the index.
            List<int> lineNumbers = new(Enumerable.Range(1, lines.Count));
            Dialogue currentDialogue = null;
            DialogueElement currentElement = null;
            Speaker currentSpeaker = null;
            List<string> textBuffer = new();
            List<Attribute> attributeBuffer = new();

            bool isParsingCodeBlock = false;
            // Set while the code block being parsed belongs to an option instead of to the node, in
            // which case the line that closes it also carries the option's destination ("}-><key>")
            string currentOptionText = null;
            // Guard of the option whose code block is being parsed ("{<expr>}*<text>=>{"), waiting
            // for the closing "}-><key>" line to become an Option together with it
            string currentOptionCondition = "";
            float currentOptionChance = -1.0f;
            OneShotScope currentOptionOneShot = OneShotScope.None;
            // Set while the code block being parsed is a "{ ... }" entry block instead of a "=>{ ... }",
            // along with the one-shot marker of its "{OneShot}{" prefix, if it had one
            bool isEntryCode = false;
            OneShotScope entryCodeOneShot = OneShotScope.None;
            string currentCondition = "";
            bool currentIsRandom = false;
            float currentWeight = 0.0f;
            List<string> codeBlockBuffer = new();
            List<int> codeBlockLineNumbers = new();
            bool isInBlockComment = false;

            for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                string trimmedLine = lines[lineIdx].Trim();
                sourceLine = lineNumbers[lineIdx];

                // Handle block comments
                if (isInBlockComment)
                {
                    if (trimmedLine.Contains("*/"))
                    {
                        isInBlockComment = false;
                        trimmedLine = trimmedLine.Substring(trimmedLine.IndexOf("*/") + 2).Trim();
                    }
                    else
                    {
                        continue;
                    }
                }

                if (trimmedLine.StartsWith("/*"))
                {
                    isInBlockComment = true;
                    if (trimmedLine.Contains("*/"))
                    {
                        isInBlockComment = false;
                        trimmedLine = trimmedLine.Substring(trimmedLine.IndexOf("*/") + 2).Trim();
                    }
                    else
                    {
                        continue;
                    }
                }

                // Handle single-line comments
                if (trimmedLine.StartsWith("//"))
                {
                    continue;
                }

                if (isParsingCodeBlock)
                {
                    if (trimmedLine.StartsWith("}"))
                    {
                        // ParseCodeStatements walks sourceLine through the block to report each statement
                        // at its own line, so anything said about the closing line has to put it back
                        int closingLine = sourceLine;
                        var code = ParseCodeStatements(codeBlockBuffer, codeBlockLineNumbers);
                        sourceLine = closingLine;

                        if (currentOptionText != null)
                        {
                            // "}-><key>": the code belongs to the option, and so does where it goes next
                            AddOption(currentElement, currentOptionText, trimmedLine.Substring(1).Trim(), code, currentOptionCondition, currentOptionChance, currentOptionOneShot);
                            currentOptionText = null;
                            currentOptionCondition = "";
                            currentOptionChance = -1.0f;
                            currentOptionOneShot = OneShotScope.None;
                        }
                        else if (isEntryCode)
                        {
                            currentDialogue.entryBlocks.Add(new EntryBlock { oneShot = entryCodeOneShot, code = code });
                            isEntryCode = false;
                            entryCodeOneShot = OneShotScope.None;
                        }
                        else
                        {
                            currentDialogue.conditionalNext.Add(new DialogueCondition
                            {
                                condition = currentCondition,
                                isRandom = currentIsRandom,
                                weight = currentWeight,
                                nextKey = new NextKeyOrCode { code = code }
                            });
                        }

                        isParsingCodeBlock = false;
                        currentCondition = "";
                        currentIsRandom = false;
                        currentWeight = 0.0f;
                        codeBlockBuffer.Clear();
                        codeBlockLineNumbers.Clear();
                        continue;
                    }

                    codeBlockBuffer.Add(trimmedLine);
                    codeBlockLineNumbers.Add(sourceLine);
                    continue;
                }

                // A whole code block written on one line is rewritten into the multi-line form and fed
                // back to the parser, so there's only ever one piece of code that reads a block
                var expandedBlock = ExpandSingleLineCodeBlock(trimmedLine);
                if (expandedBlock != null)
                {
                    lines.InsertRange(lineIdx + 1, expandedBlock);
                    lineNumbers.InsertRange(lineIdx + 1, Enumerable.Repeat(sourceLine, expandedBlock.Count));
                    continue;
                }

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer, attributeBuffer);
                    continue;
                }

                if (TryParseInclude(trimmedLine))
                {
                    continue;
                }

                if (trimmedLine.StartsWith("#"))
                {
                    StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer, attributeBuffer);
                    string key = trimmedLine.Substring(1).Trim();
                    currentDialogue = new Dialogue { name = key };
                    dialogues.Add(currentDialogue);
                    currentSpeaker = null;
                    // Attributes that were never claimed by an element belong to the dialogue that just
                    // ended, not to the one starting here
                    attributeBuffer.Clear();
                }
                else if (trimmedLine.StartsWith("[") && trimmedLine.Contains("]:"))
                {
                    StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer, attributeBuffer);
                    int endIdx = trimmedLine.IndexOf("]:");
                    string speakerName = trimmedLine.Substring(1, endIdx - 1);
                    currentSpeaker = GetSpeakerByName(speakerName);
                    string dialogueText = trimmedLine.Substring(endIdx + 2).Trim();
                    currentElement = new DialogueElement { speaker = currentSpeaker };
                    if (!string.IsNullOrEmpty(dialogueText))
                        textBuffer.Add(dialogueText);
                }
                else if ((trimmedLine.StartsWith("*")) || (GetOptionGuardEnd(trimmedLine) >= 0))
                {
                    // "*<text>" or "{<guard>}*<text>" - both are options; the guard, when there is
                    // one, is the option's one-shot marker, condition and/or chance
                    string optionCondition = "";
                    float optionChance = -1.0f;
                    OneShotScope optionOneShot = OneShotScope.None;
                    string optionLine = trimmedLine;

                    int guardEnd = GetOptionGuardEnd(trimmedLine);
                    if (guardEnd >= 0)
                    {
                        ParseOptionGuard(trimmedLine.Substring(1, guardEnd - 1), out optionCondition, out optionChance, out optionOneShot);
                        optionLine = trimmedLine.Substring(guardEnd + 1).TrimStart();
                    }

                    int codeIdx = optionLine.IndexOf("=>{");
                    if (codeIdx >= 0)
                    {
                        // "*<text>=>{" opens a code block that belongs to this option; the destination
                        // comes on the line that closes it
                        currentOptionText = optionLine.Substring(1, codeIdx - 1).Trim();
                        currentOptionCondition = optionCondition;
                        currentOptionChance = optionChance;
                        currentOptionOneShot = optionOneShot;
                        isParsingCodeBlock = true;
                    }
                    else
                    {
                        ParseOption(optionLine, currentElement, optionCondition, optionChance, optionOneShot);
                    }
                }
                else if (trimmedLine.StartsWith("{"))
                {
                    int oneShotBraceIdx = GetEntryOneShotPrefixEnd(trimmedLine, out var oneShotScope);
                    if (oneShotBraceIdx >= 0)
                    {
                        // "{OneShot}{" / "{GlobalOneShot}{": an entry block that only runs the first
                        // time the node is entered, tracked in the corresponding DialogueState
                        currentCondition = "";
                        currentIsRandom = false;
                        currentWeight = 0.0f;
                        isEntryCode = true;
                        entryCodeOneShot = oneShotScope;
                        isParsingCodeBlock = true;
                    }
                    else if (trimmedLine.Contains("}=>{"))
                    {
                        int conditionEnd = trimmedLine.IndexOf("}=>{");
                        ParseCondition(trimmedLine.Substring(1, conditionEnd - 1), out currentCondition, out currentIsRandom, out currentWeight);
                        isParsingCodeBlock = true;
                    }
                    else if (trimmedLine.Contains("}=>"))
                    {
                        ParseConditionalNext(trimmedLine, currentDialogue);
                    }
                    else if (IsEntryCodeBlock(trimmedLine))
                    {
                        currentCondition = "";
                        currentIsRandom = false;
                        currentWeight = 0.0f;
                        isEntryCode = true;
                        isParsingCodeBlock = true;
                    }
                    else
                    {
                        ParseDialogueFlags(trimmedLine, currentDialogue);
                    }
                }
                else if (trimmedLine.StartsWith("=>{"))
                {
                    currentCondition = "";
                    currentIsRandom = false;
                    currentWeight = 0.0f;
                    isParsingCodeBlock = true;
                }
                else if (trimmedLine.StartsWith("=>"))
                {
                    string nextKey = trimmedLine.Substring(2).Trim();
                    currentDialogue.conditionalNext.Add(new DialogueCondition
                    {
                        condition = "",
                        nextKey = new NextKeyOrCode { nextKey = nextKey }
                    });
                }
                else if (trimmedLine.StartsWith("@"))
                {
                    ParseAttribute(trimmedLine, attributeBuffer);
                }
                else
                {
                    textBuffer.Add(trimmedLine);
                }
            }
            StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer, attributeBuffer);
        }

        // Turns "=>{ A; B; }", "{cond}=>{ A; }" or "*<text>=>{ A; }-><key>" into the lines they would
        // have been written as, so the single-line form is only a spelling and not a second parser:
        //   "=>{" / "A;" / "B;" / "}"
        // Returns null when the line isn't a single-line code block, which includes the case where a
        // block just opens here and continues on the lines below.
        private List<string> ExpandSingleLineCodeBlock(string line)
        {
            if (!line.StartsWith("=>{") && !line.StartsWith("{") && !line.StartsWith("*")) return null;

            int openIdx = line.IndexOf("=>{");
            // "{OneShot}{ ... }" opens at the second brace, past the tag
            int oneShotBraceIdx = GetEntryOneShotPrefixEnd(line, out _);
            // With no arrow the block opens at the "{" itself, which is the entry-code form
            if ((openIdx < 0) && (oneShotBraceIdx < 0) && (!IsEntryCodeBlock(line))) return null;

            int braceIdx = (openIdx >= 0) ? (openIdx + 2) : ((oneShotBraceIdx >= 0) ? (oneShotBraceIdx) : 0);
            // The last one, so a "}" inside a string literal isn't mistaken for the end of the block.
            // For "{cond}=>{" this lands on the condition's own brace, which is before the block even
            // opens - i.e. the block is empty here and continues below.
            int closeIdx = line.LastIndexOf('}');
            if (closeIdx <= braceIdx) return null;

            var expandedBlock = new List<string> { line.Substring(0, braceIdx + 1) };
            expandedBlock.AddRange(SplitStatements(line.Substring(braceIdx + 1, closeIdx - braceIdx - 1)));
            expandedBlock.Add(line.Substring(closeIdx));

            return expandedBlock;
        }

        // Tells the two things that can be written as "{...}" with no arrow apart: an entry code block
        // and a dialogue flags line ("{OneShot}"). Statements have to end with ";" (ParseCodeStatements
        // enforces it) and flag names never contain one, so that is the difference. A "{" that doesn't
        // close on its own line can only be a block, since a flags line has to be complete.
        // A guarded option ("{<expr>}*<text>") also starts with "{" and its text may contain ";", so it
        // is ruled out first.
        private bool IsEntryCodeBlock(string line)
        {
            if (line.Contains("=>")) return false;
            if (GetOptionGuardEnd(line) >= 0) return false;
            if (!line.Contains("}")) return true;

            return line.Contains(";");
        }

        // "{OneShot}{" / "{GlobalOneShot}{": returns the index of the block's own "{" when the line
        // starts with a one-shot entry-code prefix, -1 when it is anything else. Only these two tags
        // are allowed in that position - "{ShowInvalid}{" stays a flags line followed by nonsense,
        // which the flags parser will complain about.
        private static int GetEntryOneShotPrefixEnd(string line, out OneShotScope scope)
        {
            scope = OneShotScope.None;

            if (!line.StartsWith("{")) return -1;

            int closeIdx = line.IndexOf('}');
            if (closeIdx < 0) return -1;

            string tag = line.Substring(1, closeIdx - 1).Trim();

            OneShotScope parsed;
            if (tag == "OneShot") parsed = OneShotScope.Local;
            else if (tag == "GlobalOneShot") parsed = OneShotScope.Global;
            else return -1;

            for (int i = closeIdx + 1; i < line.Length; i++)
            {
                if (char.IsWhiteSpace(line[i])) continue;
                if (line[i] == '{')
                {
                    scope = parsed;
                    return i;
                }
                return -1;
            }

            return -1;
        }

        // A line whose "{...}" guard is immediately followed by "*" is a guarded option
        // ("{<expr>}*<text> -> <key>"). Returns the index of the guard's closing brace, or -1 when the
        // line is not that. The first "}" is taken as the guard's end - an expression has no use for
        // a brace of its own.
        private static int GetOptionGuardEnd(string line)
        {
            if (!line.StartsWith("{")) return -1;

            int guardEnd = line.IndexOf('}');
            if (guardEnd < 0) return -1;

            for (int i = guardEnd + 1; i < line.Length; i++)
            {
                if (char.IsWhiteSpace(line[i])) continue;
                return (line[i] == '*') ? (guardEnd) : (-1);
            }

            return -1;
        }

        // Splits an option guard into its one-shot marker, its chance and its condition: "{<expr>}"
        // is condition only, "{50%}" chance only, and "{50% && <expr>}" is both - the expression
        // gates the option, and only when it passes is the chance rolled. "OneShot"/"GlobalOneShot"
        // written as the first term(s) mark the option as pickable only once ("{OneShot}",
        // "{OneShot && 50%}", "{OneShot && <expr>}"). Like ParseCondition, anything that parses
        // entirely as a number is a chance and the "%" is just the readable convention - but here the
        // number is an absolute probability in percent, not a weight normalised across a group.
        private void ParseOptionGuard(string guardText, out string condition, out float chance, out OneShotScope oneShot)
        {
            condition = guardText.Trim();
            chance = -1.0f;
            oneShot = OneShotScope.None;

            // One-shot markers only count at the front of the guard, where they read as a tag and
            // can't be confused with a term of the expression itself
            bool stripped = true;
            while (stripped && (condition.Length > 0))
            {
                stripped = false;

                int andIdx = condition.IndexOf("&&");
                string head = (andIdx >= 0) ? (condition.Substring(0, andIdx).Trim()) : (condition);

                OneShotScope scope = OneShotScope.None;
                if (head == "OneShot") scope = OneShotScope.Local;
                else if (head == "GlobalOneShot") scope = OneShotScope.Global;

                if (scope != OneShotScope.None)
                {
                    oneShot = scope;
                    condition = (andIdx >= 0) ? (condition.Substring(andIdx + 2).Trim()) : ("");
                    stripped = true;
                }
            }

            int chanceAndIdx = condition.IndexOf("&&");
            string chanceHead = (chanceAndIdx >= 0) ? (condition.Substring(0, chanceAndIdx).Trim()) : (condition);
            string chanceText = (chanceHead.EndsWith("%")) ? (chanceHead.Substring(0, chanceHead.Length - 1).Trim()) : (chanceHead);

            if (float.TryParse(chanceText, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var parsedChance))
            {
                chance = parsedChance;
                condition = (chanceAndIdx >= 0) ? (condition.Substring(chanceAndIdx + 2).Trim()) : ("");
            }
        }

        // Splits a run of statements on ";", ignoring the ones inside a string so Say("a;b") survives.
        // A trailing fragment with no ";" is kept as it is, for ParseCodeStatements to complain about.
        private List<string> SplitStatements(string code)
        {
            var statements = new List<string>();
            bool isInString = false;
            int start = 0;

            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] == '"') isInString = !isInString;
                else if ((code[i] == ';') && (!isInString))
                {
                    var statement = code.Substring(start, i - start).Trim();
                    if (!string.IsNullOrEmpty(statement)) statements.Add(statement + ";");
                    start = i + 1;
                }
            }

            var tail = code.Substring(start).Trim();
            if (!string.IsNullOrEmpty(tail)) statements.Add(tail);

            return statements;
        }

        private void ParseConditionalCode(string line, Dialogue currentDialogue)
        {
            int closeBraceIdx = line.IndexOf("}");
            string condition = line.Substring(1, closeBraceIdx - 1).Trim();

            int arrowIdx = line.IndexOf("=>{");
            string codeBlock = line.Substring(arrowIdx + 3, line.Length - arrowIdx - 4).Trim();

            currentDialogue.conditionalNext.Add(new DialogueCondition
            {
                condition = condition,
                nextKey = new NextKeyOrCode { code = ParseFunctionCalls(codeBlock) }
            });
        }

        private void ParseUnconditionalCode(string line, Dialogue currentDialogue)
        {
            string codeBlock = line.Substring(3, line.Length - 4).Trim();

            currentDialogue.conditionalNext.Add(new DialogueCondition
            {
                condition = "",
                nextKey = new NextKeyOrCode { code = ParseFunctionCalls(codeBlock) }
            });
        }

        // codeLines are the raw lines of one block and codeLineNumbers are the lines of the file they
        // came from, so a bad statement can be reported where it was actually written
        private List<CodeElem> ParseCodeStatements(List<string> codeLines, List<int> codeLineNumbers)
        {
            var statements = new List<CodeElem>();

            for (int i = 0; i < codeLines.Count; i++)
            {
                string statementLine = codeLines[i].Trim();
                sourceLine = codeLineNumbers[i];

                if (string.IsNullOrWhiteSpace(statementLine))
                    continue;

                // Verify each statement ends with a semicolon
                if (!statementLine.EndsWith(";"))
                {
                    DebugHelpers.LogError($"Syntax Error: Missing ';' at end of statement '{statementLine}' ({SourceRef()})");
                    continue; // or optionally throw an exception here if strict behavior desired
                }

                // Remove the trailing semicolon for parsing
                statementLine = statementLine.Substring(0, statementLine.Length - 1).Trim();

                if (statementLine.Contains("="))
                {
                    var splitAssignment = statementLine.Split(new[] { '=' }, 2);

                    if (splitAssignment.Length != 2)
                    {
                        DebugHelpers.LogError($"Invalid assignment syntax: {statementLine} ({SourceRef()})");
                        continue;
                    }

                    statements.Add(new CodeElem
                    {
                        type = CodeElem.Type.Attribution,
                        functionOrVarName = splitAssignment[0].Trim(),
                        expressions = new List<string> { splitAssignment[1].Trim() }
                    });
                }
                else
                {
                    int openParenIdx = statementLine.IndexOf('(');
                    int closeParenIdx = statementLine.LastIndexOf(')');

                    if (openParenIdx < 0 || closeParenIdx < 0 || closeParenIdx <= openParenIdx)
                    {
                        DebugHelpers.LogError($"Malformed function call detected: {statementLine} ({SourceRef()})");
                        continue;
                    }

                    string functionName = statementLine.Substring(0, openParenIdx).Trim();
                    string parametersBlock = statementLine.Substring(openParenIdx + 1, closeParenIdx - openParenIdx - 1);

                    var parameters = new List<string>();

                    if (!string.IsNullOrWhiteSpace(parametersBlock))
                    {
                        parameters.AddRange(parametersBlock.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                           .Select(p => p.Trim()));
                    }

                    statements.Add(new CodeElem
                    {
                        type = CodeElem.Type.FunctionCall,
                        functionOrVarName = functionName,
                        expressions = parameters
                    });
                }
            }

            return statements;
        }

        private List<CodeElem> ParseFunctionCalls(string codeBlock)
        {
            var functionCalls = new List<CodeElem>();
            var lines = codeBlock.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var funcLine = line.Trim().TrimEnd(';');
                int openParenIdx = funcLine.IndexOf('(');
                int closeParenIdx = funcLine.LastIndexOf(')');

                string functionName = funcLine.Substring(0, openParenIdx).Trim();
                string parametersBlock = funcLine.Substring(openParenIdx + 1, closeParenIdx - openParenIdx - 1);

                var parameters = new List<string>();

                if (!string.IsNullOrWhiteSpace(parametersBlock))
                {
                    parameters.AddRange(parametersBlock.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                       .Select(p => p.Trim()));
                }

                functionCalls.Add(new CodeElem
                {
                    functionOrVarName = functionName,
                    expressions = parameters
                });
            }

            return functionCalls;
        }

        // Helper to store buffered text into current element
        private void StoreCurrentElement(ref Dialogue currentDialogue, ref DialogueElement currentElement, ref Speaker currentSpeaker, List<string> textBuffer, List<Attribute> attributeBuffer)
        {
            if (currentDialogue == null || textBuffer.Count == 0)
                return;

            if (currentElement == null)
                currentElement = new DialogueElement { speaker = currentSpeaker };

            currentElement.text = string.Join("\n", textBuffer);

            // Attributes are buffered separately so they can be written before or after the speaker line
            if (attributeBuffer.Count > 0)
            {
                currentElement.attributes.AddRange(attributeBuffer);
                attributeBuffer.Clear();
            }

            currentDialogue.elems.Add(currentElement);

            textBuffer.Clear();
            currentElement = null;
        }

        // Helper method for "@name=value" metadata; a bare "@name" is allowed and means an empty value
        private void ParseAttribute(string line, List<Attribute> attributeBuffer)
        {
            string data = line.Substring(1).Trim();

            int separatorIdx = data.IndexOf('=');
            string name = (separatorIdx < 0) ? (data) : (data.Substring(0, separatorIdx).Trim());
            string value = (separatorIdx < 0) ? ("") : (data.Substring(separatorIdx + 1).Trim());

            if (string.IsNullOrEmpty(name))
            {
                DebugHelpers.LogWarning($"Malformed attribute detected: {line} ({SourceRef()})");
                return;
            }

            attributeBuffer.Add(new Attribute { name = name, value = value });
        }

        // 'include("Name")' (a trailing ';' is tolerated): a reference to another dialogue file, so
        // keys that aren't found in this one are also looked up there. Only the name is stored here;
        // the importer resolves it to the actual asset.
        private static readonly System.Text.RegularExpressions.Regex includeRegex =
            new(@"^include\s*\(\s*""([^""]+)""\s*\)\s*;?\s*$");

        private bool TryParseInclude(string line)
        {
            if (!line.StartsWith("include")) return false;

            var match = includeRegex.Match(line);
            if (!match.Success)
            {
                DebugHelpers.LogWarning($"Malformed include - expected include(\"Name\"): {line} ({SourceRef()})");
                // Still claimed as an include so it doesn't end up as dialogue text
                return true;
            }

            string includeName = match.Groups[1].Value.Trim();
            if (includeNames.Contains(includeName))
            {
                DebugHelpers.LogWarning($"Duplicate include \"{includeName}\" ({SourceRef()})");
                return true;
            }

            includeNames.Add(includeName);
            includeRefs.Add(null);

            return true;
        }

        // Helper method to tell an expression condition apart from a random weight ("25%")
        private void ParseCondition(string conditionText, out string condition, out bool isRandom, out float weight)
        {
            condition = conditionText.Trim();
            isRandom = false;
            weight = 0.0f;

            // "{25%}" and "{25}" are the same thing: the % is a readable convention, not a unit. Weights
            // are relative and normalised across the group, so 25/50/25 and 1/2/1 behave identically.
            // Anything that is entirely a number is a weight - as a condition a bare number would just
            // mean "nonzero", which is a thing nobody writes on purpose.
            string weightText = (condition.EndsWith("%")) ? (condition.Substring(0, condition.Length - 1).Trim()) : (condition);

            if (float.TryParse(weightText,
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out var parsedWeight))
            {
                condition = "";
                isRandom = true;
                weight = parsedWeight;
            }
        }

        // Helper method to parse flags safely. Flags accumulate, so "{ShowInvalid}" and "{Marker=x}"
        // can be written on separate lines. Besides the DialogueFlags names, "Marker=<name>" is
        // accepted and names the node for "-> Marker(<name>)" jumps.
        private void ParseDialogueFlags(string line, Dialogue currentDialogue)
        {
            if (currentDialogue == null)
            {
                DebugHelpers.LogWarning($"Tag line outside of any dialogue: {line} ({SourceRef()})");
                return;
            }

            string data = line.Substring(1, line.Length - 2);  // Remove curly brackets
            var splitData = data.Split(',');

            foreach (var entry in splitData)
            {
                string trimmedEntry = entry.Trim();

                int equalsIdx = trimmedEntry.IndexOf('=');
                if (equalsIdx >= 0)
                {
                    string tagName = trimmedEntry.Substring(0, equalsIdx).Trim();
                    string tagValue = trimmedEntry.Substring(equalsIdx + 1).Trim();

                    if (string.Equals(tagName, "Marker", StringComparison.OrdinalIgnoreCase) && (!string.IsNullOrEmpty(tagValue)))
                        currentDialogue.marker = tagValue;
                    else
                        DebugHelpers.LogWarning($"Unknown dialogue tag: {trimmedEntry} ({SourceRef()})");
                }
                // Enum.TryParse happily parses a bare number as a flags value, which nobody means
                else if ((!float.TryParse(trimmedEntry, out _)) && Enum.TryParse(trimmedEntry, out DialogueFlags parsedFlag))
                {
                    currentDialogue.flags |= parsedFlag;
                }
                else
                {
                    DebugHelpers.LogWarning($"Unknown DialogueFlag: {trimmedEntry} ({SourceRef()})");
                }
            }
        }

        // Helper method for option parsing with validation
        private void ParseOption(string line, DialogueElement currentElement, string condition, float chance, OneShotScope oneShot)
        {
            int arrowIdx = line.IndexOf("->");
            if (arrowIdx < 0)
            {
                DebugHelpers.LogWarning($"Option \"{line}\" has no destination - every option needs \"-><key>\" (use \"-> End\" to end the conversation)! ({SourceRef()})");
                return;
            }

            AddOption(currentElement, line.Substring(1, arrowIdx - 1).Trim(), line.Substring(arrowIdx).Trim(), null, condition, chance, oneShot);
        }

        // destination is the "-><key>" part, still with its arrow, because that's what both option forms
        // have in hand at this point
        private void AddOption(DialogueElement currentElement, string optionText, string destination, List<CodeElem> code, string condition, float chance, OneShotScope oneShot)
        {
            if (!destination.StartsWith("->"))
            {
                DebugHelpers.LogWarning($"Option \"{optionText}\" doesn't say where it goes (expected \"-><key>\", got \"{destination}\") - use \"-> End\" to end the conversation! ({SourceRef()})");
                return;
            }

            string destinationKey = destination.Substring(2).Trim();

            if (string.IsNullOrEmpty(optionText) || string.IsNullOrEmpty(destinationKey))
            {
                DebugHelpers.LogWarning($"Incomplete option detected: {optionText} -> {destinationKey} ({SourceRef()})");
                return;
            }

            if (currentElement != null)
                currentElement.options.Add(new Option { text = optionText, key = destinationKey, code = code, condition = condition, chance = chance, oneShot = oneShot });
            else
                DebugHelpers.LogWarning($"Option defined without an element context: {optionText} ({SourceRef()})");
        }

        private void ParseConditionalNext(string line, Dialogue currentDialogue)
        {
            int closeBraceIdx = line.IndexOf("}");
            ParseCondition(line.Substring(1, closeBraceIdx - 1), out var condition, out var isRandom, out var weight);

            int arrowIdx = line.IndexOf("=>", closeBraceIdx);
            string nextKey = line.Substring(arrowIdx + 2).Trim();

            currentDialogue.conditionalNext.Add(new DialogueCondition
            {
                condition = condition,
                isRandom = isRandom,
                weight = weight,
                nextKey = new NextKeyOrCode { nextKey = nextKey }
            });
        }

        private Speaker GetSpeakerByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            if (speakerCache.TryGetValue(name, out Speaker cachedSpeaker))
            {
                return cachedSpeaker;
            }

            var allSpeakers = AssetUtils.GetAll<Speaker>();
            Speaker speaker = Array.Find(allSpeakers, s => (s.displayName == name) || ((s.nameAlias != null) && (s.nameAlias.Contains(name))));

            if (speaker != null)
            {
                speakerCache[name] = speaker;
                return speaker;
            }

            DebugHelpers.LogWarning($"Speaker '{name}' not found! ({SourceRef()})");
            return null;
        }

        public bool HasDialogue(string dialogueKey)
        {
            return FindDialogue(dialogueKey) != null;
        }

        public Dialogue GetFirstDialogue()
        {
            if ((dialogues == null) || (dialogues.Count == 0)) return null;

            return dialogues[0];
        }

        public Dialogue GetDialogue(string dialogueKey)
        {
            var dialogue = FindDialogue(dialogueKey);

            if (dialogue == null)
            {
                DebugHelpers.LogWarning($"Dialogue '{dialogueKey}' not found!");
            }

            return dialogue;
        }

        // Like GetDialogue, but not finding the key is a normal outcome and stays quiet - search
        // chains (current file -> includes -> globals) probe with this so a miss in one place isn't
        // reported before the others have been tried
        public Dialogue FindDialogue(string dialogueKey)
        {
            if (dialogueCache.TryGetValue(dialogueKey, out var dialogue))
            {
                return dialogue;
            }

            dialogue = dialogues.Find(s => s.name == dialogueKey);

            if (dialogue != null)
            {
                dialogueCache[dialogueKey] = dialogue;
            }

            return dialogue;
        }

        // Searches this file and then everything it includes, transitively - an included file's own
        // includes count too. Returns the file the key was found in along with the dialogue itself,
        // so a conversation that follows the key can move its notion of "current file" there and
        // resolve that file's local keys from then on.
        public (DialogueData data, Dialogue dialogue) FindDialogueInHierarchy(string dialogueKey)
        {
            return FindDialogueInHierarchy(dialogueKey, new HashSet<DialogueData>());
        }

        private (DialogueData data, Dialogue dialogue) FindDialogueInHierarchy(string dialogueKey, HashSet<DialogueData> visited)
        {
            // Includes can be circular (the girl file including the event that includes it back) -
            // that's fine for lookups, each file is just searched once
            if (!visited.Add(this)) return (null, null);

            var dialogue = FindDialogue(dialogueKey);
            if (dialogue != null) return (this, dialogue);

            for (int i = 0; i < includeRefs.Count; i++)
            {
                var include = GetResolvedInclude(i);
                if (include == null) continue;

                var result = include.FindDialogueInHierarchy(dialogueKey, visited);
                if (result.dialogue != null) return result;
            }

            return (null, null);
        }

        // Follows an include: normally the hard reference baked in by the importer. When that is
        // missing (the other file wasn't imported yet when this one was), the editor falls back to
        // searching the asset database so iteration keeps working; a build can't search, so there the
        // include is simply reported - "Unity Common/Dialogue/Update References" exists to make sure
        // a build never gets to that point.
        public DialogueData GetResolvedInclude(int index)
        {
            if ((index < 0) || (index >= includeRefs.Count)) return null;
            if (includeRefs[index] != null) return includeRefs[index];

#if UNITY_EDITOR
            foreach (var candidate in AssetUtils.GetAll<DialogueData>())
            {
                if ((candidate != null) && (candidate != this) && (candidate.name == includeNames[index]))
                {
                    includeRefs[index] = candidate;
                    return candidate;
                }
            }
#endif

            if (!warnedUnresolvedIncludes)
            {
                warnedUnresolvedIncludes = true;
                DebugHelpers.LogError($"Dialogue file \"{name}\" includes \"{includeNames[index]}\", which isn't resolved - run Unity Common/Dialogue/Update References!");
            }

            return null;
        }

        private bool warnedUnresolvedIncludes = false;

        public List<string> GetKeys()
        {
            if ((keys == null) || (keys.Count == 0) || (keys.Count != dialogues.Count))
            {
                RefreshKeys();
            }

            return keys;
        }

        void RefreshKeys()
        {
            keys = new();
            foreach (var dialogue in dialogues)
            {
                keys.Add(dialogue.name);
            }

        }
    }

    public class DialogueDataProbList : ProbList<DialogueData>
    {

    }
}
