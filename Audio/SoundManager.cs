using NaughtyAttributes;
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
        [Header("Default volumes")]
        [SerializeField, Range(0.0f, 1.0f)] private float defaultVolume = 1.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float defaultMusicVolume = 1.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float defaultFX1Volume = 1.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float defaultFX2Volume = 1.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float defaultBackgroundVolume = 1.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float defaultVoiceVolume = 1.0f;
        [SerializeField, Header("Default Music")] private AudioClip  startMusic;
        [SerializeField] private Hypertag   musicTag;
        [SerializeField] private float defaultCrossfadeTime = 1.0f;

        List<AudioSource>       audioSources;
        List<HypertaggedObject> audioTags;
        AudioMixerGroup[] mixerGroups;
        AudioSource musicSource;

        class PauseStruct
        {
            public List<AudioSource> pausedSources;
        };

        List<PauseStruct> pauseStack = new();

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
            audioTags = new();
            audioSources = new List<AudioSource>(GetComponentsInChildren<AudioSource>());
            if (audioSources == null)
            {
                audioSources = new();                
            }
            else
            {
                foreach (var audioSource in audioSources)
                {
                    var t = audioSource.GetComponent<HypertaggedObject>();
                    if (t == null) t = audioSource.gameObject.AddComponent<HypertaggedObject>();
                    audioTags.Add(t);
                }
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
            for (int i = 0; i < mixerGroups.Length; i++)
            {
                SoundType type = (SoundType)i;

                // Try to get saved value, otherwise use defaults
                float volume = PlayerPrefs.GetFloat($"{type}Volume", GetDefaultVolume(type));

                _SetVolume(type, volume, true);
            }

            if (startMusic)
            {
                musicSource = _PlayMusic(startMusic, 1.0f, 1.0f, musicTag);
            }
        }
        private AudioSource _PlayMusic(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, Hypertag tag = null)
        {
            return _PlayMusic(clip, volume, pitch, defaultCrossfadeTime, tag);
        }

        private AudioSource _PlayMusic(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, float crossFadeTime = -float.MaxValue, Hypertag defaultTag = null)
        {
            if (musicSource == null) 
            {
                if (clip == null) return null;

                musicSource = _PlaySound(SoundType.Music, clip, 0.0f, 1, true, (defaultTag == null) ? (musicTag) : (defaultTag));
                musicSource.FadeTo(1.0f, (crossFadeTime < 0.0f) ? (defaultCrossfadeTime) : (crossFadeTime));

                return musicSource;
            }

            if (musicSource.clip == clip)
            {
                return musicSource;
            }

            if (clip == null)
            {
                musicSource.FadeTo(0.0f, crossFadeTime).Done(() =>
                {
                    musicSource.Stop();
                    musicSource.loop = false;
                    musicSource = null;
                });
                return null;
            }

            // Crossfade
            var newMusicSource = _PlaySound(SoundType.Music, clip, 0, 1, true, (defaultTag == null) ? (musicTag) : (defaultTag));

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

        private AudioSource _PlaySound(SoundType type, AudioClip clip, float volume = 1.0f, float pitch = 1.0f, bool loop = false, Hypertag tag = null)
        {
            (var audioSource, var hypertaggedObject) = GetSource(tag);

            audioSource.clip = clip;
            audioSource.loop = loop;
            audioSource.volume = volume;
            audioSource.pitch = pitch;
            audioSource.outputAudioMixerGroup = mixerGroups[(int)type];

            audioSource.Play();

            if (tag)
            {

            }

            return audioSource;
        }

        private void _PauseAll()
        {
            PauseStruct p = new();
            p.pausedSources = new();

            foreach (var source in audioSources)
            {
                if (source.isPlaying)
                {
                    p.pausedSources.Add(source);
                    source.Pause();
                }
            }

            audioSources.RemoveAll((src) => p.pausedSources.Contains(src));

            pauseStack.Add(p);
        }

        private void _UnpauseAll()
        {
            if (pauseStack.Count > 0)
            {
                var p = pauseStack.PopLast();

                foreach (var source in p.pausedSources)
                {
                    source.UnPause();
                    audioSources.Add(source);
                }
            }
        }

        private (AudioSource, HypertaggedObject) GetSource(Hypertag tag)
        {
            if (audioSources == null)
            {
                audioSources = new List<AudioSource>();
                return NewSource(tag);
            }

            for (int i = 0; i < audioSources.Count; i++)
            {
                var source = audioSources[i];
                if ((!source.isPlaying) && ((audioTags[i].hypertag == tag) || (tag == null) || (audioTags[i].hypertag == null)))
                {
                    audioTags[i].hypertag = tag;
                    return (source, audioTags[i]);
                }
            }

            return NewSource(tag);
        }

        private (AudioSource, HypertaggedObject) NewSource(Hypertag tag)
        {
            GameObject go = new GameObject();
            go.name = "Audio Source";
            go.transform.SetParent(transform);

            var audioSource = go.AddComponent<AudioSource>();
            var ht = go.AddComponent<HypertaggedObject>();
            ht.hypertag = tag;

            audioSources.Add(audioSource);
            audioTags.Add(ht);

            return (audioSource, ht);
        }
        private AudioSource _GetSound(Hypertag soundTag)
        {
            for (int i = 0; i < audioSources.Count; i++)
            {
                if ((audioSources[i].isPlaying) && (audioTags[i].hypertag == soundTag)) return audioSources[i];
            }

            return null;
        }


        private void _SetVolume(SoundType soundType, float value, bool savePref)
        {
            if (savePref)
            {
                PlayerPrefs.SetFloat($"{soundType}Volume", value);
                PlayerPrefs.Save();
            }

            var mixer = mixerGroups[(int)soundType];
            if (mixer == null) return;

            // Convert 0–1 range to decibels; avoid log(0)
            float dB = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f;

            // Unfortunately, I can only expose properties at the group level, so they have to have different names
            string paramName = $"{soundType}Volume";

            if (mixer.audioMixer.GetFloat(paramName, out float dummy))
            {
                mixer.audioMixer.SetFloat(paramName, dB);
            }
        }

        private float _GetVolume(SoundType soundType)
        {
            string key = $"{soundType}Volume";

            return PlayerPrefs.GetFloat(key, GetDefaultVolume(soundType));
        }

        float GetDefaultVolume(SoundType soundType)
        {
            switch (soundType)
            {
                case SoundType.Music: return defaultMusicVolume;
                case SoundType.PrimaryFX: return defaultFX1Volume;
                case SoundType.SecondaryFX: return defaultFX2Volume;
                case SoundType.Background: return defaultBackgroundVolume;
                case SoundType.Voice: return defaultVoiceVolume;
            }

            return defaultVolume;
        }

        [Button(nameof(ClearPlayerPrefs))]
        void ClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        static public AudioSource PlaySound(SoundType type, AudioClip clip, float volume = 1.0f, float pitch = 1.0f, Hypertag defaultTag = null)
        {
            if (_instance == null) return null;
            return _instance._PlaySound(type, clip, volume, pitch, false, defaultTag);
        }


        static public AudioSource PlayMusic(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, float crossfadeTime = -float.MaxValue, Hypertag defaultTag = null)
        {
            if (_instance == null) return null;

            if (crossfadeTime >= 0.0f)
                return _instance._PlayMusic(clip, volume, pitch, crossfadeTime, (defaultTag == null) ? (_instance.musicTag) : (defaultTag));
            else
                return _instance._PlayMusic(clip, volume, pitch, (defaultTag == null) ? (_instance.musicTag) : (defaultTag));
        }

        static public AudioSource PlayLoopSound(SoundType type, AudioClip clip, float volume = 1.0f, float pitch = 1.0f, Hypertag defaultTag = null)
        {
            if (_instance == null) return null;
            return _instance._PlaySound(type, clip, volume, pitch, true, defaultTag);
        }

        static public void PauseAll()
        {
            if (_instance == null) return;
            _instance._PauseAll();
        }

        static public void UnpauseAll()
        {
            if (_instance == null) return;
            _instance._UnpauseAll();
        }

        static public AudioSource GetSound(Hypertag soundTag)
        {
            if (_instance == null) return null;
            return _instance._GetSound(soundTag);
        }

        static public void SetVolume(SoundType soundType, float value, bool savePref)
        {
            _instance?._SetVolume(soundType, value, savePref);

        }

        static public float GetVolume(SoundType soundType)
        {
            if (_instance == null) return 1.0f;

            return _instance._GetVolume(soundType);
        }        
    }
}