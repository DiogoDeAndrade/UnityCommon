using NaughtyAttributes;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;

public class GridAction_Pickup : GridAction
{
    private enum PickupType { Single, Limited, Infinite };

    [SerializeField, Header("Pickup"), ShowIf(nameof(notResourceType))]
    private Item            item;
    [SerializeField, ShowIf(nameof(notItem))]
    private ResourceType    resourceType;
    [SerializeField, ShowIf(nameof(hasResourceType))]
    private float           quantity;
    [SerializeField]
    private PickupType  type = PickupType.Single;
    [SerializeField, ShowIf(nameof(needCharges))]
    private int         maxCharges = 3;
    [SerializeField, ShowIf(nameof(canDestroy))]
    private bool        destroyOnNullCharges = true;

    bool needCharges => type == PickupType.Limited;
    bool canDestroy => type != PickupType.Infinite;
    bool notItem => item == null;
    bool notResourceType => resourceType == null;
    bool hasResourceType => resourceType != null;

    int charges = 0;

    private void Start()
    {
        charges = (type == PickupType.Single) ? (1) : (maxCharges);
    }

    public override bool CanRunAction(GridObject subject, Vector2Int position)
    {
        if ((charges <= 0) && (type != PickupType.Infinite)) return false;
        if (item)
        {
            if (subject.GetComponent<Inventory>() == null) return false;
        }
        if (resourceType)
        {
            var resHandler = subject.FindResourceHandler(resourceType);
            if (resHandler == null) return false;
            if (resHandler.normalizedResource >= 1.0f) return false;
        }

        return true;
    }

    public override bool RunAction(GridObject subject, Vector2Int position)
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

        if (enableCombatText)
        {
            CombatTextManager.SpawnText(subject.gameObject, combatText, combatTextColor, combatTextColor.ChangeAlpha(0.0f), 1.0f, 1.0f);
        }

        return true;
    }
}
