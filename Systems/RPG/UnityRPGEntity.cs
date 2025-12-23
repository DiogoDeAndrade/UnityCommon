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

        public Faction faction => _rpgEntity.faction;

        protected virtual void Start()
        {
            SetupEntity();

            RegisterEvents(rpgEntity);
        }

        protected virtual void RegisterEvents(RPGEntity entity)
        {
            var resources = entity.GetResources();
            foreach (var res in resources)
            {
                var resInstance = entity.Get(res);
                resInstance.onChange += Entity_onChange;

            }

            health = entity.Get(GlobalsBase.healthResource);
            if (health != null)
            {
                health.onResourceEmpty += Entity_OnDeath;
            }
        }

        protected virtual void UnregisterEvents(RPGEntity entity)
        {
            var resources = entity.GetResources();
            foreach (var res in resources)
            {
                var resInstance = rpgEntity.Get(res);
                resInstance.onChange -= Entity_onChange;
            }
        }

        protected abstract void Entity_OnDeath(ResourceInstance resourceInstance, GameObject changeSource);

        protected abstract void Entity_onChange(ResourceInstance resourceInstance, ChangeData changeData);

        protected virtual void OnDestroy()
        {
            UnregisterEvents(rpgEntity);
            Unregister(_rpgEntity);
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
        public bool HasStat(StatType type) => _rpgEntity.Has(type);

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

        protected virtual void Wait()
        {
            RunActionPerformed();
        }

        public virtual void TriggerAnimation(string triggerName, bool completeAction, Action callback)
        {
            callback?.Invoke();

            if (completeAction) RunActionPerformed();
        }


        protected void RunActionPerformed()
        {
            onActionPerformed?.Invoke(this);
        }

        public virtual float GetInitiative()
        {
            return 0.0f;
        }

        // Get notified by main system that an action should be taken (or not)
        // In normal circumstance, this evaluates and executes an action (but the action is only "done" when RunActionPerformed() is called)
        // enable is used to be able to 'disable' mechanisms for actions (if we're talking about time-sliced systems, or player systems)
        public virtual void RunTurn(bool enable)
        {
            if (!enable) return;

            // Search for TurnDriver and take decisions
            if (!data) return;

            var turnDrivers = data.GetModules<TurnDriver>(true);

            // Remove all disabled modules
            turnDrivers.RemoveAll(m => !m.IsEnabled(this));

            // Precompute priority once, sort, then unwrap
            var tmp = new List<(TurnDriver driver, float priority)>(turnDrivers.Count);

            foreach (var d in turnDrivers)
            {
                tmp.Add((d, d.GetPriority(this)));
            }

            tmp.Sort((a, b) => b.priority.CompareTo(a.priority)); // largest -> smallest

            turnDrivers.Clear();
            turnDrivers.Capacity = Mathf.Max(turnDrivers.Capacity, tmp.Count);
            for (int i = 0; i < tmp.Count; i++)
            {
                turnDrivers.Add(tmp[i].driver);
            }

            foreach (var d in turnDrivers)
            {
                if (d.Execute(this))
                {
                    // Done, found an action I can execute
                    return;
                }
            }

            // Run wait, no action could be executed
            Wait();
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

        public static IEnumerable<KeyValuePair<RPGEntity, UnityRPGEntity>> GetEntities()
        {
            foreach (var entity in entityCache)
            {
                yield return entity;
            }
        }

        #endregion
    }
}