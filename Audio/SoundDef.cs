using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "SoundDef", menuName = "Unity Common/Data/SoundDef")]
    public class SoundDef : ScriptableObject
    {
        public enum Mode { Single, Multiple };

        [Flags]
        public enum SoundFlags { Interruptable = 1, Default3d = 4, MaxInstances = 8 };

        public Mode                 mode = Mode.Single;
        [ShowIf(nameof(isSingle))]
        public AudioClip            clip;
        [ShowIf(nameof(isMultiple))]
        public AudioClipProbList    clips;
        public SoundType            soundType = SoundType.PrimaryFX;
        [ShowIf(nameof(isNotMusic))]
        public bool                 loop = false;
        public SoundFlags       soundFlags = 0;
        public SubtitleTrack    subtitleTrack;
        public Speaker          speaker;
        public Speaker[]        additionalSpeakers;
        [MinMaxSlider(0.0f, 1.0f)]
        public Vector2          volumeRange = new Vector2(1f, 1f);
        [MinMaxSlider(0.0f, 2.0f)]
        public Vector2          pitchRange = new Vector2(1f, 1f);
        [ShowIf(nameof(is3d))]        
        public Vector2          distanceRange = new Vector2(0.0f, 100.0f);
        public Hypertag         defaultTag;
        [SerializeField, Range(0.0f, 1.0f)]
        protected float         playProbability = 1.0f;
        [SerializeField, ShowIf(nameof(isMaxInstances))]
        protected int           maxInstances = 0;
        [SerializeField, ShowIf(nameof(isMaxInstances))]
        protected bool          hijackSound = false;  

        bool isNotMusic => soundType != SoundType.Music;
        bool isVoice => soundType == SoundType.Voice;
        bool is3d => (soundFlags & SoundFlags.Default3d) != 0;
        bool isInterruptable => (soundFlags & SoundFlags.Interruptable) != 0;
        bool isMaxInstances => (soundFlags & SoundFlags.MaxInstances) != 0;
        bool isSingle => mode == Mode.Single;
        bool isMultiple => mode == Mode.Multiple;

        public AudioSource Play()
        {
            return Play(1.0f, 1.0f, -float.MaxValue);
        }

        public AudioSource Play(float crossfadeTime)
        {
            return Play(1.0f, 1.0f, crossfadeTime);
        }

        public AudioSource Play(float volumeMultiplier = 1.0f, float pitchMultiplier = 1.0f, float crossfadeTime = -float.MaxValue, Vector3 position = default, bool force3d = false, Transform prsObject = null)
        {
            if (UnityEngine.Random.value > playProbability) return null;

            if (isMultiple)
            {
                clip = clips.Get();
            }

            if (isMaxInstances)
            {
                if (SoundManager.CountInstances(clip) >= maxInstances)
                {
                    return null;
                }
            }

            if (subtitleTrack)
            {
                // If subtitle is playing, and if it is an interruptable sound, interrupt it
                var currentSnd = SubtitleDisplayManager.GetCurrentSound();
                if ((currentSnd != null) && (currentSnd.isInterruptable))
                {
                    SubtitleDisplayManager.StopCurrentSound();
                }
            }

            AudioSource ret = null;

            if (isNotMusic)
            {
                if (loop)
                {
                    if (prsObject)
                        ret = SoundManager.PlaySoundAndFollow(soundType, clip, true, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random(), defaultTag, distanceRange, prsObject);
                    else
                        ret = SoundManager.PlaySound(soundType, clip, true, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random(), defaultTag, is3d || force3d, distanceRange, position);
                }
                else
                {
                    if (prsObject)
                        ret = SoundManager.PlaySoundAndFollow(soundType, clip, false, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random(), defaultTag, distanceRange, prsObject);
                    else
                        ret = SoundManager.PlaySound(soundType, clip, false, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random(), defaultTag, is3d || force3d, distanceRange, position);
                }
            }
            else
            {
                if ((force3d) || (prsObject != null))
                {
                    DebugHelpers.LogWarning("Positional music is not supported...");
                }

                ret = SoundManager.PlayMusic(clip, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random(), crossfadeTime, defaultTag);
            }

            if (subtitleTrack)
            {
                // Play subtitles
                SubtitleDisplayManager.DisplaySubtitle(this, ret);
            }

            return ret;
        }

        public AudioSource FadeIn(float time)
        {
            var audioSource = Play(1.0f, 1.0f, -float.MaxValue);
            var targetVolume = audioSource.volume;
            audioSource.volume = 0.0f;
            audioSource.FadeTo(targetVolume, time);

            return audioSource;
        }

#if UNITY_EDITOR
        static AudioSource previewSource;

        [Button("Preview")]
        void Preview()
        {
            var previewClip = isMultiple ? clips.Get() : clip;
            if (previewClip == null) return;

            if (previewSource == null)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags("SoundDef Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
                previewSource = go.GetComponent<AudioSource>();
            }

            previewSource.Stop();
            previewSource.clip = previewClip;
            previewSource.volume = volumeRange.Random();
            previewSource.pitch = pitchRange.Random();
            previewSource.Play();
        }

        [Button("Stop Preview")]
        void StopPreview()
        {
            if (previewSource) previewSource.Stop();
        }
#endif
    }

