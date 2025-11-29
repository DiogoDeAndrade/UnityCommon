using System;
using UC.RPG;

namespace UC
{
    [Serializable]
    public abstract class EquipCondition
    {
        public abstract bool CanEquip(Hypertag slot, RPGEntity entity, Item item);
    }
}
