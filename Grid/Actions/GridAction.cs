using NaughtyAttributes;
using System.Runtime.InteropServices;
using UnityEngine;

public abstract class GridAction : MonoBehaviour
{
    [SerializeField, Header("Action")] 
    protected string  _verb;
    [SerializeField]
    protected bool    combatText = false;
    [SerializeField, ShowIf(nameof(combatText))]
    protected Color   combatTextColor = Color.white;

    public string verb => _verb;

    public abstract bool CanRunAction(GridObject subject);

    public abstract bool RunAction(GridObject subject);

}
