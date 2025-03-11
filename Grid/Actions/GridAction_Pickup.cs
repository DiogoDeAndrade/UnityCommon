using NaughtyAttributes;
using System;
using UnityEngine;

public class GridAction_Pickup : GridAction
{
    private enum PickupType { Single, Limited, Infinite };

    [SerializeField, Header("Pickup")]
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
        
        return true;
    }

    public override bool RunAction(GridObject subject)
    {
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
}
