using NaughtyAttributes;
using UnityEngine;

namespace UC.RPG
{

    [CreateAssetMenu(fileName = "StatType", menuName = "Unity Common/Data/Stat Type")]
    public class StatType : ScriptableObject
    {
        public string   displayName;
        public string   abreviation;
        public Color    displaySpriteColor = Color.white;
        public Sprite   displaySprite;
        public Color    displayTextColor = Color.white;
        public float    baseValue = 1;
        public bool     isPercentage = false;
        [ShowIf(nameof(isNotPercentage))]
        public bool     hasMaxValue = false;
        [ShowIf(nameof(needMaxValue))]
        public float    maxValue = 100;

        bool isNotPercentage => !isPercentage;
        bool needMaxValue => isNotPercentage && hasMaxValue;
    }
}