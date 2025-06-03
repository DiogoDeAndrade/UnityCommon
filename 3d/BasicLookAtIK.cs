using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This script controls looking at points of interest (POI) using IK (Inverse Kinematics).
// It supports both looking for POIs in the scene, and wandering gaze if no POI is found.
// The look position can be overriden externally, for example when a BasicHoldIK script is used to force the gaze towards a weapon sight or similar target.

namespace UC
{

    [RequireComponent(typeof(Animator))]
    public class BasicLookAtIK : MonoBehaviour
    {
        // Basically, we can define different scan areas, with different FOV and ranges. It can also scan different layers for interesting objects.
        [System.Serializable]
        struct ScanParameters
        {
            public float fov;
            public float minRange;
            public float maxRange;
            public LayerMask scanLayers;
            public bool displayCone;
        }

#if UNITY_EDITOR
        class DebugPOI
        {
            public enum LOS { Untested, No, Yes };

            public Vector3 position;
            public LOS hasLOS;
            public bool selected;
        }
        List<DebugPOI> debugPOIs = new();
#endif

        [SerializeField, Tooltip("If we want the humanoid to turn the body towards the POI - 0 means it won't, 1 means it will turn fully, 0.5 means it will turn a bit.")]
        float maxWeightBody = 0.0f;     
        [SerializeField, Tooltip("Should it turn the head towards the POI? 0 means it won't, 1 means it will turn fully, 0.5 means it will turn a bit.")]
        float maxWeightHead = 1.0f;
        [SerializeField, Tooltip("Should it turn the eyes towards the POI? 0 means it won't, 1 means it will turn fully, 0.5 means it will turn a bit. This requires the humanoid rig to have the eyes bones set up correctly in the animator.")]
        float maxWeightEyes = 1.0f;
        [SerializeField, Tooltip("Time to reset the look at position and weights when the target is lost or changed.")]
        float timeToRest = 0.1f;
        [SerializeField, Tooltip("When tracking a target, how fast do we follow it?")]
        float maxSpeedToTarget = 100.0f;
        [SerializeField, Tooltip("When wandering with the gaze, how fast do we move around?")]
        float maxSpeedToWander = 10.0f;
        [SerializeField, Tooltip("Should we check for LoS when tracking a POI?")]
        bool checkLOS = true;
        [SerializeField, MinMaxSlider(0.0f, 1.0f), ShowIf(nameof(checkLOS)), Tooltip("What is the normalized range of the LoS check?\nIf set to 0 and 1, it's the normal, if it's set to 0.1 to 0.9, it will only track obstacles from 10% to 90% of the way towards the POI.")]
        Vector2 LOSNormalizedRange = new Vector2(0.0f, 1.0f);
        [SerializeField, ShowIf(nameof(checkLOS)), Tooltip("Obstacle layers for LoS check")]
        LayerMask obstacleLayers = ~0;
        [SerializeField, Tooltip("If there's no POI, should the view wander?")]
        bool autoWander = false;
        [SerializeField, ShowIf(nameof(autoWander)), MinMaxSlider(0.5f, 5.0f), Tooltip("How long between wander transitions?")]
        Vector2 wanderChangeTime = new Vector2(1.0f, 3.0f);
        [SerializeField, ShowIf(nameof(autoWander)), MinMaxSlider(0.0f, 90.0f), Tooltip("How much to wander, in degrees?")]
        Vector2 wanderAngularRange = new Vector2(0.0f, 20.0f);
        [SerializeField, Tooltip("Both on POI or wandering, should we use noise to do a kind of 'subwandering'?")]
        bool useNoise = false;
        [SerializeField, ShowIf(nameof(needNoiseParams)), Tooltip("Noise frequency")]
        Vector3 noiseFrequency = Vector3.one;
        [SerializeField, ShowIf(nameof(needNoiseParams)), Tooltip("Noise amplitude")]
        float noiseAmplitude = 2.0f;
        [SerializeField, Tooltip("Scan parameters, ideally define 3 levels")]
        ScanParameters[] scanParameters;
        [SerializeField, Tooltip("Should the gizmos be displayed?")]
        bool displayGizmos = true;

        bool needNoiseParams => useNoise;

        Transform target;
        Vector3 targetPos;
        Animator animator;
        Vector3 currentTargetPosition;
        Vector3 targetVelocity;
        float targetLookAtWeight = 1.0f;
        float currentLookAtWeight = 0.0f;
        float weightVelocity = 0.0f;
        float wanderTime = 0.0f;
        Vector3 targetBasePos;
        Vector2 wanderAngularRangeRad;
        Vector3 noiseAngle;
        Transform forceLook;

        private void Start()
        {
            animator = GetComponent<Animator>();

            noiseAngle = new Vector3(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f));
        }

        private void Update()
        {
            FindPOITarget();

            float ms = maxSpeedToTarget;

            // This only does anything if there is no target set
            if (target == null)
            {
                if (autoWander)
                {
                    WanderSight();
                    ms = maxSpeedToWander;
                }
            }

            if (useNoise)
            {
                noiseAngle += Time.deltaTime * noiseFrequency;

                targetPos = targetBasePos + noiseAmplitude * new Vector3(Mathf.PerlinNoise1D(noiseAngle.x) * 2.0f - 1.0f,
                                                                         Mathf.PerlinNoise1D(noiseAngle.y) * 2.0f - 1.0f,
                                                                         Mathf.PerlinNoise1D(noiseAngle.z) * 2.0f - 1.0f);
            }
            else
            {
                targetPos = targetBasePos;
            }

            if (forceLook != null)
            {
                targetPos = forceLook.position;
                target = forceLook;
                targetLookAtWeight = 1.0f;
            }

            // Smooth look movements
            currentLookAtWeight = Mathf.SmoothDamp(currentLookAtWeight, targetLookAtWeight, ref weightVelocity, timeToRest, float.MaxValue, Time.deltaTime);
            currentTargetPosition = Vector3.SmoothDamp(currentTargetPosition, targetPos, ref targetVelocity, timeToRest, ms, Time.deltaTime);
        }

