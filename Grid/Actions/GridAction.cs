using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

public abstract class GridAction : MonoBehaviour
{
    [SerializeField]
    private List<GridActionCondition> conditions;
    [SerializeField, Header("Action")]
    protected string _verb;
    [SerializeField]
    protected bool    enableCombatText = false;
    [SerializeField, ShowIf(nameof(enableCombatText))]
    protected string  combatText = "";
    [SerializeField, ShowIf(nameof(enableCombatText))]
    protected Color   combatTextColor = Color.white;
    [SerializeField, ShowIf(nameof(conditionsHaveItems))]
    protected bool    consumeItems;
    [SerializeField]
    protected AudioClip actionSnd;

    public string verb => _verb;
    
    bool conditionsHaveItems
    {
        get
        {
            if (conditions == null) return false;
            if (conditions.Count == 0) return false;
            foreach (var cond in conditions)
            {
                if (cond.GetItem().item != null) return true;
            }

            return false;
        }
    }

    protected UCExpression.IContext context;
    protected GridSystem            gridSystem;

    protected virtual void Start()
    {
        context = InterfaceHelpers.GetFirstInterfaceComponent<UCExpression.IContext>();
        gridSystem = GetComponentInParent<GridSystem>();
    }

    public void GatherActions(GridObject subject, Vector2Int position, List<GridAction> actions)
    {
        if ((conditions != null) && (conditions.Count > 0))
        {
            foreach (var condition in conditions)
            {
                if (!condition.CheckCondition(context)) return;
            }
        }

        ActualGatherActions(subject, position, actions);
    }

    protected abstract void ActualGatherActions(GridObject subject, Vector2Int position, List<GridAction> actions);

    public bool RunAction(GridObject subject, Vector2Int position)
    {
        if (ActualRunAction(subject, position))
        {
            if (enableCombatText)
            {
                CombatTextManager.SpawnText(subject.gameObject, combatText, combatTextColor, combatTextColor.ChangeAlpha(0.0f), 1.0f, 1.0f);
            }

            if (actionSnd)
            {
                SoundManager.PlaySound(SoundType.PrimaryFX, actionSnd);
            }

            if (consumeItems)
            {
                if ((conditions != null) && (conditions.Count > 0))
                {
                    foreach (var condition in conditions)
                    {
                        (var item, var count) = condition.GetItem();
                        if (item)
                        {
                            var inventory = condition.GetInventory();

                            inventory?.Remove(item, count);
                        }
                    }
                }
            }

            return true;
        }

        return false;
    }

    protected abstract bool ActualRunAction(GridObject subject, Vector2Int position);

    public virtual bool ShouldRunTurn() { return true; }

    public bool hasSound => actionSnd != null;

}
