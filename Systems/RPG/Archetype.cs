using System;
using UnityEngine;

namespace UC.RPG
{

    [CreateAssetMenu(fileName = "Archetype", menuName = "Unity Common/Data/Archetype")]
    public class Archetype : ScriptableObject
    {
        [Serializable]
        public struct Stat
        {
            public StatType                 type;
            [SerializeReference]
            public ResourceValueFunction    calculator;
        }

        [Serializable]
        public struct Resource
        {
            public ResourceType     type;
            [SerializeReference]
            public ResourceValueFunction   calculator;
        }

        [Serializable]
        public struct Generator
        {
            public StatType type;
            [SerializeReference]
            public ResourceValueFunction calculator;
        }

        [Header("Visuals")]
        public string                       displayName;
        public Color                        highlightColor = Color.white;
        public RuntimeAnimatorController    controller;
        [Header("RPG")]
        public Stat[]                       primaryStats;
        public Resource[]                   resources;
        public Weapon                       defaultWeapon;
        [Header("Generator")]
        public Generator[]                  statGenerators;

        public void RunGenerator(StatType type, RPGEntity character, StatInstance statInstance)
        {
            foreach (var g in statGenerators)
            {
                if ((g.type == type) && (g.calculator != null))
                {
                    statInstance.value = g.calculator.GetValue(character);
                }
            }
        }

        public void UpdateDerivedStats(RPGEntity entity)
        {
            foreach (var s in primaryStats)
            {
                if (s.calculator != null)
                {
                    var statInstance = entity.Get(s.type);
                    if (statInstance != null)
                    {
                        statInstance.value = s.calculator.GetValue(entity);
                    }
                }
            }
        }
    }
}
