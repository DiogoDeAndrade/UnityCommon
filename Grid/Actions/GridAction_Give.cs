using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;

public class GridAction_Give : GridAction
{
    [SerializeField, Header("Give"), ShowIf(nameof(notResourceType))]
    private Item            item;
    [SerializeField, ShowIf(nameof(notItem))]
    private ResourceType    resourceType;
    [SerializeField, ShowIf(nameof(hasResourceType))]
    private float           quantity;

    bool notResourceType => resourceType == null;
    bool hasResourceType => resourceType != null;
    bool notItem => item == null;

    protected override void ActualGatherActions(GridObject subject, Vector2Int position, List<GridAction> actions)
    {
        if (hasResourceType)
        {
            var thisHandler = this.FindResourceHandler(resourceType);
            if (thisHandler == null) return;
            if (thisHandler.normalizedResource >= 1.0f) return;

            var thatHandler = subject.FindResourceHandler(resourceType);
            if (thatHandler == null) return;
            if (thatHandler.resource < quantity) return;

            actions.Add(this);
        }
        else if (item != null)
        {
            var thisInventory = GetComponent<Inventory>();
            if (thisInventory == null) return;
            var thatInventory = subject.GetComponent<Inventory>();
            if (thatInventory == null) return;
            if (thatInventory.GetItemCount(item) < quantity) return;

            actions.Add(this);
        }
    }

    protected override bool ActualRunAction(GridObject subject, Vector2Int position)
    {
        if (hasResourceType)
        {
            var thisHandler = this.FindResourceHandler(resourceType);
            var thatHandler = subject.FindResourceHandler(resourceType);

            thisHandler.Change(ResourceHandler.ChangeType.Burst, quantity, subject.transform.position, Vector3.zero, subject.gameObject, true);
            thatHandler.Change(ResourceHandler.ChangeType.Burst, -quantity, transform.position, Vector3.zero, gameObject, true);

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
