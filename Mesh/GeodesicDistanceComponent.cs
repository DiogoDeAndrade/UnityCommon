using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{

    [ExecuteInEditMode]
    [RequireComponent(typeof(TopologyComponent))]
    public class GeodesicDistanceComponent : MonoBehaviour
    {
        [SerializeField]
        private Transform sourcePoint;
        [SerializeField]
        private Gradient colorGradient;
        [SerializeField]
        private bool testPoint;
        [SerializeField, ShowIf(nameof(testPoint))]
        private Transform testPosition;
        [SerializeField]
        private bool interaction = false;
        [SerializeField, ShowIf("interaction")]
        private bool showPath = false;

        [SerializeField, HideInInspector]
        GeodesicDistance _geodesicDistance;

        public GeodesicDistance geodesicDistance => _geodesicDistance;
        public bool hasSourcePoint => (sourcePoint != null);

        [Button("Build")]
        public void Build()
        {
            TopologyComponent topologyComponent = GetComponent<TopologyComponent>();
            if (topologyComponent == null) return;

            var topology = topologyComponent.topology;
            if (topology == null) return;

            Build(topology);

            topologyComponent.customColorFunction = CustomColorFunction;
        }

        public void Build(TopologyStatic topology, Vector3? sourcePos = null)
        {
            int sourcePointId = (sourcePos.HasValue) ? (topology.GetClosestPointId(sourcePos.Value)) : (topology.GetClosestPointId(sourcePoint.position));

            _geodesicDistance = new();
            _geodesicDistance.topology = topology;
            _geodesicDistance.sourcePointId = sourcePointId;
            _geodesicDistance.Build();
        }

        Color CustomColorFunction(int index, TopologyStatic.TVertex vertex, Color originalColor)
        {
            if (_geodesicDistance == null) return originalColor;

            if (index == _geodesicDistance.sourcePointId) return Color.cyan;
            if (colorGradient != null)
            {
                float distance = _geodesicDistance.GetDistance(index);
                float maxDistance = _geodesicDistance.GetMaxDistance();

                float t = distance / maxDistance;
                return colorGradient.Evaluate(t);
            }

            return originalColor;
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            // Subscribe to the Scene view event
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            // Unsubscribe when disabled
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private TopologyStatic.TVertex hoverVertex;
        private int hoverVertexId;

        void OnSceneGUI(SceneView view)
        {
            if (!interaction)
            {
                hoverVertex = null;
                return;
            }

            TopologyComponent topologyComponent = GetComponent<TopologyComponent>();
            if (topologyComponent == null) return;

            var topology = topologyComponent.topology;
            if (topology == null) return;

            if ((topology == null) || (topology.vertices == null))
            {
                hoverVertex = null;
                return;
            }

            // Get mouse position in Scene view
            Event e = Event.current;
            if (e != null)
            {
                // Only proceed if the mouse is moving in the scene view
                if (e.type == EventType.MouseMove || e.type == EventType.Repaint)
                {
                    hoverVertex = null;
                    hoverVertexId = -1;

                    // Create a ray from the mouse position, and change it to local coordinates
                    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                    for (int i = 0; i < topology.vertices.Count; i++)
                    {
                        var vertex = topology.vertices[i];
                        if (Sphere.Raycast(ray, vertex.position, 0.1f, float.MaxValue, out float dist))
                        {
                            hoverVertex = vertex;
                            hoverVertexId = i;
                            break;
                        }
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if ((testPoint) && (testPosition != null))
            {
                TopologyComponent topologyComponent = GetComponent<TopologyComponent>();
                if (topologyComponent == null) return;

                var topology = topologyComponent.topology;
                if (topology == null) return;

                int index = topology.GetClosestTriangle(testPosition.position, out float u, out float v, out float w);
                if (index != -1)
                {
                    Gizmos.color = Color.cyan.ChangeAlpha(0.25f);
                    (var p1, var p2, var p3) = topology.GetTriangle(index);
                    DebugHelpers.DrawTriangle(p1, p2, p3);

                    float distance = _geodesicDistance.ComputeDistance(index, u, v, w);
                    float maxDistance = _geodesicDistance.GetMaxDistance();
                    float t = distance / maxDistance;

                    Gizmos.color = colorGradient.Evaluate(t);
                    Gizmos.DrawSphere(p1 * u + p2 * v + p3 * w, 0.1f);
                }
            }
            if ((hoverVertex != null) && (hoverVertexId != -1) && (_geodesicDistance != null) && (_geodesicDistance.isComputed))
            {
                float d = _geodesicDistance.GetDistance(hoverVertexId);
                DebugHelpers.DrawTextAt(hoverVertex.position, new Vector3(20, 20, 0), 12, Color.white, $"Vertex={hoverVertexId}, Distance={d}", true);

                if (showPath)
                {
                    var topology = _geodesicDistance.topology;
                    Vector3 prevVertex = topology.GetVertexPosition(hoverVertexId);
                    int currentVertex = _geodesicDistance.GetClosestVertexToStart(hoverVertexId);
                    Gizmos.color = Color.yellow;
                    while (currentVertex != -1)
                    {
                        Vector3 currentPos = topology.GetVertexPosition(currentVertex);
                        Gizmos.DrawLine(prevVertex, currentPos);
                        prevVertex = currentPos;
                        currentVertex = _geodesicDistance.GetClosestVertexToStart(currentVertex);
                    }
                }
            }
        }
#endif
    }
}