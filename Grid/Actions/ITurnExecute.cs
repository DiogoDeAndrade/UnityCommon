using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ITurnExecute
{
    public void ExecuteTurn();

    public static void ExecuteAllTurns()
    {
        var allMonoBehaviours = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        // Execute turns
        foreach (var monoBehaviour in allMonoBehaviours)
        {
            var turnExecutor = monoBehaviour as ITurnExecute;
            if (turnExecutor != null)
            {
                turnExecutor.ExecuteTurn();
            }
        }
    }
}
