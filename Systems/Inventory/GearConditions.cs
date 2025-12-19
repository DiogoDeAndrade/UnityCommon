using UnityEngine;

namespace UC.RPG
{

    [System.Serializable]
    public class LevelRequirement : EquipCondition
    {
        [SerializeField, Tooltip("If minLevel = -1, then the minimum level is the level of the item itself")] private int minLevel = -1;

        public override bool CanEquip(Hypertag slot, RPGEntity entity, Item item)
        {
            if ((entity.level < minLevel) && (minLevel >= 0)) return false;
            if ((entity.level < item.level) && (minLevel == -1)) return false;

            return true;
        }
    }

    [System.Serializable]
    public class StatRequirement : EquipCondition
    {
        [SerializeField] private StatType stat;
        [SerializeField] private float    minValue;

        public override bool CanEquip(Hypertag slot, RPGEntity entity, Item item)
        {
            if (entity.Get(stat).GetValue() < minValue) return false;

            return true;
        }
    }

}