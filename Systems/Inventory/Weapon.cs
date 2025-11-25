using System;
using UC.RPG;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Weapon", menuName = "Unity Common/RPG/Weapon")]
    public partial class Weapon : Gear
    {
        [Header("Weapon")]
        [SerializeField]
        protected DamageType   damageType;
        [SerializeReference]
        protected AttackModule attackModule;
        [SerializeField]
        protected SoundDef     attackSound;

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
