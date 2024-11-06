using System.Collections;
using UnityEngine;

public static class AudioSourceExtensions
{
    public static void FadeTo(this AudioSource audioSource, float targetVolume, float time)
    {
        audioSource.Tween().Interpolate(audioSource.volume, targetVolume, time, (value) => audioSource.volume= value);
    }
}
