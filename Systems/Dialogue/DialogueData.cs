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
            OneShot = 1,
            Random = 2
        };

        [Serializable]
        public class Option
        {
            public string text;
            public string key;
            // Code written as part of the option itself ("*<text>=>{ ... }-><key>"). It runs when this
            // option is picked, before going to key - which is the only way to attach a side effect to
            // one specific choice instead of to the whole beat.
            public List<CodeElem> code;

            public bool hasCode => (code != null) && (code.Count > 0);
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

        [Serializable]
        public class Dialogue
        {
            public string name;
            public DialogueFlags flags;
            public List<DialogueElement> elems = new();

            // new support for conditional next keys
            public List<DialogueCondition> conditionalNext = new();

            // Code written as "{ ... }" with no arrow. It runs when the node is entered, before any of
            // its text is shown - as opposed to "=>{ ... }", which runs on the way out.
            public List<CodeElem> entryCode = new();

            public bool isRedirect => (elems == null) || (elems.Count == 0);

            // Resolves where this dialogue goes next without causing any side effect: code blocks are
            // skipped instead of run, and random groups resolve to their first entry instead of rolling.
            // This is what the "is there anything to say?" queries need.
            public string GetNextDialogue(Expression.IContext context) => EvaluateNext(context, null);

            // Walks the conditionalNext list in order and returns the key it ends up redirecting to:
            //   - a random group is a run of consecutive "{25%}=>" entries; one of them is picked by
            //     weight (or the first one, when peeking) and the rest of the run is skipped
            //   - a code block doesn't redirect anywhere, so it's executed and the walk *continues*,
            //     which is what makes "=>{ ... }" followed by "=>SomeKey" work
            //   - the first entry that resolves to a key wins
            // Passing a null codeExecutor makes this a peek (see GetNextDialogue).
            public string EvaluateNext(Expression.IContext context, Action<NextKeyOrCode> codeExecutor)
            {
                if (conditionalNext == null) return null;

                bool peek = (codeExecutor == null);
                int index = 0;

                while (index < conditionalNext.Count)
                {
                    DialogueCondition condition;

                    if (conditionalNext[index].isRandom)
                    {
                        int groupEnd = index;
                        while ((groupEnd < conditionalNext.Count) && (conditionalNext[groupEnd].isRandom)) groupEnd++;

                        condition = SelectRandom(index, groupEnd, peek);
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
                                Debug.LogWarning($"Can't evaluate \"{condition.condition}\" in dialogue \"{name}\" - no context!");
                                continue;
                            }

                            if (Expression.TryParse(condition.condition, out var expression))
                            {
                                if (!expression.EvaluateBool(context)) continue;
                            }
                            else
                            {
                                Debug.LogWarning($"Can't parse expression \"{condition.condition}\"!");
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

            private DialogueCondition SelectRandom(int startIndex, int endIndex, bool peek)
            {
                if (peek)
                {
                    // Deterministic stand-in for the roll: the first entry that could actually be picked
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
                    Debug.LogWarning($"Random group in dialogue \"{name}\" has no positive weight!");
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
                    if (roll <= 0.0f) return conditionalNext[i];
                }

                return lastValid;
            }
        }

        [SerializeField] private List<Dialogue> dialogues = new();

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
                Debug.LogError($"Failed to load {SourceRef(filename, 1)}: {e.Message}");
                return null;
            }

            return newObject;
        }

        void _Import(string filename)
        {
            dialogues.Clear();
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
            // Set while the code block being parsed is a "{ ... }" entry block instead of a "=>{ ... }"
            bool isEntryCode = false;
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
                            AddOption(currentElement, currentOptionText, trimmedLine.Substring(1).Trim(), code);
                            currentOptionText = null;
                        }
                        else if (isEntryCode)
                        {
                            currentDialogue.entryCode.AddRange(code);
                            isEntryCode = false;
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
                else if (trimmedLine.StartsWith("{"))
                {
                    if (trimmedLine.Contains("}=>{"))
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
                else if (trimmedLine.StartsWith("*"))
                {
                    int codeIdx = trimmedLine.IndexOf("=>{");
                    if (codeIdx >= 0)
                    {
                        // "*<text>=>{" opens a code block that belongs to this option; the destination
                        // comes on the line that closes it
                        currentOptionText = trimmedLine.Substring(1, codeIdx - 1).Trim();
                        isParsingCodeBlock = true;
                    }
                    else
                    {
                        ParseOption(trimmedLine, currentElement);
                    }
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
            // With no arrow the block opens at the "{" itself, which is the entry-code form
            if ((openIdx < 0) && (!IsEntryCodeBlock(line))) return null;

            int braceIdx = (openIdx >= 0) ? (openIdx + 2) : 0;
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
        private bool IsEntryCodeBlock(string line)
        {
            if (line.Contains("=>")) return false;
            if (!line.Contains("}")) return true;

            return line.Contains(";");
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
                    Debug.LogError($"Syntax Error: Missing ';' at end of statement '{statementLine}' ({SourceRef()})");
                    continue; // or optionally throw an exception here if strict behavior desired
                }

                // Remove the trailing semicolon for parsing
                statementLine = statementLine.Substring(0, statementLine.Length - 1).Trim();

                if (statementLine.Contains("="))
                {
                    var splitAssignment = statementLine.Split(new[] { '=' }, 2);

                    if (splitAssignment.Length != 2)
                    {
                        Debug.LogError($"Invalid assignment syntax: {statementLine} ({SourceRef()})");
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
                        Debug.LogError($"Malformed function call detected: {statementLine} ({SourceRef()})");
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
                Debug.LogWarning($"Malformed attribute detected: {line} ({SourceRef()})");
                return;
            }

            attributeBuffer.Add(new Attribute { name = name, value = value });
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

        // Helper method to parse flags safely
        private void ParseDialogueFlags(string line, Dialogue currentDialogue)
        {
            string data = line.Substring(1, line.Length - 2);  // Remove curly brackets
            var splitData = data.Split(',');

            DialogueFlags flags = DialogueFlags.None;

            foreach (var entry in splitData)
            {
                string trimmedEntry = entry.Trim();

                if (Enum.TryParse(trimmedEntry, out DialogueFlags parsedFlag))
                    flags |= parsedFlag;
                else
                    Debug.LogWarning($"Unknown DialogueFlag: {trimmedEntry} ({SourceRef()})");
            }

            currentDialogue.flags = flags;
        }

        // Helper method for option parsing with validation
        private void ParseOption(string line, DialogueElement currentElement)
        {
            int arrowIdx = line.IndexOf("->");
            if (arrowIdx < 0)
            {
                Debug.LogWarning($"Malformed option detected: {line} ({SourceRef()})");
                return;
            }

            AddOption(currentElement, line.Substring(1, arrowIdx - 1).Trim(), line.Substring(arrowIdx).Trim(), null);
        }

        // destination is the "-><key>" part, still with its arrow, because that's what both option forms
        // have in hand at this point
        private void AddOption(DialogueElement currentElement, string optionText, string destination, List<CodeElem> code)
        {
            if (!destination.StartsWith("->"))
            {
                Debug.LogWarning($"Option \"{optionText}\" doesn't say where it goes (expected \"-><key>\", got \"{destination}\")! ({SourceRef()})");
                return;
            }

            string destinationKey = destination.Substring(2).Trim();

            if (string.IsNullOrEmpty(optionText) || string.IsNullOrEmpty(destinationKey))
            {
                Debug.LogWarning($"Incomplete option detected: {optionText} -> {destinationKey} ({SourceRef()})");
                return;
            }

            if (currentElement != null)
                currentElement.options.Add(new Option { text = optionText, key = destinationKey, code = code });
            else
                Debug.LogWarning($"Option defined without an element context: {optionText} ({SourceRef()})");
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

            Debug.LogWarning($"Speaker '{name}' not found! ({SourceRef()})");
            return null;
        }

        public bool HasDialogue(string dialogueKey)
        {
            return GetDialogue(dialogueKey) != null;
        }

        public Dialogue GetFirstDialogue()
        {
            if ((dialogues == null) || (dialogues.Count == 0)) return null;

            return dialogues[0];
        }

        public Dialogue GetDialogue(string dialogueKey)
        {
            if (dialogueCache.TryGetValue(dialogueKey, out var dialogue))
            {
                return dialogue;
            }

            // Placeholder function for finding a speaker (replace with actual implementation)
            dialogue = dialogues.Find(s => s.name == dialogueKey);

            if (dialogue != null)
            {
                dialogueCache[dialogueKey] = dialogue;
                return dialogue;
            }

            Debug.LogWarning($"Dialogue '{dialogueKey}' not found!");

            return null;
        }

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
