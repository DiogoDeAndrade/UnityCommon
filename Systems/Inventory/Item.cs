using NaughtyAttributes;
using System;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Item", menuName = "Unity Common/RPG/Item")]
    public class Item : ScriptableObject
    {
        [Header("Item Stats")]
        public Item[]       parentItems;
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

            if (parentItems == null) return false;

            foreach (var item in parentItems)
            {
                if (item == itemType) return true;
            }

            return false;
        }

        protected RetObjType Get<RetObjType, OwnerObjType>(Func<OwnerObjType, RetObjType> func) where OwnerObjType : class
        {
            var ret = func(this as OwnerObjType);
            if (ret != null) return ret;

            foreach (var item in parentItems)
            {
                if (item is OwnerObjType ownerObj)
                {
                    ret = func(ownerObj);
                    if (ret != null) return ret;
                }
            }

            return default;
        }

        public GameObject GetScenePrefab() => Get<GameObject, Item>((obj) => obj.scenePrefab);
    }
}
