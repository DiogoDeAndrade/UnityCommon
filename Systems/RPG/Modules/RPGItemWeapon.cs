using System;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Item/Weapon")]
    public class RPGItemWeapon : RPGItemGear
    {
        [Header("Weapon")]
        public      DamageType      damageType;
        public      DistanceRange  range;
        [SerializeReference]
        private     AttackModule    _attackModule;
        public      SoundDef        attackSound;
        public      SoundDef        missSound;

        public AttackModule attackModule
        {
            get
            {
                if (_attackModule != null) return _attackModule;

                foreach (var p in scriptableObject.parents)
                {
                    var wm = p.GetModule<RPGItemWeapon>(true);
                    if (wm != null)
                    {
                        var tmp = wm.attackModule;
                        if (tmp != null) return tmp;
                    }
                }

                return null;
            }
        }
    }
}