        void WanderSight()
        {
            wanderTime -= Time.deltaTime;
            if (wanderTime <= 0.0f)
            {
                wanderTime = wanderChangeTime.Random();

                // Random yaw and pitch (Y and X)
                float yaw = Random.Range(-wanderAngularRange.y, wanderAngularRange.y);     // Yaw (horizontal)
                float pitch = Random.Range(-wanderAngularRange.x, wanderAngularRange.x);   // Pitch (vertical)

                // Build a rotation from pitch and yaw
                Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

                // Apply rotation to forward direction
                Vector3 direction = rot * transform.forward;

                var headPos = animator.GetBoneTransform(HumanBodyBones.Head).position;

                // Compute target position
                targetBasePos = targetPos = headPos + direction.normalized * 20.0f;
            }

            targetLookAtWeight = 1.0f;
        }

        void FindPOITarget()
        {
#if UNITY_EDITOR
            debugPOIs.Clear();
            DebugPOI selectedPOI = null;
#endif

            target = null;

            var headPos = animator.GetBoneTransform(HumanBodyBones.Head).position;
            var headDir = transform.forward;
            int currentInterest = -1;
            float currentPriority = -float.MaxValue;
            float currrentDistance = float.MaxValue;

            for (int i = 0; i < scanParameters.Length; i++)
            {
                var scan = scanParameters[i];
                var halfFOV = scan.fov / 2.0f;

                var objectsInRange = Physics.OverlapSphere(headPos, scan.maxRange, scan.scanLayers);
                foreach (var objectInRange in objectsInRange)
                {
                    Vector3 dir = objectInRange.transform.position - headPos;
                    float dist = dir.magnitude;
                    if (dist < scan.minRange) continue;

                    PointOfInterest poi = objectInRange.GetComponent<PointOfInterest>();
                    if (poi != null)
                    {
                        // Scan of level 0 is only interested in POI interest level of 0, 1, and 2
                        // Scan of level 1 is only interested in POI interest level of 1 and 2
                        // Scan of level 2 is only interested in POI interest level of 2
                        if (poi.interestLevel < i) continue;

#if UNITY_EDITOR
                        var debugElem = new DebugPOI
                        {
                            position = objectInRange.transform.position,
                            hasLOS = DebugPOI.LOS.Untested,
                            selected = false,
                        };
                        debugPOIs.Add(debugElem);
#endif

                        if ((poi.priority > currentPriority) || ((poi.priority == currentPriority) && (dist < currrentDistance)) ||
                            (currentInterest < poi.interestLevel))
                        {
                            var angle = Vector3.Angle(headDir, objectInRange.transform.position - headPos);
                            if (angle < halfFOV)
                            {
                                if (checkLOS)
                                {
                                    var hits = Physics.RaycastAll(headPos, dir.normalized, dist, obstacleLayers);
                                    bool hasLOS = true;
                                    float dot = Vector3.Dot(headDir, dir.normalized);
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
#if UNITY_EDITOR
                                        debugElem.hasLOS = DebugPOI.LOS.No;
#endif
                                        continue;
                                    }
                                }

#if UNITY_EDITOR
                                debugElem.hasLOS = DebugPOI.LOS.Yes;
                                if (selectedPOI != null) selectedPOI.selected = false;
                                debugElem.selected = true;
                                selectedPOI = debugElem;
#endif

                                target = poi.transform;
                                currentPriority = poi.priority;
                                currrentDistance = dist;
                                currentInterest = poi.interestLevel;
                            }
                        }
                    }
                }

                if (target != null)
                {
                    targetBasePos = targetPos = target.position;
                    wanderTime = 0.0f;
                }
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (target)
            {
                targetLookAtWeight = 1.0f;
            }
            else
            {
                targetLookAtWeight = 0.0f;
            }

            animator.SetLookAtPosition(currentTargetPosition);
            animator.SetLookAtWeight(currentLookAtWeight, maxWeightBody, maxWeightHead, maxWeightEyes);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!displayGizmos) return;
            if (animator == null) animator = GetComponent<Animator>();

            var headPos = animator.GetBoneTransform(HumanBodyBones.Head).position;
            var headDir = transform.forward;

            Handles.color = Color.cyan.ChangeAlpha(0.05f);
            foreach (var scan in scanParameters)
            {
                if (scan.displayCone)
                {
                    Handles.DrawSolidArc(headPos, Vector3.up, headDir, scan.fov / 2.0f, scan.maxRange);
                    Handles.DrawSolidArc(headPos, Vector3.up, headDir, -scan.fov / 2.0f, scan.maxRange);
                }
            }

            foreach (var target in debugPOIs)
            {
                Gizmos.color = target.selected ? Color.green : Color.white;
                Gizmos.DrawSphere(target.position, 0.1f);
                if (target.hasLOS == DebugPOI.LOS.Yes)
                {
                    Gizmos.color = Color.green;
                }
                else if (target.hasLOS == DebugPOI.LOS.No)
                {
                    Gizmos.color = Color.red;
                }
                else
                {
                    Gizmos.color = Color.yellow;
                }
                Gizmos.DrawLine(headPos, target.position);
            }
        }

        public void ForceLook(Transform target)
        {
            forceLook = target;
        }
#endif
    }
}