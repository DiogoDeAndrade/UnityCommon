using UC;
using UnityEngine;
using UnityEngine.Audio;

public static class AudioMixerGroupExtensions 
{
    public static Tweener.BaseInterpolator Tween(this AudioMixerGroup audioMixerGroup, GameObject tweenerObject, string propertyName, float target, float duration)
    {
        audioMixerGroup.audioMixer.GetFloat(propertyName, out float currentValue);

        return tweenerObject.Tween().Interpolate(currentValue, target, duration,
                (value) =>
                {
                    if (audioMixerGroup) audioMixerGroup.audioMixer.SetFloat(propertyName, value);
                });
    }
}
