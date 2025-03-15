using System.Collections.Generic;
using UnityEngine;

public class GridAction_Teleport : GridAction
{
    [SerializeField] 
    private Hypertag    objectToTeleportTag;
    [SerializeField] 
    private Hypertag    targetLocationTag;

    public override void GatherActions(GridObject subject, Vector2Int position, List<GridAction> actions)
    {
        if (objectToTeleportTag)
        {
            var targetObj = Hypertag.FindFirstObjectWithHypertag<GridObject>(objectToTeleportTag);
            if (targetObj == null) return;
        }
        if (targetLocationTag)
        {
            var targetPos = Hypertag.FindFirstObjectWithHypertag<Transform>(targetLocationTag);
            if (targetPos == null) return;

            actions.Add(this);
        }
    }

    public override bool RunAction(GridObject subject, Vector2Int position)
    {
        GridObject targetObj = null;
        if (objectToTeleportTag)
        {
            targetObj = Hypertag.FindFirstObjectWithHypertag<GridObject>(objectToTeleportTag);
            if (targetObj == null) return false;
        }
        else
        {
            targetObj = subject;
        }

        if (targetLocationTag)
        {
            var targetPos = Hypertag.FindFirstObjectWithHypertag<Transform>(targetLocationTag);
            if (targetPos == null) return false;

            targetObj.TeleportTo(targetPos.transform.position);
        }

        return false;
    }
}
