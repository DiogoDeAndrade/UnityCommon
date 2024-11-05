using System.Collections;
using UnityEngine;

public class WaitForSound : CustomYieldInstruction
{
    private AudioSource audioSource;

    public WaitForSound(AudioSource source)
    {
        audioSource = source;
    }

    public override bool keepWaiting
    {
        get
        {
            // Wait until the AudioSource is not playing anymore
            if (audioSource == null) return false;
            return audioSource.isPlaying;
        }
    }
}