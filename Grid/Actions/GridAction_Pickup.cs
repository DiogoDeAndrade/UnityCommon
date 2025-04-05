using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class GridAction_Pickup : GridActionContainer
    {
        private enum PickupType { Single, Limited, Infinite };

        [SerializeField, Header("Pickup"), ShowIf(nameof(notResourceType))]
        private Item item;
        [SerializeField, ShowIf(nameof(notItem)), Header("Pickup")]
        private ResourceType resourceType;
        [SerializeField, ShowIf(nameof(hasResourceType))]
        private float quantity;
        [SerializeField]
        private PickupType type = PickupType.Single;
        [SerializeField, ShowIf(nameof(needCharges))]
        private int maxCharges = 3;
        [SerializeField, ShowIf(nameof(canDestroy))]
        private bool destroyOnNullCharges = true;

        bool needCharges => type == PickupType.Limited;
        bool canDestroy => type != PickupType.Infinite;
        bool notItem => item == null;
        bool notResourceType => resourceType == null;
        bool hasResourceType => resourceType != null;

        int charges = 0;

        protected override void Start()
        {
            base.Start();
            charges = (type == PickupType.Single) ? (1) : (maxCharges);
        }

        public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
        {
            if ((charges <= 0) && (type != PickupType.Infinite)) return;
            if (item)
            {
                if (subject.GetComponent<Inventory>() == null) return;
            }
            if (resourceType)
            {
                var resHandler = subject.FindResourceHandler(resourceType);
                if (resHandler == null) return;
                if (!resHandler.enabled) return;
                if (resHandler.normalizedResource >= 1.0f) return;
            }

            retActions.Add(new NamedAction
            {
                name = verb,
                action = RunAction,
                container = this
            });
        }

        protected bool RunAction(GridObject subject, Vector2Int position)
        {
            if (item)
            {
                var inventory = subject.GetComponent<Inventory>();
                if (!inventory.Add(item))
                {
                    // Couldn't pickup, probably no space
                    return false;
                }
            }
            else if (resourceType)
            {
                var resHandler = subject.FindResourceHandler(resourceType);
                if (resHandler == null) return false;
                resHandler.Change(ResourceHandler.ChangeType.Burst, quantity, transform.position, Vector3.zero, gameObject, true);
            }

            switch (type)
            {
                case PickupType.Single:
                    if (destroyOnNullCharges) Destroy(gameObject);
                    break;
                case PickupType.Limited:
                    charges--;
                    if ((charges <= 0) && (destroyOnNullCharges)) Destroy(gameObject);
                    break;
                case PickupType.Infinite:
                    break;
                default:
                    break;
            }

            return true;
        }

        internal void SetItem(Item item)
        {
            _verb = "Pickup " + item.displayName;
            this.item = item;
        }
    }
}