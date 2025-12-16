using System;
using UnityEngine;

namespace UC.RPG
{
    [Serializable]
    public abstract class HitChanceFunction
    {
        [SerializeField,HideInInspector] protected bool displayGraph = true;

        public abstract float GetValue(RPGEntity weapon, RPGEntity src, RPGEntity target);
        public virtual bool CanPreview() => true;
        public abstract float GetPreviewValue(int diff);
    }

    [Serializable]
    public class HitChanceFunctionConstant : HitChanceFunction
    {
        public float baseValue = 1.0f;

        public override float GetValue(RPGEntity weapon, RPGEntity src, RPGEntity target)
        {
            return baseValue;
        }
        public override float GetPreviewValue(int diff)
        {
            return baseValue;
        }
    }

    [Serializable]
    public class HitChanceFunctionLevelLinear : HitChanceFunction
    {
        public float baseValue = 1.0f;
        public float valuePerLevel = 1.0f;

        public override float GetValue(RPGEntity weapon, RPGEntity src, RPGEntity target)
        {
            return baseValue + src.level * valuePerLevel;
        }

        public override float GetPreviewValue(int diff)
        {
            return diff * valuePerLevel;
        }
    }

    [Serializable]
    public class HitChanceFunctionStatLinear : HitChanceFunction
    {
        public float    baseValue = 1.0f;
        public float    valuePerLevel = 1.0f;
        public StatType stat;

        public override float GetValue(RPGEntity weapon, RPGEntity src, RPGEntity target)
        {
            return baseValue + src.Get(stat).GetValue() * valuePerLevel;
        }

        public override bool CanPreview() => false;

        public override float GetPreviewValue(int diff)
        {
            return 0.0f;
        }

    }
}
