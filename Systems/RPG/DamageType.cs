using NaughtyAttributes;
using UnityEngine;

namespace UC.RPG
{

    [CreateAssetMenu(fileName = "DamageType", menuName = "Unity Common/Data/Damage Type")]
    public class DamageType : ScriptableObject
    {
        public string   displayName;
        public Color    displaySpriteColor = Color.white;
        public Sprite   displaySprite;
        public Color    displayTextColor = Color.white;
    }
}
