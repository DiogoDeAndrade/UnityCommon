using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{
    public class RPGEntity : IRPGOwner
    {
        public int          level;
        public Archetype    archetype;
        public Item         item;

        protected Dictionary<StatType, StatInstance>            stats;
        protected Dictionary<ResourceType, ResourceInstance>    resources;

        private ResourceInstance        healthRes;
        private InventoryRPGInstance    _inventory;
        private EquipmentRPGInstance    _equipment;

        public InventoryRPGInstance     inventory => _inventory;
        public EquipmentRPGInstance     equipment => _equipment;

        public bool isDead => (healthRes != null) && (healthRes.isResourceEmpty);

        public ModularScriptableObject  data
        {
            get
            {
                if (archetype) return archetype;
                if (item) return item;

                return null;
            }
        }

        public RPGEntity(int level, Archetype archetype)
        {
            this.level = level;
            this.archetype = archetype;
        }

        public RPGEntity(Item item)
        {
            this.level = item.level;
            this.item = item;
        }

        public void AddInventory(InventoryRPGInstance inventoryInstance)
        {
            _inventory = inventoryInstance;
        }

        public void AddInventory(bool limited = false, int maxSlots = 9)
        {
            _inventory = new InventoryRPGInstance(limited, maxSlots);
        }

        public void Init()
        {
            var inventoryModule = data.GetModule<RPGInventoryModule>(true);
            if (inventoryModule)
            {
                if (inventoryModule.hasInventory)
                {
                    AddInventory(inventoryModule.inventorySize != -1, inventoryModule.inventorySize);

                    var items = inventoryModule.defaultInventory;
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            RPGEntity itemEntity = new RPGEntity(item);
                            itemEntity.Init();

                            _inventory.Add(itemEntity);
                        }
                    }
                }
                if (inventoryModule.hasEquipment)
                {
                    _equipment = new EquipmentRPGInstance();
                    foreach (var slot in inventoryModule.availableEquipmentSlots)
                    {
                        _equipment.AddSlot(slot);
                    }

                    var items = inventoryModule.defaultEquipment;
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var itemEntity = new RPGEntity(item.item);
                            itemEntity.Init();

                            _equipment.Equip(item.slot, itemEntity);
                        }
                    }
                }
            }

            stats = new();
            if (data)
            {
                foreach (var s in data.GetModules<RPGStatModule>(true))
                {
                    var statInstance = new StatInstance(s.type);

                    var generator = data.GetModule<RPGStatGenerator>(true);
                    if (generator != null)
                        generator.RunGenerator(s.type, this, statInstance);

                    stats.Add(s.type, statInstance);
                }

                if (archetype)
                    archetype.UpdateDerivedStats(this);
                else if (item)
                    item.UpdateDerivedStats(this);

                resources = new();
                foreach (var r in data.GetModules<RPGResourceModule>(true))
                {
                    var res = new ResourceInstance(r.type, this);
                    res.maxValue = r.calculator.GetValue(this);
                    resources.Add(r.type, res);

                    if (r.type == GlobalsBase.healthResource) healthRes = res;
                }
            }

            // Register all events necessary for this entity
            var events = data.GetModules<RPGEvent>(true);
            foreach (var  evt in events)
            {
                evt.Init(this);
            }
        }

        public StatInstance Get(StatType s)
        {
            if (stats == null) return null;
            if (stats.TryGetValue(s, out var instance)) return instance;

            return null;
        }

        public ResourceInstance Get(ResourceType r)
        {
            if (resources == null) return null;
            if (resources.TryGetValue(r, out var instance)) return instance;

            return null;
        }

        public bool DefaultAttack(Vector2Int destPos)
        {
            // Get weapon
            RPGEntity weapon = null;
            if (equipment != null)
            {
                var entity = equipment.GetItem(Globals.defaultWeaponSlot);
                if (entity != null) weapon = entity;
            }

            var inventoryModule = archetype.GetModule<RPGInventoryModule>(true);

            if (weapon == null)
            {
                weapon = new RPGEntity(inventoryModule.unnarmedWeapon);
                weapon.Init();
            }
            if (weapon != null)
            {
                return Attack(weapon, this, destPos);
            }

            return false;
        }

        public bool Attack(RPGEntity weapon, RPGEntity source, Vector2Int destPos)
        {
            var weaponModule = weapon.item.GetModule<RPGItemWeapon>(true);
            var attackModule = weaponModule?.attackModule ?? null;
            return attackModule?.Attack(weapon, source, destPos) ?? false;
        }
    }
}
