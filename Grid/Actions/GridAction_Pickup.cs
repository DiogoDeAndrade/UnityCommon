using NaughtyAttributes;
using UnityEngine;

public class GridAction_Pickup : GridAction
{
    private enum PickupType { Single, Limited, Infinite };

    [SerializeField, Header("Pickup")]
    private Item        item;
    [SerializeField]
    private PickupType  type = PickupType.Single;
    [SerializeField, ShowIf(nameof(needCharges))]
    private int         maxCharges = 3;
    [SerializeField, ShowIf(nameof(canDestroy))]
    private bool        destroyOnNullCharges = true;

    bool needCharges => type == PickupType.Limited;
    bool canDestroy => type != PickupType.Infinite;

    int charges = 0;

    private void Start()
    {
        charges = (type == PickupType.Single) ? (1) : (maxCharges);
    }

    public override bool CanRunAction(GridObject subject)
    {
        if (charges <= 0) return false;
        if (subject.GetComponent<Inventory>() == null) return false;

        return true;
    }

    public override bool RunAction(GridObject subject)
    {
        var inventory = subject.GetComponent<Inventory>();
        if (!inventory.Add(item))
        {
            // Couldn't pickup, probably no space
            return false;
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

        if (combatText)
        {
            CombatTextManager.SpawnText(subject.gameObject, new Vector2(0.0f, subject.cellSize.y * 0.5f), $"Picked up {item.displayName.ToLower()}", combatTextColor, combatTextColor.ChangeAlpha(0.0f), 1.0f, 1.0f);
        }

        return true;
    }
}
