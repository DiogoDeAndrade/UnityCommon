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

        public static Tweener.BaseInterpolator PitchTo(this AudioSource audioSource, float targetShift, float time)
        {
            return audioSource.Tween().Interpolate(audioSource.pitch, targetShift, time,
                (value) =>
                {
                    if (audioSource) audioSource.pitch = value;
                });
        }
    }
}