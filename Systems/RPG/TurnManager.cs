using UnityEngine;

namespace UC
{

    public abstract class TurnManager : Singleton<TurnManager>
    {
        public abstract void _StartTurns();
        public abstract void _StopTurns();

        public static void StartTurns()
        {
            Instance?._StartTurns();
        }

        public static void StopTurns()
        {
            Instance?._StopTurns();
        }
    }
}