using NaughtyAttributes;
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
    SubtitleTrack           currentTrack;
    Speaker                 currentSpeaker;
    float                   defaultSpeakerFontSize;
    List<DefaultTextData>   defaultTextData;
    RectTransform           rectTransform;

    void Start()
    {
        if (_instance == null)
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

    public static void DisplaySubtitle(SubtitleTrack track, Speaker speaker, AudioSource source)
    {
        if (_instance == null) return;

        _instance.currentTrack = track;
        _instance.currentAudio = source;
        _instance.currentSpeaker = speaker;
    }

    void Update()
    {
        if ((currentTrack != null) && (currentAudio != null) && (currentAudio.isPlaying))
        {
            var line = currentTrack.GetAtTime(currentAudio.time);
            if (line != null)
            {
                if ((speakerNameText) && (currentSpeaker != null))
                {
                    speakerNameText.enabled = true;
                    speakerNameText.text = currentSpeaker.displayName;
                    speakerNameText.color = currentSpeaker.nameColor;
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

                var lines = line.text.Split('\n');
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
                                subtitleLines[i].color = currentSpeaker != null ? currentSpeaker.textColor : defaultTextData[i].color;
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

    public void SetFont(TMP_FontAsset font, Material material, float textScale = 1.0f)
    {
        overrideFonts = true;
        this.font = font;
        this.material = material;
        this.textScale = textScale;
    }
}
