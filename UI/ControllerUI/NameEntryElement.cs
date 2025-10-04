using System;
using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class NameEntryElement : MonoBehaviour
{
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
        letterText.transform.ScaleTo(Vector3.one, 0.05f, "ZoomLetter");
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

    public string letter => allowed[index];
}
