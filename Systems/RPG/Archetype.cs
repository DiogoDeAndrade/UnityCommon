using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{

    [CreateAssetMenu(fileName = "Archetype", menuName = "Unity Common/Data/Archetype")]
    public class Archetype : ScriptableObject
    {
        [Serializable]
        public struct Stat
        {
            public StatType type;
            [SerializeReference]
            public ResourceValueFunction calculator;
        }

        [Serializable]
        public struct Resource
        {
            public ResourceType type;
            [SerializeReference]
            public ResourceValueFunction calculator;
        }

        [Serializable]
        public struct Generator
        {
            public StatType type;
            [SerializeReference]
            public ResourceValueFunction calculator;
        }

        public Archetype baseArchetype;
        [Header("Visuals")]
        public string displayName;
        public Color highlightColor = Color.white;
        [SerializeField]
        private RuntimeAnimatorController controller;
        [Header("Sounds")]
        [SerializeField]
        private SoundDef hitSound;
        [SerializeField]
        private SoundDef deathSound;
        [Header("RPG")]
        [SerializeField]
        private Stat[] primaryStats;
        [SerializeField]
        private Resource[] resources;
        [SerializeField]
        private Weapon defaultWeapon;
        [Header("Generator")]
        [SerializeField]
        private Generator[] statGenerators;

        private Resource[] _cachedResources;
        private Resource[] cachedResources
        {
            get
            {
                if ((_cachedResources == null) || (_cachedResources.Length == 0))
                {
                    var resList = new List<Resource>();
                    if (baseArchetype != null)
                    {
                        resList.AddRange(baseArchetype.GetResources());
                    }
                    foreach (var r in resources)
                    {
                        var index = resList.FindIndex(x => x.type == r.type);
                        if (index == -1) resList.Add(r);
                        else resList[index] = r;
                    }

                    _cachedResources = resList.ToArray();
                }   

                return _cachedResources;
            }
        }
        private Stat[] _cachedStats;
        private Stat[] cachedStats
        {
            get
            {
                if ((_cachedStats == null) || (_cachedStats.Length == 0))
                {
                    var resList = new List<Stat>();
                    if (baseArchetype != null)
                    {
                        resList.AddRange(baseArchetype.GetStats());
                    }
                    foreach (var s in primaryStats)
                    {
                        var index = resList.FindIndex(x => x.type == s.type);
                        if (index == -1) resList.Add(s);
                        else resList[index] = s;
                    }

                    _cachedStats = resList.ToArray();
                }

                return _cachedStats;
            }
        }

        public bool RunGenerator(StatType type, RPGEntity character, StatInstance statInstance)
        {
            foreach (var g in statGenerators)
            {
                if ((g.type == type) && (g.calculator != null))
                {
                    statInstance.value = g.calculator.GetValue(character);
                    return true;
                }
            }

            return baseArchetype?.RunGenerator(type, character, statInstance) ?? false;
        }

        public void UpdateDerivedStats(RPGEntity entity)
        {
            baseArchetype?.UpdateDerivedStats(entity);

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

        public IEnumerable<Resource> GetResources()
        {
            return cachedResources;
        }

        public IEnumerable<Stat> GetStats()
        {
            return cachedStats;
        }

        public RuntimeAnimatorController GetAnimatorController()
        {
            if (controller != null) return controller;
            
            return baseArchetype?.GetAnimatorController() ?? null;
        }

        public Weapon GetDefaultWeapon()
        {
            if (defaultWeapon != null) return defaultWeapon;
            return baseArchetype?.GetDefaultWeapon() ?? null;
        }

        public SoundDef GetHitSound()
        {
            if (hitSound != null) return hitSound;
            return baseArchetype?.GetHitSound() ?? null;
        }
        public SoundDef GetDeathSound()
        {
            if (deathSound != null) return deathSound;
            return baseArchetype?.GetDeathSound() ?? null;
        }
    }
}
