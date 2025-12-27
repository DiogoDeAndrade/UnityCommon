using UnityEngine;

namespace UC.RPG
{
    // Base class for turn state - should be implemented in partial classes elsewhere in the game specific code
    // This should keep any state necessary for turn drivers to function (local variables on the class won't work properly, since that object is shared)
    public partial class TurnState
    {
    }
}
