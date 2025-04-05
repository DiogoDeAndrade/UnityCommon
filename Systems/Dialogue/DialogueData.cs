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
        }

        [Serializable]
        public class DialogueElement
        {
            public Speaker speaker;
            public string text;
            public List<Option> options = new List<Option>();

            public bool hasOptions => (options != null) && (options.Count > 0);
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
        }

        [Serializable]
        public class Dialogue
        {
            public string name;
            public DialogueFlags flags;
            public List<DialogueElement> elems = new();

            // new support for conditional next keys
            public List<DialogueCondition> conditionalNext = new();

            public bool isRedirect => (elems == null) || (elems.Count == 0);
            public string GetNextDialogue(Expression.IContext context)
            {
                foreach (var condition in conditionalNext)
                {
                    if (string.IsNullOrEmpty(condition.condition))
                    {
                        return condition.nextKey.nextKey;
                    }
                    if (Expression.TryParse(condition.condition, out var expression))
                    {
                        if (expression.EvaluateBool(context))
                        {
                            return condition.nextKey.nextKey;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Can't parse expression \"{condition.condition}\"!");
                    }
                }

                return null;
            }
        }

        [SerializeField] private List<Dialogue> dialogues = new();

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
            catch
            {
                Debug.LogError($"Failed to load {filename}!");
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

            var content = System.IO.File.ReadAllText(filename);

            ParseTextAsset(content);

            RefreshKeys();
        }

        private void ParseTextAsset(string text)
        {
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Dialogue currentDialogue = null;
            DialogueElement currentElement = null;
            Speaker currentSpeaker = null;
            List<string> textBuffer = new();

            bool isParsingCodeBlock = false;
            string currentCondition = "";
            List<string> codeBlockBuffer = new();
            bool isInBlockComment = false;

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();

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
                    if (trimmedLine == "}")
                    {
                        var codeBlock = string.Join("\n", codeBlockBuffer);
                        currentDialogue.conditionalNext.Add(new DialogueCondition
                        {
                            condition = currentCondition,
                            nextKey = new NextKeyOrCode { code = ParseCodeStatements(codeBlock) }
                        });

                        isParsingCodeBlock = false;
                        currentCondition = "";
                        codeBlockBuffer.Clear();
                        continue;
                    }

                    codeBlockBuffer.Add(trimmedLine);
                    continue;
                }

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);
                    continue;
                }

                if (trimmedLine.StartsWith("#"))
                {
                    StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);
                    string key = trimmedLine.Substring(1).Trim();
                    currentDialogue = new Dialogue { name = key };
                    dialogues.Add(currentDialogue);
                    currentSpeaker = null;
                }
                else if (trimmedLine.StartsWith("[") && trimmedLine.Contains("]:"))
                {
                    StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);
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
                        currentCondition = trimmedLine.Substring(1, conditionEnd - 1).Trim();
                        isParsingCodeBlock = true;
                    }
                    else if (trimmedLine.Contains("}=>"))
                    {
                        ParseConditionalNext(trimmedLine, currentDialogue);
                    }
                    else
                    {
                        ParseDialogueFlags(trimmedLine, currentDialogue);
                    }
                }
                else if (trimmedLine.StartsWith("=>{"))
                {
                    currentCondition = "";
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
                    ParseOption(trimmedLine, currentElement);
                }
                else
                {
                    textBuffer.Add(trimmedLine);
                }
            }
            StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);
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

        private List<CodeElem> ParseCodeStatements(string codeBlock)
        {
            var statements = new List<CodeElem>();

            string[] lines = codeBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                string statementLine = lines[i].Trim();

                if (string.IsNullOrWhiteSpace(statementLine))
                    continue;

                // Verify each statement ends with a semicolon
                if (!statementLine.EndsWith(";"))
                {
                    Debug.LogError($"Syntax Error: Missing ';' at end of statement '{statementLine}' (line {i + 1})");
                    continue; // or optionally throw an exception here if strict behavior desired
                }

                // Remove the trailing semicolon for parsing
                statementLine = statementLine.Substring(0, statementLine.Length - 1).Trim();

                if (statementLine.Contains("="))
                {
                    var splitAssignment = statementLine.Split(new[] { '=' }, 2);

                    if (splitAssignment.Length != 2)
                    {
                        Debug.LogError($"Invalid assignment syntax: {statementLine}");
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
                        Debug.LogError($"Malformed function call detected: {statementLine}");
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
        private void StoreCurrentElement(ref Dialogue currentDialogue, ref DialogueElement currentElement, ref Speaker currentSpeaker, List<string> textBuffer)
        {
            if (currentDialogue == null || textBuffer.Count == 0)
                return;

            if (currentElement == null)
                currentElement = new DialogueElement { speaker = currentSpeaker };

            currentElement.text = string.Join("\n", textBuffer);
            currentDialogue.elems.Add(currentElement);

            textBuffer.Clear();
            currentElement = null;
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
                    Debug.LogWarning($"Unknown DialogueFlag: {trimmedEntry}");
            }

            currentDialogue.flags = flags;
        }

        // Helper method for option parsing with validation
        private void ParseOption(string line, DialogueElement currentElement)
        {
            int arrowIdx = line.IndexOf("->");
            if (arrowIdx < 0)
            {
                Debug.LogWarning($"Malformed option detected: {line}");
                return;
            }

            string optionText = line.Substring(1, arrowIdx - 1).Trim();
            string destinationKey = line.Substring(arrowIdx + 2).Trim();

            if (string.IsNullOrEmpty(optionText) || string.IsNullOrEmpty(destinationKey))
            {
                Debug.LogWarning($"Incomplete option detected: {line}");
                return;
            }

            if (currentElement != null)
                currentElement.options.Add(new Option { text = optionText, key = destinationKey });
            else
                Debug.LogWarning($"Option defined without an element context: {line}");
        }

        private void ParseConditionalNext(string line, Dialogue currentDialogue)
        {
            int closeBraceIdx = line.IndexOf("}");
            string condition = line.Substring(1, closeBraceIdx - 1).Trim();

            int arrowIdx = line.IndexOf("=>");
            string nextKey = line.Substring(arrowIdx + 2).Trim();

            currentDialogue.conditionalNext.Add(new DialogueCondition
            {
                condition = condition,
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

            Debug.LogWarning($"Speaker '{name}' not found!");
            return null;
        }

        public bool HasDialogue(string dialogueKey)
        {
            return GetDialogue(dialogueKey) != null;
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
}