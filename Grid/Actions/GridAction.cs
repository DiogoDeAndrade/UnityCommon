using System.Runtime.InteropServices;
using UnityEngine;

public abstract class GridAction : MonoBehaviour
{
    [SerializeField, Header("Action")] 
    private string _verb;

    public string verb => _verb;

    public abstract bool CanRunAction(GridObject subject);

    public abstract bool RunAction(GridObject subject);

}
