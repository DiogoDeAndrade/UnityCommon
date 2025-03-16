using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

[CreateAssetMenu(fileName = "Dialogue Data", menuName = "Unity Common/Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Flags]
    public enum DialogueFlags { None = 0,
                                OneShot = 1 };

    public Speaker[]    speakers;
    public TextAsset[]  textAssets;

    [Serializable]
    public class Option
    {
        public string text;
        public string key;
    }

    [Serializable]
    public class DialogueElement
    {
        public Speaker      speaker;
        public string       text;
        public List<Option> options = new List<Option>();
        
        public bool hasOptions => (options != null) && (options.Count > 0);
    }

    [Serializable]
    public class DialogueCondition
    {
        public string expression; // condition as a string, to be parsed later
        public string nextKey;
    }

    [Serializable]
    public class Dialogue
    {
        public DialogueFlags flags;
        public string key;
        public List<DialogueElement> elems = new();

        // new support for conditional next keys
        public List<DialogueCondition> conditionalNext = new();
        public string nextKey; // default next key if none conditions met
    }

    [SerializeField] private List<Dialogue> dialogues = new();

    private Dictionary<string, Speaker>     speakerCache = new();
    private Dictionary<string, Dialogue>    dialogueCache = new();
    private List<string>                    keys = null;

    [Button("Parse Data")]
    public void ParseData()
    {
        dialogues.Clear();
        speakerCache = new();
        dialogueCache = new();
        keys = null;

        foreach (var textAsset in textAssets)
        {
            ParseTextAsset(textAsset.text);
        }
    }

    private void ParseTextAsset(string text)
    {
        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        Dialogue currentDialogue = null;
        DialogueElement currentElement = null;
        Speaker currentSpeaker = null;
        List<string> textBuffer = new();

        foreach (var line in lines)
        {
            string trimmedLine = line.Trim();

            if (string.IsNullOrEmpty(trimmedLine))
            {
                // Blank line indicates end of current dialogue element text
                StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);
                continue;
            }

            if (trimmedLine.StartsWith("#"))
            {
                // New Dialogue Section
                StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);
                string key = trimmedLine.Substring(1).Trim();
                currentDialogue = new Dialogue { key = key };
                dialogues.Add(currentDialogue);
            }
            else if (trimmedLine.StartsWith("[") && trimmedLine.Contains("]:"))
            {
                // Speaker Change and new element
                StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);

                int endIdx = trimmedLine.IndexOf("]:");
                string speakerName = trimmedLine.Substring(1, endIdx - 1);
                currentSpeaker = GetSpeakerByName(speakerName);

                string dialogueText = trimmedLine.Substring(endIdx + 2).Trim();
                currentElement = new DialogueElement { speaker = currentSpeaker };

                if (!string.IsNullOrEmpty(dialogueText))
                    textBuffer.Add(dialogueText);
            }
            else if (trimmedLine.StartsWith("{") && trimmedLine.EndsWith("}"))
            {
                // Flags parsing
                if (currentDialogue == null)
                {
                    Debug.LogWarning($"Flags defined without dialogue at line: {trimmedLine}");
                    continue;
                }

                ParseDialogueFlags(trimmedLine, currentDialogue);
            }
            else if (trimmedLine.StartsWith("*"))
            {
                // Option parsing
                ParseOption(trimmedLine, currentElement);
            }
            else if (trimmedLine.StartsWith("{") && trimmedLine.Contains("}=>"))
            {
                // Conditional Next Dialogue Handling
                if (currentDialogue == null)
                {
                    Debug.LogWarning($"Conditional next-key defined without dialogue at line: {trimmedLine}");
                    continue;
                }
                ParseConditionalNext(trimmedLine, currentDialogue);
            }
            else if (trimmedLine.StartsWith("=>"))
            {
                // Default Next Dialogue Handling
                if (currentDialogue != null)
                    currentDialogue.nextKey = trimmedLine.Substring(2).Trim();
                else
                    Debug.LogWarning($"NextKey defined without dialogue at line: {trimmedLine}");
            }
            else
            {
                // Normal text line
                textBuffer.Add(trimmedLine);
            }
        }

        // Store last element
        StoreCurrentElement(ref currentDialogue, ref currentElement, ref currentSpeaker, textBuffer);
    }

    // Helper to store buffered text into current element
    private void StoreCurrentElement(ref Dialogue currentDialogue, ref DialogueElement currentElement, ref Speaker currentSpeaker, List<string> textBuffer)
    {
        if (currentDialogue == null || currentSpeaker == null || textBuffer.Count == 0)
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

        int arrowIdx = line.IndexOf("=>", closeBraceIdx);
        if (arrowIdx < 0)
        {
            Debug.LogWarning($"Malformed conditional next detected: {line}");
            return;
        }

        string nextKey = line.Substring(arrowIdx + 2).Trim();

        if (string.IsNullOrEmpty(nextKey))
        {
            Debug.LogWarning($"Incomplete conditional next detected: {line}");
            return;
        }

        currentDialogue.conditionalNext.Add(new DialogueCondition
        {
            expression = condition,
            nextKey = nextKey
        });
    }

    private Speaker GetSpeakerByName(string name)
    {
        if (speakerCache.TryGetValue(name, out Speaker cachedSpeaker))
        {
            return cachedSpeaker;
        }

        // Placeholder function for finding a speaker (replace with actual implementation)
        Speaker speaker = Array.Find(speakers, s => s.displayName == name);

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
        dialogue = dialogues.Find(s => s.key == dialogueKey);

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
        if ((keys == null) || (keys.Count == 0))
        {
            keys = new();
            foreach (var dialogue in dialogues)
            {
                keys.Add(dialogue.key);
            }
        }

        return keys;
    }    
}
  