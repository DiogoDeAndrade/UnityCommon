using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{
    public abstract class UnityRPGEntity : MonoBehaviour
    {
        public delegate void OnActionPerformed(UnityRPGEntity entity);
        public event OnActionPerformed onActionPerformed;

        [SerializeField, ShowIf(nameof(shouldDisplayArchetype))] 
        protected Archetype        _archetype;
        [SerializeField, ShowIf(nameof(shouldDisplayItem))] 
        protected Item              _item;
        [SerializeField] 
        protected int              _level = 1;

        protected RPGEntity         _rpgEntity;
        protected ResourceInstance  health;

        public Archetype                archetype => _archetype;
        public RPGEntity                rpgEntity => _rpgEntity;
        public ModularScriptableObject  data
        {
            get
            {
                if (archetype) return archetype;
                if (_item) return _item;

                return null;
            }
        }

        public bool isDead => rpgEntity.isDead;
        public int level => _level;

        public bool isCharacter => archetype != null;
        public bool isNotCharacter => !isCharacter;
        public bool isItem => _item != null;
        public bool isNotItem => !isItem;
        public bool shouldDisplayArchetype => (_archetype != null) || ((_archetype == null) && (_item == null));
        public bool shouldDisplayItem => (_item != null) || ((_archetype == null) && (_item == null));

        protected virtual void Start()
        {
            SetupEntity();

            if (data)
            {
                foreach (var r in data.GetModules<RPGResourceModule>(true))
                {
                    var res = rpgEntity.Get(r.type);
                    res.onChange += Entity_onChange;
                        
                    if (r.type == GlobalsBase.healthResource)
                    {
                        health = res;
                    }
                }

                if (health != null)
                {
                    health.onResourceEmpty += Entity_OnDeath;
                }
            }
        }

        protected abstract void Entity_OnDeath(ResourceInstance resourceInstance, GameObject changeSource);

        protected abstract void Entity_onChange(ResourceInstance resourceInstance, ChangeData changeData);

        protected virtual void OnDestroy()
        {
            if (data)
            {
                foreach (var r in data.GetModules<RPGResourceModule>(true))
                {
                    var res = rpgEntity.Get(r.type);
                    res.onChange -= Entity_onChange;
                }

                if (health != null)
                {
                    health.onResourceEmpty -= Entity_OnDeath;
                }
            }
        }

        protected virtual void SetupEntity()
        {
            if (!data)
            {
                Debug.Log($"Can't setup entity {name}: No archetype or item defined!");
                return;
            }

            if (isCharacter)
                _rpgEntity = new RPGEntity(_level, _archetype);
            else
                _rpgEntity = new RPGEntity(_item);
            _rpgEntity.Init();

            Register(_rpgEntity, this);

            if (data)
            {
                foreach (var r in data.GetModules<RPGResourceModule>(true))
                {
                    var handler = gameObject.AddComponent<ResourceHandler>();
                    handler.instance = _rpgEntity.Get(r.type);
                }

                var inventoryModule = data.GetModule<RPGInventoryModule>(true);
                if (inventoryModule)
                {
                    if (inventoryModule.hasInventory)
                    {
                        var invHandler = gameObject.AddComponent<InventoryRPG>();
                        invHandler.instance = _rpgEntity.inventory;
                    }

                    if (inventoryModule.hasEquipment)
                    {
                        var equipmentHandler = gameObject.AddComponent<EquipmentRPG>();
                        equipmentHandler.instance = _rpgEntity.equipment;
                    }
                }
            }
        }

        public float GetStat(StatType type) => _rpgEntity.Get(type).GetValue();

        public StatInstance GetStatInstance(StatType type) => _rpgEntity.Get(type);
        public ResourceInstance GetResourceInstance(ResourceType type) => _rpgEntity.Get(type);

        public InventoryRPGInstance GetInventory() => _rpgEntity.inventory;
        public EquipmentRPGInstance GetEquipment() => _rpgEntity.equipment;

        public bool RunAction(Interactable interactable)
        {
            var context = new ActionContext
            {
                triggerGameObject = gameObject,
                triggerEntity = rpgEntity,
                targetGameObject = interactable.referenceObject
            };

            if (interactable.Interact(context, this))
            {
                RunActionPerformed();

                return true;
            }
            return false;
        }

        protected void RunActionPerformed()
        {
            onActionPerformed?.Invoke(this);
        }

        #region Static management of UnityRPGEntity

        private static RPGEntity masterEntity = new();
        private static Dictionary<RPGEntity, UnityRPGEntity> entityCache = new();

        public static void Register(RPGEntity entity, UnityRPGEntity unity)
        {
            entityCache[entity] = unity;            
            masterEntity.AddChild(entity);
        }

        public static void Unregister(RPGEntity entity)
        {
            if (entity == null) return;

            if (entityCache.ContainsKey(entity))
            {
                entityCache.Remove(entity);
                masterEntity.RemoveChild(entity);
            }
        }

        public static UnityRPGEntity GetEntity(RPGEntity source)
        {
            if (entityCache.TryGetValue(source, out var entity))
            {
                return entity;
            }

            return null;
        }

        internal static IEnumerable<KeyValuePair<RPGEntity, UnityRPGEntity>> GetEntities()
        {
            foreach (var entity in entityCache)
            {
                yield return entity;
            }
        }

        #endregion
    }
}