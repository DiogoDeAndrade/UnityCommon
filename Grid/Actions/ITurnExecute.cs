using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ITurnExecute
{
    public int GetExecutionOrder() { return 0; }
    public void ExecuteTurn();

    public static void ExecuteAllTurns()
    {
        var allMonoBehaviours = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var turnExecutors = new List<ITurnExecute>();

        // Collect all ITurnExecute instances
        foreach (var monoBehaviour in allMonoBehaviours)
        {
            if ((monoBehaviour is ITurnExecute executor) && (monoBehaviour.enabled))
            {
                turnExecutors.Add(executor);
            }
        }

        // Sort by GetExecutionOrder (ascending)
        turnExecutors.Sort((a, b) => a.GetExecutionOrder().CompareTo(b.GetExecutionOrder()));

        // Execute in order
        foreach (var executor in turnExecutors)
        {
            executor.ExecuteTurn();
        }
    }
}
