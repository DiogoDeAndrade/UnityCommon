using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using UnityEngine.Rendering;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

[RequireComponent(typeof(TopologyComponent))]
public class GeodesicDistanceComponent : MonoBehaviour
{
    [SerializeField]
    private Transform   sourcePoint;
    [SerializeField]
    private Gradient    colorGradient;
    [SerializeField]
    private bool        testPoint;
    [SerializeField, ShowIf(nameof(testPoint))]
    private Transform   testPosition;

    [SerializeField, HideInInspector]
    GeodesicDistance _geodesicDistance;

    public GeodesicDistance geodesicDistance => _geodesicDistance;

    [Button("Build")]
    public void Build()
    {
        TopologyComponent topologyComponent = GetComponent<TopologyComponent>();
        if (topologyComponent == null) return;

        var topology = topologyComponent.topology;
        if (topology == null) return;

        int sourcePointId = topology.GetClosestPointId(sourcePoint.position);

        _geodesicDistance = new();
        _geodesicDistance.topology = topology;
        _geodesicDistance.sourcePointId = sourcePointId;
        _geodesicDistance.Build();

        topologyComponent.customColorFunction = CustomColorFunction;
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
    }
}
