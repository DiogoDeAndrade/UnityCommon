using NaughtyAttributes;
using UnityEngine;
using System.Collections.Generic;
using System;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using System.Linq.Expressions;

[RequireComponent(typeof(GeodesicDistanceComponent))]
public class ContourLinesComponent : MonoBehaviour
{
    [SerializeField, Range(0.0f, 1.0f)]
    private float l = 0.0f;

    private List<Polyline> contours;
    
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

        contours = geodesicDistance.ComputeContours(lRef, false);
    }

    static Color[] ContourLinesColors = { Color.red, Color.yellow, Color.cyan, Color.green, Color.blue, Color.magenta, Color.white, Color.black, Color.grey };

    private void OnDrawGizmosSelected()
    {
        if (contours == null) return;

        for (int i = 0; i < contours.Count; i++) 
        {
            var polyline = contours[i];
            Gizmos.color = ContourLinesColors[i % ContourLinesColors.Length];
            polyline.DrawGizmos();
        }
    }
}
