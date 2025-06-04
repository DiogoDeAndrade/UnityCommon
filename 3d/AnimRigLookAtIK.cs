#if UNITY_3D_ANIMATION_RIG_AVAILABLE

using NaughtyAttributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This script controls looking at points of interest (POI) using IK (Inverse Kinematics).
// It uses the animation rig package
// It supports both looking for POIs in the scene, and wandering gaze if no POI is found.
// The look position can be overriden externally, for example when a BasicHoldIK script is used to force the gaze towards a weapon sight or similar target.
// A good approach in terms of rig is having a series of MultiAimConstraints, starting at the head and going down the neck/spine/etc, with less weight.

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class AnimRigLookAtIK : MonoBehaviour
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

        [SerializeField, Tooltip("Rig that controls looking at stuff"), Header("IK Setup")]
        Rig     lookRig;
        [SerializeField, Tooltip("Object to control as look target")]
        Transform targetObject;
        [SerializeField, Tooltip("Time to reset the look at position and weights when the target is lost or changed."), Header("Animation")]
        float timeToRest = 0.1f;
        [SerializeField, Tooltip("When tracking a target, how fast do we follow it?")]
        float maxSpeedToTarget = 100.0f;
        [SerializeField, Tooltip("When wandering with the gaze, how fast do we move around?")]
        float maxSpeedToWander = 10.0f;
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
        [SerializeField, Tooltip("Should we check for LoS when tracking a POI?"), Header("Target Selection")]
        bool checkLOS = true;
        [SerializeField, MinMaxSlider(0.0f, 1.0f), ShowIf(nameof(checkLOS)), Tooltip("What is the normalized range of the LoS check?\nIf set to 0 and 1, it's the normal, if it's set to 0.1 to 0.9, it will only track obstacles from 10% to 90% of the way towards the POI.")]
        Vector2 LOSNormalizedRange = new Vector2(0.0f, 1.0f);
        [SerializeField, ShowIf(nameof(checkLOS)), Tooltip("Obstacle layers for LoS check")]
        LayerMask obstacleLayers = ~0;
        [SerializeField, Tooltip("Scan parameters, ideally define 3 levels")]
        ScanParameters[] scanParameters;

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

            if (target)
            {
                targetObject.position = currentTargetPosition;
                
                lookRig.weight = currentLookAtWeight;
            }
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
            target = null;

            var headPos = animator.GetBoneTransform(HumanBodyBones.Head).position;
            var headDir = transform.forward;
            int currentInterest = -1;
            float currentPriority = -float.MaxValue;
            float currentDistance = float.MaxValue;

            for (int i = 0; i < scanParameters.Length; i++)
            {
                var scan = scanParameters[i];

                var poi = PointOfInterest.GetClosestPOI(headPos, headDir, scan.fov, scan.minRange, scan.maxRange, scan.scanLayers, i,
                                                        checkLOS, obstacleLayers, LOSNormalizedRange);
                if (poi)
                {
                    float dist = Vector3.Distance(headPos, poi.transform.position);
                    if ((dist < currentDistance) || (poi.priority > currentPriority) || (currentInterest < poi.interestLevel))
                    {
                        currentInterest = poi.interestLevel;
                        currentPriority = poi.priority;
                        currentDistance = dist;
                        target = poi.transform;
                    }
                }

                if (target != null)
                {
                    targetBasePos = targetPos = target.position;
                    wanderTime = 0.0f;
                }
            }
        }

        /*private void OnAnimatorIK(int layerIndex)
        {
            if ((layerIndex != animationLayer) && (animationLayer != -1)) return;

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
        }*/

        public void ForceLook(Transform target)
        {
            forceLook = target;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
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
        }
#endif
    }
}

#endif