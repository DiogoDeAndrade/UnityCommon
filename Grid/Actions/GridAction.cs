using NaughtyAttributes;
using System.Runtime.InteropServices;
using UnityEngine;

public abstract class GridAction : MonoBehaviour
{
    [SerializeField, Header("Action")] 
    protected string  _verb;
    [SerializeField]
    protected bool    enableCombatText = false;
    [SerializeField, ShowIf(nameof(enableCombatText))]
    protected string  combatText = "";
    [SerializeField, ShowIf(nameof(enableCombatText))]
    protected Color   combatTextColor = Color.white;

    public string verb => _verb;

    public abstract bool CanRunAction(GridObject subject, Vector2Int position);

    public abstract bool RunAction(GridObject subject, Vector2Int position);

}
