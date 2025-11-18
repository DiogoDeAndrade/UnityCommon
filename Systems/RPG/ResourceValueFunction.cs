using UnityEngine;

namespace UC.RPG
{
    [System.Serializable]
    public abstract class ResourceValueFunction
    {
        public abstract float GetValue(RPGEntity character);
    }

    [System.Serializable]
    public class ResourceValueFunctionConstant : ResourceValueFunction
    {
        public float baseValue = 1.0f;

        public override float GetValue(RPGEntity character)
        {
            return baseValue;
        }
    }

    [System.Serializable]
    public class ResourceValueFunctionLevelLinear : ResourceValueFunction
    {
        public float baseValue = 1.0f;
        public float valuePerLevel = 1.0f;

        public override float GetValue(RPGEntity character)
        {
            return Mathf.FloorToInt(baseValue + character.level * valuePerLevel);
        }
    }

    [System.Serializable]
    public class ResourceValueFunctionStatLinear : ResourceValueFunction
    {
        public float    baseValue = 1.0f;
        public float    valuePerLevel = 1.0f;
        public StatType stat;

        public override float GetValue(RPGEntity character)
        {
            return Mathf.FloorToInt(baseValue + character.Get(stat).value * valuePerLevel);
        }
    }
}
