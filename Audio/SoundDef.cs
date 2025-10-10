using NaughtyAttributes;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "SoundDef", menuName = "Unity Common/Data/SoundDef")]
    public class SoundDef : ScriptableObject
    {
        [Flags]
        public enum SoundFlags { None = 0, Interruptable = 1 };

        public AudioClip        clip;
        public SoundType        soundType = SoundType.PrimaryFX;
        [ShowIf(nameof(isNotMusic))]
        public bool             loop = false;
        public SoundFlags       soundFlags = SoundFlags.None;
        public SubtitleTrack    subtitleTrack;
        public Speaker          speaker;
        public Speaker[]        additionalSpeakers;
        public Vector2          volumeRange = new Vector2(1f, 1f);
        public Vector2          pitchRange = new Vector2(1f, 1f);

        bool isNotMusic => soundType != SoundType.Music;
        bool isVoice => soundType == SoundType.Voice;

        public AudioSource Play()
        {
            return Play(1.0f, 1.0f);
        }

        public AudioSource Play(float volumeMultiplier = 1.0f, float pitchMultiplier = 1.0f)
        {
            AudioSource ret = null;

            if (isNotMusic)
            {
                if (loop)
                {
                    ret = SoundManager.PlayLoopSound(soundType, clip, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random());
                }
                else
                {
                    ret = SoundManager.PlaySound(soundType, clip, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random());
                }
            }
            else
            {
                ret = SoundManager.PlayMusic(clip, volumeMultiplier * volumeRange.Random(), pitchMultiplier * pitchRange.Random());
            }

            if (subtitleTrack)
            {
                // If subtitle is playing, and if it is an interruptable sound, interrupt it
                var currentSnd = SubtitleDisplayManager.GetCurrentSound();
                if (currentSnd != null)
                {
                    SubtitleDisplayManager.StopCurrentSound();
                }
                // Play subtitles
                SubtitleDisplayManager.DisplaySubtitle(this, ret);
            }

            return ret;
        }
    }

#if UNITY_EDITOR
    public static class SoundDefFromSelection
    {
        [MenuItem("Assets/Unity Common Tools/Create SoundDef From Selection", true)]
        private static bool CreateFromSelectionValidate()
        {
            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets);
            return clips.Length == 1;
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
            AudioClip clip = clips[0];

            // Choose output folder (based on first selected object)
            var firstPath = AssetDatabase.GetAssetPath(clip);
            var outFolder = Directory.Exists(firstPath) ? firstPath : Path.GetDirectoryName(firstPath);
            if (string.IsNullOrEmpty(outFolder)) outFolder = "Assets";

            var sd = ScriptableObject.CreateInstance<SoundDef>();
            sd.clip = clip;
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

            var assetName = $"{clip.name}.asset";
            var path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outFolder, assetName));

            AssetDatabase.CreateAsset(sd, path);
            EditorUtility.SetDirty(sd);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }        
    }
#endif
}
