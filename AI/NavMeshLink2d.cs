using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    /// <summary>
    /// Bridges two navmesh polygons that the bake left disconnected (an openable door, a wall to
    /// jump, etc.). The two endpoints are stored as positions relative to this transform and edited
    /// in the Scene view with the "Edit NavMesh Link" tool.
    /// </summary>
    public class NavMeshLink2d : MonoBehaviour
    {
        [SerializeField]
        private NavMeshAgentType2d  agentType;
        [SerializeField, Tooltip("First endpoint, relative to this transform.")]
        private Vector3             localStart = new Vector3(-10.0f, 0.0f, 0.0f);
        [SerializeField, Tooltip("Second endpoint, relative to this transform.")]
        private Vector3             localEnd = new Vector3(10.0f, 0.0f, 0.0f);
        [SerializeField, Tooltip("If set, the agent walks across the link as ordinary path waypoints " +
                                 "(through the closest points between the two polygons). If clear, the " +
                                 "agent walks to endpoint A and delegates the crossing to an " +
                                 "INavMeshLinkTraversal on its own GameObject (teleport/jump/etc.).")]
        private bool                autoTraverse = true;
        [SerializeField, Tooltip("If clear, the link can only be traversed from A to B, not back.")]
        private bool                bidirectional = true;
        [SerializeField, Min(0.0f), Tooltip("Multiplier applied to the geometric bridge length when " +
                                            "computing the A* traversal cost. 1 = behave like ordinary travel.")]
        private float               costMultiplier = 1.0f;

        private INavMeshLinkCondition[] _conditions;

        public NavMeshAgentType2d   AgentType => agentType;
        public bool                 IsAutoTraverse => autoTraverse;
        public bool                 IsBidirectional => bidirectional;
        public float                CostMultiplier => Mathf.Max(0.0f, costMultiplier);

        public Vector3 worldStart => transform.TransformPoint(localStart);
        public Vector3 worldEnd   => transform.TransformPoint(localEnd);

        // PathXY-style accessors used by the editor tool. Points are kept in local space.
        public List<Vector3> GetEditPoints() => new List<Vector3> { localStart, localEnd };
        public void SetEditPoints(List<Vector3> points)
        {
            if (points == null || points.Count < 2) return;
            localStart = points[0];
            localEnd = points[1];
            Invalidate();
        }

        /// <summary>True when every <see cref="INavMeshLinkCondition"/> on this GameObject allows passage.</summary>
        public bool CanPass(NavMeshAgent2d agent)
        {
            var conditions = _conditions ?? RefreshConditions();
            for (int i = 0; i < conditions.Length; i++)
            {
                try
                {
                    if (!conditions[i].NavCanPass(agent, this)) return false;
                }
                catch
                {
                    // A condition that throws (e.g. when evaluated with a null agent during debug
                    // drawing/path testing) is treated as blocking the link.
                    return false;
                }
            }
            return true;
        }

        public INavMeshLinkCondition[] RefreshConditions()
        {
            _conditions = GetComponents<INavMeshLinkCondition>();
            return _conditions;
        }

        /// <summary>Tell navmeshes to rebuild their link adjacency (endpoints/agent type changed).</summary>
        public void Invalidate()
        {
            NavMesh2d.InvalidateLinks();
        }

        private void Awake()
        {
            RefreshConditions();
        }

        private void OnEnable()
        {
            RefreshConditions();
            NavMesh2d.RegisterLink(this);
        }

        private void OnDisable()
        {
            NavMesh2d.UnregisterLink(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _conditions = null;
            Invalidate();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1.0f, 0.6f, 0.1f, 1.0f);
            Vector3 a = worldStart;
            Vector3 b = worldEnd;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 1.5f);
            Gizmos.DrawSphere(b, 1.5f);
        }
#endif
    }
}
