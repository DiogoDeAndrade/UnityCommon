using UnityEngine;

public abstract class TurnManager : MonoBehaviour
{
    private static TurnManager instance;

    void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Something wrong, it seems there's more than one TurnManager!");
        }
        else
        {
            instance = this;
        }
    }

    public abstract void _StartTurns();
    public abstract void _StopTurns();

    public static void StartTurns()
    {
        instance?._StartTurns();
    }

    public static void StopTurns()
    {
        instance?._StopTurns();
    }
}
