using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{
    public class RPGEntity
    {
        public int          level;
        public Archetype    archetype;
        public Item         item;

        protected Dictionary<StatType, StatInstance>            stats;
        protected Dictionary<ResourceType, ResourceInstance>    resources;

        private ResourceInstance healthRes;

        public bool isDead => healthRes.isResourceEmpty;

        public RPGEntity(int level, Archetype archetype)
        {
            this.level = level;
            this.archetype = archetype;
        }

        public void Init()
        {
            stats = new();
            foreach (var s in archetype.GetStats())
            {
                var statInstance = new StatInstance(s.type);

                archetype.RunGenerator(s.type, this, statInstance);

                stats.Add(s.type, statInstance);
            }

            archetype.UpdateDerivedStats(this);

            resources = new();
            foreach (var r in archetype.GetResources())
            {
                var res = new ResourceInstance(r.type);
                res.maxValue = r.calculator.GetValue(this);
                resources.Add(r.type, res);

                if (r.type == Globals.healthResource) healthRes = res;
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
            var weapon = archetype.GetDefaultWeapon();
            if (weapon)
            {
                return Attack(weapon, this, destPos);
            }

            return false;
        }

        public bool Attack(Weapon weapon, RPGEntity source, Vector2Int destPos)
        {
            return weapon.GetAttackModule()?.Attack(weapon, source, destPos) ?? false;
        }
    }
}
