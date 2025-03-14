using NaughtyAttributes;
using UnityEditorInternal.Profiling.Memory.Experimental;
using UnityEngine;

public class GridAction_None : GridAction
{
    private void Start()
    {
    }

    public override bool CanRunAction(GridObject subject, Vector2Int position)
    {
        return true;
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
