using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{
    public class RPGEntity
    {
        public int          level;
        public Archetype    archetype;

        protected Dictionary<StatType, StatInstance>            stats;
        protected Dictionary<ResourceType, ResourceInstance>    resources;

        public RPGEntity(int level, Archetype archetype)
        {
            this.level = level;
            this.archetype = archetype;
        }

        public void Init()
        {
            stats = new();
            foreach (var s in archetype.primaryStats)
            {
                var statInstance = new StatInstance(s.type);

                archetype.RunGenerator(s.type, this, statInstance);

                stats.Add(s.type, statInstance);
            }

            resources = new();
            foreach (var r in archetype.resources)
            {
                var res = new ResourceInstance(r.type);
                res.maxValue = r.calculator.GetValue(this);
                resources.Add(r.type, res);
            }
        }

        public StatInstance Get(StatType s)
        {
            if (stats.TryGetValue(s, out var instance)) return instance;

            return null;
        }

        public ResourceInstance Get(ResourceType r)
        {
            if (resources.TryGetValue(r, out var instance)) return instance;

            return null;
        }

        public bool DefaultAttack(Vector2Int destPos)
        {
            // Get weapon
            var weapon = archetype.defaultWeapon;
            if (weapon)
            {
                return Attack(weapon, this, destPos);
            }

            return false;
        }

        public bool Attack(Weapon weapon, RPGEntity source, Vector2Int destPos)
        {
            return weapon.attackModule.Attack(source, destPos);
        }
    }
}
