using NaughtyAttributes;
using System;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace UC.RPG
{
    [Serializable]
    public abstract class ResourceValueFunction
    {
        public abstract float GetValue(RPGEntity character);
        public abstract Vector2 GetMinMax(RPGEntity character);
    }

    [Serializable]
    public class ResourceValueFunctionConstant : ResourceValueFunction
    {
        public float baseValue = 1.0f;

        public override float GetValue(RPGEntity character)
        {
            return baseValue;
        }

        public override Vector2 GetMinMax(RPGEntity character) => new Vector2(baseValue, baseValue);
    }

    [Serializable]
    public class ResourceValueFunctionConstantRange : ResourceValueFunction
    {
        public ValueRange value;

        public override float GetValue(RPGEntity character)
        {
            return value.GetRandom();
        }

        public override Vector2 GetMinMax(RPGEntity character) => new Vector2(value.Min, value.Max);

    }

    [Serializable]
    public class ResourceValueFunctionLevelLinear : ResourceValueFunction
    {
        public float baseValue = 1.0f;
        public float valuePerLevel = 1.0f;

        public override float GetValue(RPGEntity character)
        {
            return Mathf.FloorToInt(baseValue + character.level * valuePerLevel);
        }

        public override Vector2 GetMinMax(RPGEntity character) => Vector2.one * GetValue(character);
    }

    [Serializable]
    public class ResourceValueFunctionStatLinear : ResourceValueFunction
    {
        public float    baseValue = 1.0f;
        public float    valuePerLevel = 1.0f;
        public StatType stat;

        public override float GetValue(RPGEntity character)
        {
            return Mathf.FloorToInt(baseValue + character.Get(stat).GetValue() * valuePerLevel);
        }

        public override Vector2 GetMinMax(RPGEntity character) => Vector2.one * GetValue(character);
    }

    [Serializable]
    public class ResourceValueFunctionNormalizedStatAsymptote : ResourceValueFunction
    {
        public StatType stat;   

        [Tooltip("Maximum value this function asymptotically approaches (0–1, but never actually reaches it).")]
        [Range(0.0f, 1.0f)]
        public float maxValue = 0.5f;

        [Tooltip("How quickly the function approaches maxChance. Higher = faster approach. Use 3 / maxStat as a reference value.")]
        [Min(0f)]
        public float curveStrength = 0.1f;

        [Tooltip("Flat base value before the asymptotic part.")]
        [Range(0.0f, 1.0f)]
        public float baseValue = 0.0f;

        public override float GetValue(RPGEntity character)
        {
            // 1) Read the stat (clamped to non-negative to avoid curve inversion)
            float statValue = Mathf.Max(0, character.Get(stat).GetValue());

            // 2) Asymptotic curve: 1 - exp(-k * x)
            float t = 1.0f - Mathf.Exp(-curveStrength * statValue);

            // 3) Scale to desired range and add baseChance
            float chance = baseValue + maxValue * t;

            // 4) Guarantee [0,1] for safety
            return Mathf.Clamp01(chance);
        }

        public override Vector2 GetMinMax(RPGEntity character) => Vector2.one * GetValue(character);
    }

    [Serializable]
    public class ResourceValueFunctionMultiStatWeightedAdd : ResourceValueFunction
    {
        [Serializable]
        class WeightStatPair
        {
            [SerializeReference]
            public ResourceValueFunction    value;
            [Min(0f)]
            public float                    weight = 1.0f;   
        }

        [SerializeField] 
        private WeightStatPair[]   stats;
        [SerializeField] 
        private float              baseValue = 0f;
        [SerializeField] 
        public bool                clampOutput = true;
        [SerializeField, ShowIf(nameof(clampOutput))] 
        private float              maxValue = 1f;

        public override float GetValue(RPGEntity character)
        {
            float totalValue = 0f;

            foreach (var pair in stats)
            {
                if ((pair == null) || (pair.value == null)) continue;

                totalValue += pair.value.GetValue(character) * pair.weight;
            }

            var value = baseValue + totalValue;
            if (clampOutput) value = Mathf.Clamp(value, 0.0f, maxValue);

            return value;
        }

        public override Vector2 GetMinMax(RPGEntity character) => Vector2.one * GetValue(character);
    }
}
