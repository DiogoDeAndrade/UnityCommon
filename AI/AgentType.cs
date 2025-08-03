using UnityEngine;

namespace UC
{
    [CreateAssetMenu(fileName = "AgentType", menuName = "Unity Common/Data/AgentType")]
    public class AgentType : ScriptableObject
    {
        [SerializeField] public float agentRadius = 1.0f;
    }
}
