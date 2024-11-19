using NaughtyAttributes;
using UnityEngine;

public class LevelSetDiagramComponent : MonoBehaviour
{
    [SerializeField, HideInInspector] private LevelSetDiagram levelSetDiagram;
    [SerializeField] private bool displayContours;

    [Button("Build")]
    void Build()
    {
        var geodesicDistanceComponent = GetComponent<GeodesicDistanceComponent>();
        var geodesicDistance = geodesicDistanceComponent.geodesicDistance;

        levelSetDiagram = new LevelSetDiagram();
        levelSetDiagram.topology = geodesicDistance.topology;
        levelSetDiagram.comparisonOperator = geodesicDistance;
        levelSetDiagram.Build();
    }

    private void OnDrawGizmos()
    {
        /*if (displayContours)
        {
            Gizmos.color = Color.yellow;
            if (levelSetDiagram.contours != null)
            {
                foreach (var c in levelSetDiagram.contours)
                {
                    foreach (var p in c.contours)
                    {
                        p.DrawGizmos();
                    }
                }
            }
        }*/
    }
}
