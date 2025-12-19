using NaughtyAttributes;
using System;
using UnityEngine;

namespace UC.RPG
{

    [CreateAssetMenu(fileName = "StatType", menuName = "Unity Common/RPG/Stat Type")]
    public class StatType : ScriptableObject
    {
        [Flags]
        public enum UpdateMode { Always = 1, Start = 2 };

        public string       displayName;
        public string       abreviation;
        public Color        displaySpriteColor = Color.white;
        public Sprite       displaySprite;
        public Color        displayTextColor = Color.white;
        public float        baseValue = 1;
        public bool         isPercentage = false;
        [ShowIf(nameof(isNotPercentage))]
        public bool         hasMaxValue = false;
        [ShowIf(nameof(needMaxValue))]
        public float        maxValue = 100;
        [SerializeField]
        private UpdateMode  updateMode = UpdateMode.Start;

        bool isNotPercentage => !isPercentage;
        bool needMaxValue => isNotPercentage && hasMaxValue;

        public bool isUpdateAlways => (updateMode & UpdateMode.Always) != 0;
        public bool isUpdateOnStart => (updateMode & UpdateMode.Start) != 0;

        public bool NeedUpdate(UpdateMode mode) => (updateMode & mode) != 0;
    }
}