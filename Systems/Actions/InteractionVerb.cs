using UnityEngine;

namespace UC.Interaction
{
    [CreateAssetMenu(fileName = "Interaction Verb", menuName = "WSKit/InteractionVerb")]
    public class InteractionVerb : ScriptableObject
    {
        public string displayName;
        public CursorDef cursorDef;
    }
}