using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "SoundDef", menuName = "Unity Common/Data/SoundDef")]
    public class SoundDef : ScriptableObject
    {
        public AudioClip        clip;
        public SoundType        soundType = SoundType.PrimaryFX;
        [ShowIf(nameof(isNotMusic))]
        public bool             loop = false;
        public SubtitleTrack    subtitleTrack;
        public Speaker          speaker;
        public Vector2          volumeRange = new Vector2(1f, 1f);
        public Vector2          pitchRange = new Vector2(1f, 1f);

        bool isNotMusic => soundType != SoundType.Music;
        bool isVoice => soundType == SoundType.Voice;

        public AudioSource Play()
        {
            AudioSource ret = null;

            if (isNotMusic)
            {
                if (loop)
                {
                    ret = SoundManager.PlayLoopSound(soundType, clip, volumeRange.Random(), pitchRange.Random());
                }
                else
                {
                    ret = SoundManager.PlaySound(soundType, clip, volumeRange.Random(), pitchRange.Random());
                }
            }
            else
            {
                ret = SoundManager.PlayMusic(clip, volumeRange.Random(), pitchRange.Random());
            }

            if (subtitleTrack)
            {
                // Play subtitles
                SubtitleDisplayManager.DisplaySubtitle(subtitleTrack, speaker, ret);
            }

            return ret;
        }
    }
}
