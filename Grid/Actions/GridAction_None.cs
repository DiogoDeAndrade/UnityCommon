using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;

public class GridAction_None : GridAction
{
    private void Start()
    {
    }

    protected override void ActualGatherActions(GridObject subject, Vector2Int position, List<GridAction> retActions)
    {
        retActions.Add(this);
    }

    protected override bool ActualRunAction(GridObject subject, Vector2Int position)
    {
        return true;
    }
}
