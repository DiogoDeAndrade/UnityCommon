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

        float lRef = l * geodesicDistance.GetMaxDistance();

        Debug.Log($"LRef = {lRef}");

        contours = geodesicDistance.ComputeContour(lRef);
    }

    private void OnDrawGizmosSelected()
    {
        if (contours == null) return;

        Gizmos.color = Color.magenta;
        foreach (var polyline in contours)
        {
            polyline.DrawGizmos();
        }
    }
}
