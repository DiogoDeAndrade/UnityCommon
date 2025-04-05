using UnityEngine;

namespace UC
{

    public static class AudioSourceExtensions
    {
        public static Tweener.BaseInterpolator FadeTo(this AudioSource audioSource, float targetVolume, float time)
        {
            return audioSource.Tween().Interpolate(audioSource.volume, targetVolume, time,
                (value) =>
                {
                    if (audioSource) audioSource.volume = value;
                });
        }
    }
}