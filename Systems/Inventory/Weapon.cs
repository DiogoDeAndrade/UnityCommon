using System;
using UC.RPG;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Weapon", menuName = "Unity Common/Data/Weapon")]
    public partial class Weapon : Item
    {
        [Header("Weapon")]
        [SerializeField]
        protected DamageType   damageType;
        [SerializeReference]
        protected AttackModule attackModule;
        [SerializeField]
        protected SoundDef     attackSound;

        private RetObjType Get<RetObjType, OwnerObjType>(Func<OwnerObjType, RetObjType> func) where OwnerObjType : class
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

        public DamageType GetDamageType() => Get<DamageType, Weapon>((obj) => obj.damageType);
        public AttackModule GetAttackModule() => Get<AttackModule, Weapon>((obj) => obj.attackModule);
        public SoundDef GetAttackSound() => Get<SoundDef, Weapon>((obj) => obj.attackSound);
    }

    [Serializable]
    public abstract class AttackModule
    {
        public abstract bool Attack(Weapon weapon, RPGEntity source, Vector2Int destPos);
    }
}
