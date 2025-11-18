using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Item", menuName = "Unity Common/Data/Item")]
    public class Item : ScriptableObject
    {
        [Header("Item Stats")]
        public Item[]       parentItems;
        public string       displayName = "Item Display Name";
        public Color        displaySpriteColor = Color.white;
        public Sprite       displaySprite;
        public Color        displayTextColor = Color.white;
        [SerializeField] 
        protected GameObject  scenePrefab;
        public bool         isStackable = false;
        [ShowIf(nameof(isStackable))]
        public int          maxStack = 1;

        internal bool IsA(Item itemType)
        {
            if (this == itemType) return true;

            if (parentItems == null) return false;

            foreach (var item in parentItems)
            {
                if (item == itemType) return true;
            }

            return false;
        }

        public GameObject GetScenePrefab()
        {
            if (scenePrefab) return scenePrefab;

            foreach (var parent in parentItems)
            {
                var prefab = parent.GetScenePrefab();
                if (prefab) return prefab;
            }

            return null;
        }
    }
}
