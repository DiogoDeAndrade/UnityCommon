using UnityEngine;

namespace UC.Interaction
{
    [CreateAssetMenu(fileName = "Interaction Verb", menuName = "Unity Common/Data/InteractionVerb")]
    public class InteractionVerb : ScriptableObject
    {
        public string displayName;
        public CursorDef cursorDef;
    }
}