using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    /// <summary>
    /// Marks its colliders as impassable for the navmesh, regardless of their layer or static flag.
    /// The navmesh bakes these in as obstacles; adding or removing one at runtime requests a rebuild of
    /// the affected navmesh(es). Optionally scoped to a single agent type.
    /// </summary>
    public class NavMeshObstacle2d : MonoBehaviour
    {
        [SerializeField, Tooltip("If set, only the navmesh for this agent type treats this as an obstacle. If empty, every navmesh does.")]
        private NavMeshAgentType2d  agentType;

        public NavMeshAgentType2d   AgentType => agentType;

        /// <summary>Fills 'into' with the colliders on this GameObject that should block the navmesh.</summary>
        public void GetColliders(List<Collider2D> into)
        {
            GetComponents(into);
        }

        private void OnEnable()
        {
            NavMesh2d.NotifyObstacleAdded(this);
        }

        private void OnDisable()
        {
            NavMesh2d.NotifyObstacleRemoved(this);
        }
    }
}
