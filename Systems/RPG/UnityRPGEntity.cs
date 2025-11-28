using NaughtyAttributes;
using UC.Interaction;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

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

        public Archetype archetype => _archetype;
        public RPGEntity rpgEntity => _rpgEntity;
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

            if (archetype)
            {
                foreach (var r in archetype.GetModules<RPGResourceModule>(true))
                {
                    var res = rpgEntity.Get(r.type);
                    res.onChange += (changeData) =>
                    {
                        Entity_onChange(r.type, changeData);
                    };
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

        protected abstract void Entity_OnDeath(GameObject changeSource);

        protected abstract void Entity_onChange(ResourceType resourceType, ChangeData changeData);

        protected abstract void OnDestroy();

        protected virtual void SetupEntity()
        {
            if ((_archetype == null) && (_item == null))
            {
                Debug.Log($"Can't setup entity {name}: No archetype or item defined!");
                return;
            }

            if (isCharacter)
                _rpgEntity = new RPGEntity(_level, _archetype);
            else
                _rpgEntity = new RPGEntity(_item);
            _rpgEntity.Init();

            if (_archetype)
            {
                foreach (var r in _archetype.GetModules<RPGResourceModule>(true))
                {
                    var handler = gameObject.AddComponent<ResourceHandler>();
                    handler.instance = _rpgEntity.Get(r.type);
                }

                var inventoryModule = archetype.GetModule<RPGInventoryModule>(true);

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

        public float GetStat(StatType type) => _rpgEntity.Get(type).GetValue();
        public InventoryRPGInstance GetInventory() => _rpgEntity.inventory;
        public EquipmentRPGInstance GetEquipment() => _rpgEntity.equipment;

        public bool RunAction(Interactable interactable)
        {
            if (interactable.Interact(gameObject, interactable.referenceObject, this))
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
    }
}
