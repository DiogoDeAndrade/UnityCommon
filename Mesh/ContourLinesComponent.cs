using NaughtyAttributes;
using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(GeodesicDistanceComponent))]
public class ContourLinesComponent : MonoBehaviour
{
    [SerializeField, Range(0.0f, 1.0f)]
    private float l = 0.0f;
    [SerializeField]
    private bool displayTriangles;

    private List<Polyline>  contours;
    private List<List<int>> triIndices;

    [Button("Rebuild")]
    private void Rebuild()
    {
        var geodesicDistanceComponent = GetComponent<GeodesicDistanceComponent>();
        var geodesicDistance = geodesicDistanceComponent.geodesicDistance;
        var staticTopologyComponent = GetComponent<TopologyComponent>();
        if (staticTopologyComponent)
        {
            var topology = staticTopologyComponent.topology;
            geodesicDistance.topology = topology;
        }

        float lRef = l * geodesicDistance.GetMaxDistance();

        /*Debug.Log("Forcing vertex 73 distance");
        lRef = geodesicDistance.GetDistance(73);*/

        Debug.Log($"LRef = {lRef}");

        triIndices = new();
        contours = geodesicDistance.ComputeContours(lRef, false, triIndices);
    }

    static Color[] ContourLinesColors = { Color.red, Color.yellow, Color.cyan, Color.green, Color.blue, Color.magenta, Color.white, Color.black, Color.grey };

    private void OnDrawGizmosSelected()
    {
        if (!enabled) return;
        if (contours == null) return;

        var staticTopologyComponent = GetComponent<TopologyComponent>();
        TopologyStatic topology = null;
        if (staticTopologyComponent)
        {
            topology = staticTopologyComponent.topology;
        }

        for (int i = 0; i < contours.Count; i++) 
        {
            var polyline = contours[i];
            Gizmos.color = ContourLinesColors[i % ContourLinesColors.Length];
            polyline.DrawGizmos();

            Gizmos.color = ContourLinesColors[i % ContourLinesColors.Length].ChangeAlpha(0.5f);
            if ((displayTriangles)  && (topology != null) && (triIndices != null))
            {
                foreach (var triIndex in triIndices[i])
                {
                    (Vector3 p1, Vector3 p2, Vector3 p3) = topology.GetTriangle(triIndex);
                    DebugHelpers.DrawTriangle(p1, p2, p3);
                }
            }
        }
    }
}
