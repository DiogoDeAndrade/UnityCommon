using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum SoundType { Music = 0, PrimaryFX = 1, SecondaryFX = 2, Background = 3, Voice = 4 };

public class SoundManager : MonoBehaviour
{
    private static SoundManager _instance;

    [SerializeField] private AudioMixerGroup defaultMixerOutput;
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup fx1MixerGroup;
    [SerializeField] private AudioMixerGroup fx2MixerGroup;
    [SerializeField] private AudioMixerGroup backgroundMixerGroup;
    [SerializeField] private AudioMixerGroup voiceMixerGroup;

    List<AudioSource> audioSources;
    AudioMixerGroup[] mixerGroups;

    public static SoundManager instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<SoundManager>();
            }
            return _instance;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        // Find all audio sources
        audioSources = new List<AudioSource>(GetComponentsInChildren<AudioSource>());
        if (audioSources == null)
        {
            audioSources = new List<AudioSource>();
        }

        mixerGroups = new AudioMixerGroup[5];
        mixerGroups[0] = defaultMixerOutput;
        mixerGroups[0] = (musicMixerGroup != null) ? (musicMixerGroup) : (defaultMixerOutput);
        mixerGroups[1] = (fx1MixerGroup != null) ? (fx1MixerGroup) : (defaultMixerOutput);
        mixerGroups[2] = (fx2MixerGroup != null) ? (fx2MixerGroup) : (defaultMixerOutput);
        mixerGroups[3] = (backgroundMixerGroup != null) ? (backgroundMixerGroup) : (defaultMixerOutput);
        mixerGroups[4] = (voiceMixerGroup != null) ? (voiceMixerGroup) : (defaultMixerOutput);
    }

    private AudioSource _PlaySound(SoundType type, AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        var audioSource = GetSource();

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.outputAudioMixerGroup = mixerGroups[(int)type];

        audioSource.Play();

        return audioSource;
    }

    private AudioSource GetSource()
    {
        if (audioSources == null)
        {
            audioSources = new List<AudioSource>();
            return NewSource();
        }

        foreach (var source in audioSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        return NewSource();
    }

    private AudioSource NewSource()
    {
        GameObject go = new GameObject();
        go.name = "Audio Source";
        go.transform.SetParent(transform);

        var audioSource = go.AddComponent<AudioSource>();

        audioSources.Add(audioSource);

        return audioSource;
    }

    static public AudioSource PlaySound(SoundType type, AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        return _instance._PlaySound(type, clip, volume, pitch);
    }
}