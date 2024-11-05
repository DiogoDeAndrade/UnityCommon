using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class UITrackAudioSource : UITrackObject
{
    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    protected override bool GetVisibility()
    {
        if (audioSource == null) return false;
        if (!audioSource.isPlaying) return false;

        return base.GetVisibility();
    }
}
