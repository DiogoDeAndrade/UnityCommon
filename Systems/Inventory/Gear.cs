using System;
using System.Collections.Generic;
using System.Linq;
using UC.RPG;
using UnityEngine;

namespace UC
{

    public partial class Gear : Item
    {
        [Header("Gear")]
        [SerializeField]
        private Hypertag[] equipmentSlots;
        [SerializeReference]
        private EquipCondition[] equipConditions;

        List<Hypertag> cachedEquipmentSlots;

        public bool CanEquip(Hypertag slot, RPGEntity entity)
        {
            var slots = GetEquipmentSlots();
            if (slots.IndexOf(slot) < 0)
            {
                return false;
            }

            if (equipConditions != null)
            {
                foreach (var condition in equipConditions)
                {
                    if (!condition.CanEquip(slot, entity, this)) return false;
                }
            }

            return true;
        }

        public List<Hypertag> GetEquipmentSlots()
        {
            if ((cachedEquipmentSlots == null) || (cachedEquipmentSlots.Count == 0))
            {
                HashSet<Hypertag> ret = new();
                if (parentItems != null)
                {
                    foreach (var item in parentItems)
                    {
                        var parentGear = item as Gear;
                        if (parentGear != null)
                        {
                            var slots = parentGear.GetEquipmentSlots();
                            foreach (var s in slots) ret.Add(s);
                        }
                    }
                }

                foreach (var s in equipmentSlots) ret.Add(s);

                cachedEquipmentSlots = ret.ToList();
            }

            return cachedEquipmentSlots;
        }
    }

    [Serializable]
    public abstract class EquipCondition
    {
        public abstract bool CanEquip(Hypertag slot, RPGEntity entity, Item item);
    }
}
