using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

[CreateAssetMenu(fileName = "Dialogue Data", menuName = "Unity Common/Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    public Speaker[]    speakers;
    public TextAsset[]  textAssets;

    [Serializable]
    class Option
    {
        public string text;
        public string key;
    }

    [Serializable]
    class DialogueElement
    {
        public Speaker speaker;
        public string text;
        public List<Option> options = new List<Option>();
        public string nextKey;
    }

    [Serializable]
    class Dialogue
    {
        public string key;
        public List<DialogueElement> elems = new List<DialogueElement>();
    }

    [SerializeField] private List<Dialogue> dialogues = new();

    private Dictionary<string, Speaker> speakerCache = new();

    [Button("Parse Data")]
    public void ParseData()
    {
        dialogues.Clear();

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
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Pause detected: store the collected text as an element
                if (currentElement != null && textBuffer.Count > 0)
                {
                    currentElement.text = string.Join("\n", textBuffer);
                    currentDialogue.elems.Add(currentElement);
                    currentElement = new DialogueElement { speaker = currentSpeaker };
                    textBuffer.Clear();
                }
                continue;
            }

            if (trimmedLine.StartsWith("# "))
            {
                // New Dialogue Section
                string key = trimmedLine.Substring(2).Trim();
                currentDialogue = new Dialogue { key = key };
                dialogues.Add(currentDialogue);
                currentElement = new DialogueElement();
                textBuffer.Clear();
            }
            else if (trimmedLine.StartsWith("[") && trimmedLine.Contains("]:"))
            {
                // Speaker Change
                int endIdx = trimmedLine.IndexOf("]:");
                string speakerName = trimmedLine.Substring(1, endIdx - 1);
                currentSpeaker = GetSpeakerByName(speakerName);

                string dialogueText = trimmedLine.Substring(endIdx + 2).Trim();
                if (!string.IsNullOrEmpty(dialogueText))
                {
                    if (currentElement != null && textBuffer.Count > 0)
                    {
                        currentElement.text = string.Join("\n", textBuffer);
                        currentDialogue.elems.Add(currentElement);
                        textBuffer.Clear();
                    }
                    currentElement = new DialogueElement { speaker = currentSpeaker };
                    textBuffer.Add(dialogueText);
                }
            }
            else if (trimmedLine.StartsWith("*"))
            {
                // Option Handling
                int arrowIdx = trimmedLine.IndexOf("->");
                if (arrowIdx > 0)
                {
                    string optionText = trimmedLine.Substring(1, arrowIdx - 1).Trim();
                    string destinationKey = trimmedLine.Substring(arrowIdx + 2).Trim();

                    if (currentElement != null)
                    {
                        currentElement.options.Add(new Option { text = optionText, key = destinationKey });
                    }
                }
            }
            else if (trimmedLine.StartsWith("=>"))
            {
                // Next Dialogue Jump
                string nextKey = trimmedLine.Substring(2).Trim();
                if (currentElement != null)
                {
                    currentElement.nextKey = nextKey;
                }
            }
            else
            {
                // Regular Dialogue Text
                textBuffer.Add(trimmedLine);
            }
        }

        // Final element storage
        if (currentElement != null && textBuffer.Count > 0)
        {
            currentElement.text = string.Join("\n", textBuffer);
            currentDialogue.elems.Add(currentElement);
        }
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
}
