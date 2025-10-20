using NaughtyAttributes;
using System;
using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.UI;

public class SubtitleDisplayManager : MonoBehaviour
{
    [SerializeField] 
    private TextMeshProUGUI    speakerNameText;
    [SerializeField] 
    private TextMeshProUGUI[]  subtitleLines;
    [SerializeField] 
    private bool               overrideFonts;
    [SerializeField, ShowIf(nameof(overrideFonts))] 
    private TMP_FontAsset      font;
    [SerializeField, ShowIf(nameof(overrideFonts))]
    private Material           material;
    [SerializeField, ShowIf(nameof(overrideFonts))]
    private float              textScale;
    [SerializeField] 
    private bool               enableSpeakerFontColor;


    private static SubtitleDisplayManager _instance;

    public static SubtitleDisplayManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SubtitleDisplayManager>();
            }
            return _instance;
        }
    }

    struct DefaultTextData
    {
        public float size;
        public Color color;
    }

    AudioSource             currentAudio;
    SoundDef                currentTrack;
    float                   defaultSpeakerFontSize;
    List<DefaultTextData>   defaultTextData;
    RectTransform           rectTransform;

    void Start()
    {
        if ((_instance == null) || (_instance == this))
        {
            _instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        defaultTextData = new();
        foreach (var l in subtitleLines)
        {
            defaultTextData.Add(new DefaultTextData
            {
                size = l.fontSize,
                color = l.color
            });
        }
        defaultSpeakerFontSize = (speakerNameText) ? speakerNameText.fontSize : 0.0f;

        rectTransform = GetComponent<RectTransform>();
    }    

    void Update()
    {
        if ((currentTrack != null) && (currentAudio != null) && (currentAudio.isPlaying))
        {
            var line = currentTrack.subtitleTrack.GetAtTime(currentAudio.time);
            if (line != null)
            {
                var speaker = currentTrack.speaker;
                var lineText = line.text;
                
                // Get overrides
                lineText = ProcessOverrides(lineText, ref speaker);

                if ((speakerNameText) && (speaker != null))
                {
                    speakerNameText.enabled = true;
                    speakerNameText.text = speaker.displayName;
                    speakerNameText.color = speaker.nameColor;
                    if (overrideFonts)
                    {
                        speakerNameText.font = font;
                        speakerNameText.material = material;
                        speakerNameText.fontSize = defaultSpeakerFontSize * textScale;
                    }
                }
                else
                {
                    speakerNameText.enabled = false;
                }

                var lines = lineText.Split('\n');
                for (int i = 0; i < subtitleLines.Length; i++)
                {
                    if (i < lines.Length)
                    {
                        subtitleLines[i].text = lines[i];
                        subtitleLines[i].enabled = true;

                        if (overrideFonts)
                        {
                            subtitleLines[i].font = font;
                            subtitleLines[i].material = material;
                            subtitleLines[i].fontSize = defaultTextData[i].size * textScale;
                            if (enableSpeakerFontColor)
                            {
                                subtitleLines[i].color = speaker != null ? speaker.textColor : defaultTextData[i].color;
                            }
                            
                        }
                    }
                    else
                    {
                        subtitleLines[i].text = string.Empty;
                        subtitleLines[i].enabled = false;
                    }
                }
            }
            else
            {
                if (speakerNameText) speakerNameText.enabled = false;
                foreach (var l in subtitleLines)
                {
                    l.text = string.Empty;
                    l.enabled = false;
                }
            }
        }
        else
        {
            speakerNameText.enabled = false;
            foreach (var l in subtitleLines)
            {
                l.text = string.Empty;
                l.enabled = false;
            }
            currentTrack = null;
            currentAudio = null;
        }

        if (overrideFonts)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    private string ProcessOverrides(string lineText, ref Speaker speaker)
    {
        if (string.IsNullOrEmpty(lineText)) return lineText;

        // Keep consuming leading [key=val] blocks
        int safety = 0;
        while ((lineText.Length > 0) && (lineText[0] == '['))
        {
            int close = lineText.IndexOf(']');
            if (close <= 0) break; 

            string token = lineText.Substring(1, close - 1);
            lineText = (close + 1 < lineText.Length) ? lineText.Substring(close + 1) : string.Empty;

            int eq = token.IndexOf('=');
            string key, value;
            if (eq >= 0)
            {
                key = token.Substring(0, eq).Trim();
                value = token.Substring(eq + 1).Trim();
            }
            else
            {
                key = token.Trim();
                value = string.Empty;
            }

            if (key.Equals("speaker", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    speaker = GetSpeaker(value);
                }
            }

            if (++safety > 32) break;

            if (lineText.Length == 0 || lineText[0] != '[') break;
        }

        return lineText;
    }

    private Speaker GetSpeaker(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return currentTrack.speaker;
        }

        if (name.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Compare case-insensitively against the current speaker
        if ((currentTrack.speaker != null) && (currentTrack.speaker.NameMatches(name)))
        {
            return currentTrack.speaker;
        }

        // Search additional speakers safely
        if (currentTrack.additionalSpeakers != null)
        {
            foreach (var s in currentTrack.additionalSpeakers)
            {
                if ((s != null) && (s.NameMatches(name)))
                {
                    return s;
                }
            }
        }

        Debug.LogWarning($"Can't find speaker '{name}' for text '{(currentAudio ? currentAudio.name : "UnknownAudio")}'!");
        return currentTrack.speaker;
    }

    public void SetFont(TMP_FontAsset font, Material material, float textScale = 1.0f)
    {
        overrideFonts = true;
        this.font = font;
        this.material = material;
        this.textScale = textScale;
    }

    public SoundDef _GetCurrentSound()
    {
        return currentTrack;
    }

    public void _StopCurrentSound()
    {
        if (currentAudio)
        {
            currentAudio.Stop();
            currentTrack = null;
            currentAudio = null;
        }
    }


    // Statics

    public static void DisplaySubtitle(SoundDef soundDef, AudioSource source)
    {
        if (_instance == null) return;

        _instance.currentTrack = soundDef;
        _instance.currentAudio = source;
    }

    public static SoundDef GetCurrentSound()
    {
        return Instance._GetCurrentSound();
    }

    public static void StopCurrentSound()
    {
        Instance._StopCurrentSound();
    }
}
