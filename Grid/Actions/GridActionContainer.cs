using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

public abstract class GridActionContainer : MonoBehaviour
{
    public delegate bool ActualRunAction(GridObject subject, Vector2Int position);
    public struct NamedAction
    {
        public string               name;
        public ActualRunAction      action;
        public GridActionContainer  container;
        public bool?                combatTextEnable;
        public string               combatText;
        public Color?               combatTextColor;

        string GetCombatText() => (string.IsNullOrEmpty(combatText)) ? (container.combatText) : (combatText);
        Color GetCombatTextColor() => (!combatTextColor.HasValue) ? (container.combatTextColor) : (combatTextColor.Value);
        bool isCombatTextEnable => (!combatTextEnable.HasValue) ? (container.enableCombatText) : (combatTextEnable.Value);

        internal bool Run(GridObject subject, Vector2Int position)
        {
            if (action(subject, position))
            {
                if (isCombatTextEnable)
                {
                    var c = GetCombatTextColor();
                    CombatTextManager.SpawnText(subject.gameObject, GetCombatText(), c, c.ChangeAlpha(0.0f), 1.0f, 1.0f);
                }

                if (container.actionSnd)
                {
                    SoundManager.PlaySound(SoundType.PrimaryFX, container.actionSnd);
                }

                if (container.consumeItems)
                {
                    if ((container.conditions != null) && (container.conditions.Count > 0))
                    {
                        foreach (var condition in container.conditions)
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
    };

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

    public void GatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
    {
        if ((conditions != null) && (conditions.Count > 0))
        {
            foreach (var condition in conditions)
            {
                if (!condition.CheckCondition(context)) return;
            }
        }

        ActualGatherActions(subject, position, retActions);
    }

    public abstract void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions);

    public virtual bool ShouldRunTurn() { return true; }

    public bool hasSound => actionSnd != null;

}
