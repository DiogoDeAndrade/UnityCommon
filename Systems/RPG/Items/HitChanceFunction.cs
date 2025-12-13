using System;

namespace UC.RPG
{
    [Serializable]
    public abstract class HitChanceFunction
    {
        public abstract float GetValue(RPGEntity weapon, RPGEntity src, RPGEntity target);
    }

    [Serializable]
    public class HitChanceFunctionConstant : HitChanceFunction
    {
        public float baseValue = 1.0f;

        public override float GetValue(RPGEntity weapon, RPGEntity src, RPGEntity target)
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
    }
}
