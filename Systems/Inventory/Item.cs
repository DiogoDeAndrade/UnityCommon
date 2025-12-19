using NaughtyAttributes;
using System;
using System.Linq;
using UC.RPG;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Item", menuName = "Unity Common/RPG/Item")]
    public class Item : ModularScriptableObject
    {
        [Header("Item Stats")]
        public int          level = 1;
        public string       displayName = "Item Display Name";
        public Color        displaySpriteColor = Color.white;
        public Sprite       displaySprite;
        public Color        displayTextColor = Color.white;
        [ResizableTextArea]
        public string       description;
        [ResizableTextArea]
        private string      _tooltip;
        [SerializeField] 
        protected GameObject  scenePrefab;
        public bool         isStackable = false;
        [ShowIf(nameof(isStackable))]
        public int          maxStack = 1;

        public string tooltip => (string.IsNullOrEmpty(_tooltip)) ? (description) : (_tooltip);
        
        internal bool IsA(Item itemType)
        {
            if (this == itemType) return true;

            foreach (var p in parents.OfType<Item>())
            {
                if (p.IsA(itemType)) return true;
            }

            return false;
        }
    }
}
