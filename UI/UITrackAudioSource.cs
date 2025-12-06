using UnityEngine;

namespace UC
{

    [RequireComponent(typeof(AudioSource))]
    public class UITrackAudioSource : UIImageFromObject
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
}