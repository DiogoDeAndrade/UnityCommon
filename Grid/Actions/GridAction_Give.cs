using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UC.RPG;

namespace UC
{

    public class GridAction_Give : GridActionContainer
    {
        [SerializeField, Header("Give"), ShowIf(nameof(notResourceType))]
        private Item item;
        [SerializeField, Header("Give"), ShowIf(nameof(notItem))]
        private ResourceType resourceType;
        [SerializeField, ShowIf(nameof(hasResourceType))]
        private float quantity;

        bool notResourceType => resourceType == null;
        bool hasResourceType => resourceType != null;
        bool notItem => item == null;

        public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
        {
            if (hasResourceType)
            {
                var thisHandler = this.FindResourceHandler(resourceType);
                if (thisHandler == null) return;
                if (thisHandler.normalizedResource >= 1.0f) return;

                var thatHandler = subject.FindResourceHandler(resourceType);
                if (thatHandler == null) return;
                if (thatHandler.resource < quantity) return;

                retActions.Add(new NamedAction
                {
                    name = verb,
                    action = RunAction,
                    container = this
                });
            }
            else if (item != null)
            {
                var thisInventory = GetComponent<Inventory>();
                if (thisInventory == null) return;
                var thatInventory = subject.GetComponent<Inventory>();
                if (thatInventory == null) return;
                if (thatInventory.GetItemCount(item) < quantity) return;

                retActions.Add(new NamedAction
                {
                    name = verb,
                    action = RunAction,
                    container = this
                });
            }
        }

        protected bool RunAction(GridObject subject, Vector2Int position)
        {
            if (hasResourceType)
            {
                var thisHandler = this.FindResourceHandler(resourceType);
                var thatHandler = subject.FindResourceHandler(resourceType);

                thisHandler.Change(new ChangeData { deltaValue = quantity, changeSrcPosition = subject.transform.position, source = subject.gameObject }, true);
                thatHandler.Change(new ChangeData { deltaValue = -quantity, changeSrcPosition = transform.position, source = gameObject }, true);

                return true;
            }
            else if (item != null)
            {
                var thisInventory = GetComponent<Inventory>();
                var thatInventory = subject.GetComponent<Inventory>();

                thisInventory.Add(item, (int)quantity);
                thatInventory.Remove(item, (int)quantity);

                return true;
            }

            return false;
        }
    }
}