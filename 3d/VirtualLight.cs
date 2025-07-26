using UnityEngine;
using System.Collections.Generic;
using NaughtyAttributes;

namespace UC
{

    public class VirtualLight : MonoBehaviour
    {
        private enum ShapeType { Box, Sphere };

        [SerializeField]
        private ShapeType shape;
        [SerializeField, ShowIf(nameof(isSphere))]
        private float radius = 1.0f;
        [SerializeField, ShowIf(nameof(isBox))]
        private Vector3 size = Vector3.one;
        [SerializeField, ShowIf(nameof(isBox))]
        private Vector3 offset = Vector3.zero;
        [SerializeField]
        private int importance = 0;
        [SerializeField]
        private float intensity = 1.0f;
        [SerializeField]
        private bool fadeEnable = false;
        [SerializeField, ShowIf(nameof(fadeEnable))]
        private float fadePercentage = 0.1f;
        [SerializeField]
        private Color lightColor = Color.white;

        bool isSphere => shape == ShapeType.Sphere;
        bool isBox => shape == ShapeType.Box;

        static List<VirtualLight> virtualLights;

        void OnEnable()
        {
            AddVirtualLight(this);
        }

        void OnDisable()
        {
            RemoveVirtualLight(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = lightColor;

            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (shape)
            {
                case ShapeType.Box:
                    Gizmos.DrawWireCube(offset, size);
                    break;
                case ShapeType.Sphere:
                    Gizmos.DrawWireSphere(Vector3.zero, radius);
                    break;
                default:
                    break;
            }

            Gizmos.matrix = prevMatrix;
        }
        public float GetIntensity(Vector3 position)
        {
            if (!fadeEnable) return intensity;

            switch (shape)
            {
                case ShapeType.Sphere:
                    {
                        float dist = Vector3.Distance(transform.position, position);
                        float fadeStart = radius * (1 - fadePercentage);
                        float fadeEnd = radius;

                        if (dist <= fadeStart)
                            return intensity;
                        if (dist >= fadeEnd)
                            return 0f;

                        float t = Mathf.InverseLerp(fadeEnd, fadeStart, dist); // fades from 0 to 1
                        return intensity * (t * t); // quadratic fade
                    }

                case ShapeType.Box:
                    {
                        // Transform position to local space of the box
                        Vector3 localPos = transform.InverseTransformPoint(position) - offset;
                        Vector3 halfSize = size * 0.5f;
                        Vector3 fadeZone = halfSize * fadePercentage;

                        // Calculate distance to the inner fade region on each axis - don't use the Y axis, I'm not usually interested in it and it will just fade out the 
                        // shadow when we're on the ground
                        float dx = Mathf.Max(0f, Mathf.Abs(localPos.x) - (halfSize.x - fadeZone.x));
                        //float dy = Mathf.Max(0f, Mathf.Abs(localPos.y) - (halfSize.y - fadeZone.y));
                        float dz = Mathf.Max(0f, Mathf.Abs(localPos.z) - (halfSize.z - fadeZone.z));

                        // If completely outside box
                        if (Mathf.Abs(localPos.x) > halfSize.x ||
                            //Mathf.Abs(localPos.y) > halfSize.y ||
                            Mathf.Abs(localPos.z) > halfSize.z)
                            return 0f;

                        // Use the largest relative distance to any axis's fade zone as the fade factor
                        float tx = (fadeZone.x > 0) ? dx / fadeZone.x : 0f;
                        //float ty = (fadeZone.y > 0) ? dy / fadeZone.y : 0f;
                        float tz = (fadeZone.z > 0) ? dz / fadeZone.z : 0f;

                        //float t = Mathf.Max(tx, ty, tz); // max fade axis
                        float t = Mathf.Max(tx, tz); // max fade axis
                        t = Mathf.Clamp01(t);
                        return intensity * (1f - t) * (1f - t); // quadratic falloff
                    }

                default:
                    return intensity;
            }
        }


        #region Managemnent
        static void AddVirtualLight(VirtualLight vLight)
        {
            if (virtualLights == null) virtualLights = new();
            virtualLights.Add(vLight);
        }

        static void RemoveVirtualLight(VirtualLight vLight)
        {
            virtualLights?.Remove(vLight);
        }

        public static VirtualLight GetLight(Vector3 position)
        {
#if UNITY_EDITOR
            if (virtualLights == null) virtualLights = new(FindObjectsByType<VirtualLight>(FindObjectsSortMode.None));
#endif

            VirtualLight light = null;
            int importance = -int.MaxValue;
            float minDist = float.MaxValue;

            foreach (var vLight in virtualLights)
            {
                if (vLight.importance >= importance)
                {
                    float dist = float.MaxValue;

                    switch (vLight.shape)
                    {
                        case ShapeType.Box:
                            {
                                // Transform world position into light local space
                                Vector3 localPos = vLight.transform.InverseTransformPoint(position) - vLight.offset;

                                Vector3 halfSize = vLight.size * 0.5f;
                                bool inside = Mathf.Abs(localPos.x) <= halfSize.x &&
                                              Mathf.Abs(localPos.y) <= halfSize.y &&
                                              Mathf.Abs(localPos.z) <= halfSize.z;

                                if (!inside)
                                {
                                    dist = float.MaxValue;
                                }
                                else
                                {
                                    // Optional: use distance to box center (for prioritization)
                                    dist = localPos.magnitude;
                                }
                            }
                            break;
                        case ShapeType.Sphere:
                            dist = Vector3.Distance(position, vLight.transform.position);
                            if (dist >= vLight.radius) dist = float.MaxValue;
                            break;
                        default:
                            break;
                    }

                    if (dist < minDist)
                    {
                        light = vLight;
                        importance = vLight.importance;
                        minDist = dist;
                    }
                }
            }

            return light;
        }

        #endregion
    }
}