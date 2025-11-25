using UnityEngine;

namespace UC.RPG
{

    [System.Serializable]
    public class LevelRequirement : EquipCondition
    {
        [SerializeField] private int minLevel;

        public override bool CanEquip(Hypertag slot, RPGEntity entity, Item item)
        {
            if (entity.level < minLevel) return false;

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