using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace UC
{

    public enum SoundType { Music = 0, PrimaryFX = 1, SecondaryFX = 2, Background = 3, Voice = 4 };

    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;

        [SerializeField] private bool dontDestroyOnLoad = false;
        [SerializeField] private AudioMixerGroup defaultMixerOutput;
        [SerializeField] private AudioMixerGroup musicMixerGroup;
        [SerializeField] private AudioMixerGroup fx1MixerGroup;
        [SerializeField] private AudioMixerGroup fx2MixerGroup;
        [SerializeField] private AudioMixerGroup backgroundMixerGroup;
        [SerializeField] private AudioMixerGroup voiceMixerGroup;
        [SerializeField] private AudioClip startMusic;
        [SerializeField] private float defaultCrossfadeTime = 1.0f;

        List<AudioSource> audioSources;
        AudioMixerGroup[] mixerGroups;
        AudioSource musicSource;

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
        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                if (dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else
            {
                Destroy(gameObject);
                return;
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

        private void Start()
        {
            if (startMusic)
            {
                musicSource = _PlayMusic(startMusic, 1.0f, 1.0f);
            }
        }

        void _StopMusic()
        {
            if (musicSource)
            {
                var tmp = musicSource;
                musicSource.FadeTo(0.0f, defaultCrossfadeTime).Done(() =>
                {
                    tmp.Stop();
                    tmp.clip = null;
                    tmp.loop = false;
                });
                musicSource = null;
            }
        }

        private AudioSource _PlayMusic(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            if (musicSource == null)
            {
                if (clip == null) return null;

                musicSource = _PlaySound(SoundType.Music, clip, 1, 1);
                musicSource.loop = true;

                return musicSource;
            }

            if (musicSource.clip == clip)
            {
                return musicSource;
            }

            if (clip == null)
            {
                _StopMusic();
                return null;
            }

            // Crossfade
            var newMusicSource = _PlaySound(SoundType.Music, clip, 0, 1);
            newMusicSource.loop = true;

            newMusicSource.FadeTo(volume, defaultCrossfadeTime);
            var oldMusicSource = musicSource;
            musicSource.FadeTo(0.0f, defaultCrossfadeTime).Done(
                () =>
                {
                    oldMusicSource.Stop();
                    oldMusicSource.loop = false;
                });

            musicSource = newMusicSource;

            return newMusicSource;
        }

        private AudioSource _PlaySound(SoundType type, AudioClip clip, float volume = 1.0f, float pitch = 1.0f, bool loop = false)
        {
            var audioSource = GetSource();

            audioSource.clip = clip;
            audioSource.loop = loop;
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
            if (_instance == null) return null;
            return _instance._PlaySound(type, clip, volume, pitch);
        }


        static public AudioSource PlayMusic(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            if (_instance == null) return null;
            return _instance._PlayMusic(clip, volume, pitch);
        }

        static public AudioSource PlayLoopSound(SoundType type, AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            if (_instance == null) return null;
            return _instance._PlaySound(type, clip, volume, pitch, true);
        }
    }
}