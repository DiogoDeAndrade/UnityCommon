using UnityEngine;
using UC.Interaction;

namespace UC.RPG
{
    public abstract class UnityRPGEntity : MonoBehaviour
    {
        public delegate void OnActionPerformed(UnityRPGEntity entity);
        public event OnActionPerformed onActionPerformed;

        [SerializeField] protected Archetype        _archetype;
        [SerializeField] protected int              _level = 1;

        protected RPGEntity         _rpgEntity;
        protected ResourceInstance  health;

        public Archetype archetype => _archetype;
        public RPGEntity rpgEntity => _rpgEntity;
        public bool isDead => rpgEntity.isDead;

        protected virtual void Start()
        {
            SetupEntity();

            foreach (var r in archetype.GetResources())
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

        protected abstract void Entity_OnDeath(GameObject changeSource);

        protected abstract void Entity_onChange(ResourceType resourceType, ChangeData changeData);

        protected abstract void OnDestroy();

        protected virtual void SetupEntity()
        {
            if (_archetype == null)
            {
                Debug.Log($"Can't setup character {name}: No archetype defined!");
                return;
            }

            _rpgEntity = new RPGEntity(_level, _archetype);
            _rpgEntity.Init();

            foreach (var r in _archetype.GetResources())
            {
                var handler = gameObject.AddComponent<ResourceHandler>();
                handler.instance = _rpgEntity.Get(r.type);
            }

            if (archetype.hasInventory)
            {          
                var invHandler = gameObject.AddComponent<Inventory>();
                invHandler.instance = _rpgEntity.inventory;
            }

            if (archetype.hasEquipment)
            {
                var equipmentHandler = gameObject.AddComponent<Equipment>();
                equipmentHandler.instance = _rpgEntity.equipment;
            }
        }

        public float GetStat(StatType type) => _rpgEntity.Get(type).value;
        public InventoryInstance GetInventory() => _rpgEntity.inventory;
        public EquipmentInstance GetEquipment() => _rpgEntity.equipment;

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
