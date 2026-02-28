using NaughtyAttributes.Editor;
using System;
using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.UI;

public class NameEntryElement : MonoBehaviour
{
    public delegate void OnFinish();
    public event OnFinish onFinish;

    public delegate void OnBackspace();
    public event OnBackspace onBackspace;

    public delegate void OnSelect();
    public event OnSelect onSelect;

    [Flags]
    public enum NameEntryType
    {
        UppercaseAlpha = 1,
        LowercaseAlpha = 2,
        Numeric = 4,
        Symbol = 8,
        Space = 16,
        Backspace = 32,
    }
    [SerializeField] private Image              upArrow;
    [SerializeField] private TextMeshProUGUI    letterText;
    [SerializeField] private Image              downArrow;
    [SerializeField] private NameEntryType      allowedTypes = NameEntryType.UppercaseAlpha | NameEntryType.LowercaseAlpha | NameEntryType.Numeric | NameEntryType.Symbol | NameEntryType.Space;
    [SerializeField] private string             currentLetter = "A";

    private int     index = 0;
    private Color   defaultUpColor;
    private Color   defaultDownColor;

    List<string> allowed;
    
    public bool allowLowercase => allowedTypes.HasFlag(NameEntryType.LowercaseAlpha);
    public bool allowUppercase => allowedTypes.HasFlag(NameEntryType.UppercaseAlpha);
    public bool allowBackspace => allowedTypes.HasFlag(NameEntryType.Backspace);

    void Awake()
    {
        allowed = new();
        if (allowedTypes.HasFlag(NameEntryType.Space)) allowed.Add(" ");
        if (allowedTypes.HasFlag(NameEntryType.UppercaseAlpha))
        {
            for (char c = 'A'; c <= 'Z'; c++)
            {
                allowed.Add(c.ToString());
            }
        }
        if (allowedTypes.HasFlag(NameEntryType.LowercaseAlpha))
        {
            for (char c = 'a'; c <= 'z'; c++)
            {
                allowed.Add(c.ToString());
            }
        }
        if (allowedTypes.HasFlag(NameEntryType.Numeric))
        {
            for (char c = '0'; c <= '9'; c++)
            {
                allowed.Add(c.ToString());
            }
        }
        if (allowedTypes.HasFlag(NameEntryType.Symbol))
        {
            char[] symbols = new char[] { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', ';', ':', '\'', '"', ',', '<', '.', '>', '/', '?', '\\', '|', '`', '~' };
            foreach (char c in symbols)
            {
                allowed.Add(c.ToString());
            }
        }
        if (allowedTypes.HasFlag(NameEntryType.Backspace)) allowed.Add("\u2190"); // Left arrow

        allowed.Add("OK");
        index = allowed.IndexOf(currentLetter);
        if (index == -1) index = 0;
        letterText.text = allowed[index];

        defaultUpColor = upArrow.color;
        defaultDownColor = downArrow.color;

        Deactivate();
    }

    public void LetterUp()
    {
        index--;
        if (index < 0) index = allowed.Count - 1;
        letterText.text = allowed[index];
        
        FlashLetter();
        upArrow.color = Color.red;
        upArrow.FadeTo(defaultUpColor, 0.05f);
    }

    public void LetterDown()
    {
        index = (index + 1) % allowed.Count;
        letterText.text = allowed[index];

        FlashLetter();
        downArrow.color = Color.red;
        downArrow.FadeTo(defaultDownColor, 0.05f);
    }

    void FlashLetter()
    {
        letterText.transform.localScale = Vector3.one * 1.2f;
        letterText.transform.LocalScaleTo(Vector3.one, 0.05f, "ZoomLetter");
    }

    public void Activate()
    {
        upArrow.enabled = true;
        downArrow.enabled = true;
    }

    public void Deactivate()
    {
        upArrow.enabled = false;
        downArrow.enabled = false;
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (upArrow.enabled && Input.anyKeyDown)
        {
            foreach (var entry in allowed)
            {
                if (string.IsNullOrEmpty(entry))
                    continue;

                KeyCode key;
                if (entry.Length == 1)
                {
                    char c = entry[0];

                    // Try letters
                    if (char.IsLetter(c))
                    {
                        if (Enum.TryParse(c.ToString().ToUpper(), out key) && Input.GetKeyDown(key))
                        {
                            index = allowed.IndexOf(entry);
                            letterText.text = allowed[index];
                            FlashLetter();
                            onSelect();
                            break;
                        }
                    }

                    // Try numbers
                    else if (char.IsDigit(c))
                    {
                        string name = "Alpha" + c;
                        if (Enum.TryParse(name, out key) && Input.GetKeyDown(key))
                        {
                            index = allowed.IndexOf(entry);
                            letterText.text = allowed[index];
                            FlashLetter();
                            onSelect();
                            break;
                        }
                    }

                    // Handle space
                    else if (c == ' ' && Input.GetKeyDown(KeyCode.Space))
                    {
                        index = allowed.IndexOf(entry);
                        letterText.text = allowed[index];
                        FlashLetter();
                        onSelect();
                        break;
                    }
                }

                // Handle OK or special cases
                if (entry == "OK" && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                {
                    index = allowed.IndexOf(entry);
                    letterText.text = allowed[index];
                    FlashLetter();
                    onFinish();
                    break;
                }

                if (entry == "\u2190" && Input.GetKeyDown(KeyCode.Backspace))
                {
                    index = allowed.IndexOf(entry);
                    letterText.text = allowed[index];
                    FlashLetter();
                    onBackspace();
                    break;
                }
            }
        }
#endif
    }

    public bool SetLetter(char c)
    {
        int idx = allowed.IndexOf($"{c}");
        if (idx != -1)
        {
            index = idx;
            letterText.text = allowed[idx];
            FlashLetter();
            onSelect();
            return true;
        }

        return false;
    }

    public string letter => allowed[index];
}
