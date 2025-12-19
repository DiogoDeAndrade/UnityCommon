using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC.RPG
{
    [Serializable]
    [PolymorphicName("RPG/Item/Gear")]
    public class RPGItemGear : SOModule
    {
        [SerializeField]
        private Hypertag[]          equipmentSlots;
        [SerializeReference]
        private EquipCondition[]    equipConditions;

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
                    if (!condition.CanEquip(slot, entity, scriptableObject as Item)) return false;
                }
            }

            return true;
        }

        public List<Hypertag> GetEquipmentSlots()
        {
            if ((cachedEquipmentSlots == null) || (cachedEquipmentSlots.Count == 0))
            {
                HashSet<Hypertag> ret = new();
                if (scriptableObject.parents != null)
                {
                    foreach (var item in scriptableObject.parents)
                    {
                        var parentGear = item.GetModules<RPGItemGear>(true);
                        foreach (var p in parentGear) 
                        {
                            var slots = p.GetEquipmentSlots();
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
}
