using JetBrains.Annotations;
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

        private ResourceInstance        healthRes;
        private InventoryRPGInstance    _inventory;
        private EquipmentRPGInstance    _equipment;

        public InventoryRPGInstance     inventory => _inventory;
        public EquipmentRPGInstance     equipment => _equipment;

        public bool isDead => healthRes.isResourceEmpty;

        public RPGEntity(int level, Archetype archetype)
        {
            this.level = level;
            this.archetype = archetype;

            var inventoryModule = archetype.GetModule<RPGInventoryModule>(true);
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
            stats = new();
            if (archetype)
            {
                foreach (var s in archetype.GetModules<RPGStatModule>(true))
                {
                    var statInstance = new StatInstance(s.type);

                    var generator = archetype.GetModule<RPGStatGenerator>(true);
                    if (generator != null)
                        generator.RunGenerator(s.type, this, statInstance);

                    stats.Add(s.type, statInstance);
                }

                archetype.UpdateDerivedStats(this);
            }

            if (archetype)
            {
                resources = new();
                foreach (var r in archetype.GetModules<RPGResourceModule>(true))
                {
                    var res = new ResourceInstance(r.type);
                    res.maxValue = r.calculator.GetValue(this);
                    resources.Add(r.type, res);

                    if (r.type == GlobalsBase.healthResource) healthRes = res;
                }
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
            Item weapon = null;
            if (equipment != null)
            {
                var entity = equipment.GetItem(Globals.defaultWeaponSlot);
                if (entity != null) weapon = entity.item;
            }

            var inventoryModule = archetype.GetModule<RPGInventoryModule>(true);

            if (weapon == null) weapon = inventoryModule.unnarmedWeapon;
            if (weapon)
            {
                return Attack(weapon, this, destPos);
            }

            return false;
        }

        public bool Attack(Item weapon, RPGEntity source, Vector2Int destPos)
        {
            var weaponModule = weapon.GetModule<RPGItemWeapon>(true);
            var attackModule = weaponModule?.attackModule ?? null;
            return attackModule?.Attack(weapon, source, destPos) ?? false;
        }
    }
}
