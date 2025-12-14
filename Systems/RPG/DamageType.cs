using NaughtyAttributes;
using UnityEngine;

namespace UC.RPG
{

    [CreateAssetMenu(fileName = "DamageType", menuName = "Unity Common/RPG/Damage Type")]
    public class DamageType : ScriptableObject
    {
        public string   displayName;
        public Color    displaySpriteColor = Color.white;
        public Sprite   displaySprite;
        public Color    displayTextColor = Color.white;

        public string   displayTextColorHex => ColorUtility.ToHtmlStringRGBA(displayTextColor);

        public string ToRTF()
        {
            return $"<color=#{displayTextColorHex}>{displayName}</color>";
        }
    }
}
