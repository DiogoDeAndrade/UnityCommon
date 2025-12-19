using System;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Item/Weapon")]
    public class RPGItemWeapon : SOModule
    {
        public      DamageType      damageType;
        public      DistanceRange  range;
        [SerializeReference]
        private     AttackModuleFunction    _attackModule;
        public      SoundDef        attackSound;
        public      SoundDef        missSound;

        public AttackModuleFunction attackModule
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
