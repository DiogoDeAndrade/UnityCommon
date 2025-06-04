using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    public class PointOfInterest : MonoBehaviour
    {
        [SerializeField, Range(0, 2)]
        int _interestLevelCategory = 2;
        [SerializeField]
        float _priority = 0.0f;

        public int interestLevel => _interestLevelCategory;
        public float priority => _priority;

        public static PointOfInterest GetClosestPOI(Vector3 sourcePos, Vector3 direction, float fieldOfView, 
                                                    float minRange, float maxRange, LayerMask scanLayers, int interestBase,
                                                    bool checkLOS, LayerMask obstacleLayer, Vector2 LOSNormalizedRange)
        {
            PointOfInterest target = null;
            int             currentInterest = -1;
            float           currentPriority = -float.MaxValue;
            float           currrentDistance = float.MaxValue;
            var             halfFOV = fieldOfView / 2.0f;

            var objectsInRange = Physics.OverlapSphere(sourcePos, maxRange, scanLayers);
            foreach (var objectInRange in objectsInRange)
            {
                Vector3 dir = objectInRange.transform.position - sourcePos;
                float dist = dir.magnitude;
                if (dist < minRange) continue;

                PointOfInterest poi = objectInRange.GetComponent<PointOfInterest>();
                if (poi != null)
                {
                    // Scan of level 0 is only interested in POI interest level of 0, 1, and 2
                    // Scan of level 1 is only interested in POI interest level of 1 and 2
                    // Scan of level 2 is only interested in POI interest level of 2
                    if (poi.interestLevel < interestBase) continue;

                    if ((poi.priority > currentPriority) || ((poi.priority == currentPriority) && (dist < currrentDistance)) ||
                        (currentInterest < poi.interestLevel))
                    {
                        var angle = Vector3.Angle(direction, objectInRange.transform.position - sourcePos);
                        if (angle < halfFOV)
                        {
                            if (checkLOS)
                            {
                                var hits = Physics.RaycastAll(sourcePos, dir.normalized, dist, obstacleLayer);
                                bool hasLOS = true;
                                float dot = Vector3.Dot(direction, dir.normalized);
                                foreach (var rayHit in hits)
                                {
                                    if (rayHit.transform != objectInRange.transform)
                                    {
                                        float t = rayHit.distance / dist;
                                        if ((t >= LOSNormalizedRange.x) && (t <= LOSNormalizedRange.y))
                                        {
                                            hasLOS = false;
                                            break;
                                        }
                                    }
                                }

                                if (!hasLOS)
                                {
                                    continue;
                                }
                            }

                            target = poi;
                            currentPriority = poi.priority;
                            currrentDistance = dist;
                            currentInterest = poi.interestLevel;
                        }
                    }
                }
            }

            return target;
        }

        public static bool GetPOI(Vector3 sourcePos, Vector3 direction, float fieldOfView,
                                  float minRange, float maxRange, LayerMask scanLayers, int interestBase,
                                  bool checkLOS, LayerMask obstacleLayer, Vector2 LOSNormalizedRange,
                                  List<PointOfInterest> ret)
        {
            bool    found = false;
            var     halfFOV = fieldOfView / 2.0f;

            var objectsInRange = Physics.OverlapSphere(sourcePos, maxRange, scanLayers);
            foreach (var objectInRange in objectsInRange)
            {
                Vector3 dir = objectInRange.transform.position - sourcePos;
                float dist = dir.magnitude;
                if (dist < minRange) continue;

                PointOfInterest poi = objectInRange.GetComponent<PointOfInterest>();
                if (poi != null)
                {
                    // Scan of level 0 is only interested in POI interest level of 0, 1, and 2
                    // Scan of level 1 is only interested in POI interest level of 1 and 2
                    // Scan of level 2 is only interested in POI interest level of 2
                    if (poi.interestLevel < interestBase) continue;

                    var angle = Vector3.Angle(direction, objectInRange.transform.position - sourcePos);
                    if (angle < halfFOV)
                    {
                        if (checkLOS)
                        {
                            var hits = Physics.RaycastAll(sourcePos, dir.normalized, dist, obstacleLayer);
                            bool hasLOS = true;
                            float dot = Vector3.Dot(direction, dir.normalized);
                            foreach (var rayHit in hits)
                            {
                                if (rayHit.transform != objectInRange.transform)
                                {
                                    float t = rayHit.distance / dist;
                                    if ((t >= LOSNormalizedRange.x) && (t <= LOSNormalizedRange.y))
                                    {
                                        hasLOS = false;
                                        break;
                                    }
                                }
                            }

                            if (!hasLOS)
                            {
                                continue;
                            }
                        }

                        ret.Add(poi);
                        found = true;
                    }
                }
            }

            return found;
        }
    }
}