#if UNITY_EDITOR
    public static class SoundDefFromSelection
    {
        [MenuItem("Assets/Unity Common Tools/Create SoundDef From Selection", true)]
        private static bool CreateFromSelectionValidate()
        {
            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets);
            return (clips.Length == 1) || (clips.Length == Selection.count);
        }

        [MenuItem("Assets/Unity Common Tools/Create SoundDef From Selection")]
        private static void CreateFromSelection()
        {
            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets);
            var subtitles = Selection.GetFiltered<SubtitleTrack>(SelectionMode.DeepAssets);
            var speakers = Selection.GetFiltered<Speaker>(SelectionMode.DeepAssets);

            // If user picked exactly one SubtitleTrack and/or one Speaker, treat them as defaults
            SubtitleTrack subtitle = subtitles.Length >= 1 ? subtitles[0] : null;
            Speaker speaker = speakers.Length >= 1 ? speakers[0] : null;

            // With several clips, ask if they should all go into a single SoundDef (Multiple mode), or one SoundDef each
            bool singleSoundDef = false;
            if (clips.Length > 1)
            {
                int option = EditorUtility.DisplayDialogComplex("Create SoundDef From Selection", $"{clips.Length} audio clips selected.\n\nCreate a single SoundDef with all of them, or one SoundDef per clip?", "Single SoundDef", "Cancel", "One Per Clip");
                if (option == 1) return;
                singleSoundDef = (option == 0);
            }

            // Each group of clips becomes one SoundDef
            var groups = new List<AudioClip[]>();
            if (singleSoundDef)
            {
                Array.Sort(clips, (a, b) => EditorUtility.NaturalCompare(a.name, b.name));
                groups.Add(clips);
            }
            else
            {
                foreach (var clip in clips) groups.Add(new[] { clip });
            }

            foreach (var group in groups)
            {
                // Choose output folder (based on first selected object)
                var firstPath = AssetDatabase.GetAssetPath(group[0]);
                var outFolder = Directory.Exists(firstPath) ? firstPath : Path.GetDirectoryName(firstPath);
                if (string.IsNullOrEmpty(outFolder)) outFolder = "Assets";

                var sd = ScriptableObject.CreateInstance<SoundDef>();
                if (group.Length == 1)
                {
                    sd.clip = group[0];
                }
                else
                {
                    sd.mode = SoundDef.Mode.Multiple;
                    sd.clips = new AudioClipProbList();
                    foreach (var clip in group) sd.clips.Add(clip, 1.0f);
                }
                if (speaker)
                {
                    sd.soundType = SoundType.Voice;
                    sd.subtitleTrack = subtitle;
                    sd.speaker = speaker;
                    if (speakers.Length > 1)
                    {
                        sd.additionalSpeakers = new Speaker[speakers.Length - 1];
                        for (int i = 1; i < speakers.Length; i++) sd.additionalSpeakers[i - 1] = speakers[i];
                    }
                }
                else
                {
                    sd.soundType = SoundType.PrimaryFX;
                    sd.subtitleTrack = subtitle;
                }

                var assetName = $"{GetCommonName(group)}.asset";
                var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outFolder, assetName));

                AssetDatabase.CreateAsset(sd, path);
                EditorUtility.SetDirty(sd);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Common start of the clip names, without trailing separators and digits (Jump_01, Jump_02 => Jump)
        private static string GetCommonName(AudioClip[] clips)
        {
            if (clips.Length == 1) return clips[0].name;

            string prefix = clips[0].name;
            foreach (var clip in clips)
            {
                int len = 0;
                while ((len < prefix.Length) && (len < clip.name.Length) && (prefix[len] == clip.name[len])) len++;
                prefix = prefix.Substring(0, len);
            }
            prefix = prefix.TrimEnd('_', '-', ' ', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

            return (prefix.Length > 0) ? prefix : clips[0].name;
        }
    }
#endif
}
