using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;

public class GridAction_None : GridAction
{
    private void Start()
    {
    }

    public override void GatherActions(GridObject subject, Vector2Int position, List<GridAction> retActions)
    {
        retActions.Add(this);
    }

    public override bool RunAction(GridObject subject, Vector2Int position)
    {
        if (enableCombatText)
        {
            CombatTextManager.SpawnText(subject.gameObject, combatText, combatTextColor, combatTextColor.ChangeAlpha(0.0f), 1.0f, 1.0f);
        }

        return true;
    }
}
