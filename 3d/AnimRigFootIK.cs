#if UNITY_3D_ANIMATION_RIG_AVAILABLE

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This script controls the feet.
// It assumes there are animator parameters with the same names as the configured feet constraints, that define if the respective feet should be up (1) or down (0).
// This makes sense to be driven by the animations themselves, so after you import an animation (from an FBX), you can add a Curve with that name
// that can be used to drive that value.
// The tweak value will be added to the foot position, and it's kind of a hack to make sure the feet are not floating above the ground on some types of avatars
// I'm not 100% happy with this thing, there's still a lot of room for improvement:
// - There's the hack which I haven't found a better way to do it, which is the dual extension offset - In my head it doesn't make much sense
// - Most of the controls snap too quickly, so you can kind of see it on transitions (for example, walk in place near a stair)
// - There's still some twitching all around, maybe I need to smooth the target position
// - I think I need to remove the targets from the inside of the object hierarchy for better visuals - this way, I can "lock" the feet in place, and just move it when it's lifted.
//   If it's linked to the object, while moving the object, I'm moving the constraint target, which will move the foot instead of being locked to the ground, which I belive is part
//   of the weird visuals.
// Each individual foot is a Two-Bone Constraint, with tip being the foot bone. Setting position weight to 0.75 looks much better than 1.0.

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class AnimRigFootIK : MonoBehaviour
    {
        [System.Serializable]
        class Foot
        {
            public TwoBoneIKConstraint  foot;
            [HideInInspector]
            public int                  footAnimatorHash;
            [HideInInspector]
            public Transform            footBone;
            [HideInInspector]
            public Transform            footEffector;
            public float                heightOffset;
            public Quaternion           rotationTweak = Quaternion.identity;
            [HideInInspector]
            public Quaternion           originalRotation = Quaternion.identity;
            [HideInInspector]
            public float                currentWeight;
            [HideInInspector]
            public float                velocityWeight;
        }

        [SerializeField, Tooltip("What's the raycasting offset up, relative to what the humanoid rig states is the foot position."), Header("IK Setup")]
        private List<Foot> feet;
        [SerializeField, Tooltip("What's the raycasting offset up, relative to what the humanoid rig states is the foot position."), Header("Ground Setup")]
        private float raycastOffset = 0.25f;
        [SerializeField, Tooltip("How far down to cast the ray looking for the ground")]
        private float raycastDistance = 0.25f;
        [SerializeField, Tooltip("Radius of the raycast - use spherecast if > 0")]
        private float raycastRadius = 0.0f;
        [SerializeField, Tooltip("What's the max steepness allowed for the hit normal?\nThis is mostly needed if raycastRadius > 0")]
        private float maxSteepness = 45.0f;
        [SerializeField, Tooltip("What is the ground exactly?")]
        private LayerMask groundLayers;
        [SerializeField, Tooltip("This is a hack - if enabled the IK works better on slopes, but worse on planes, see comments in code")]
        private bool hackDualExtensionOffset = false;

        Animator animator;

        void Start()
        {
            animator = GetComponent<Animator>();
            foreach (var foot in feet)
            {
                foot.footAnimatorHash = Animator.StringToHash(foot.foot.name);
#if UNITY_EDITOR
                if (!animator.HasParameter(foot.footAnimatorHash))
                {
                    Debug.LogError($"Animator should have parameter called {foot.foot.name} that defines if that foot should be up (1) or down (0)");
                    foot.footAnimatorHash = 0;
                }
#endif
                foot.footBone = foot.foot.data.tip; 
                foot.footEffector = foot.foot.data.target;
                foot.originalRotation = foot.footBone.rotation;
                foot.foot.weight = 0;
                foot.currentWeight = 0;
                foot.velocityWeight = 0;
                if ((foot.rotationTweak.x == 0.0f) &&
                    (foot.rotationTweak.y == 0.0f) &&
                    (foot.rotationTweak.z == 0.0f) &&
                    (foot.rotationTweak.w == 0.0f))
                    foot.rotationTweak = Quaternion.identity;
            }
        }

        private void FixedUpdate()
        {
            foreach (var foot in feet)
            {
                float targetWeight = 0.0f;
                if (foot.footAnimatorHash != 0)
                {
                    targetWeight = animator.GetFloat(foot.footAnimatorHash);
                }
                
                if (GetGroundHit(foot.footBone.position, out var hit))
                {
                    if (foot.footAnimatorHash == 0) targetWeight = 1.0f;

                    float yOffset = foot.heightOffset;

                    if (foot.footBone.position.y < hit.point.y + foot.heightOffset)
                    {
                        yOffset += Mathf.Max(0, hit.point.y - foot.footBone.position.y);
                        targetWeight = 1.0f;
                    }
                    // If the normal is too steep, use up instead 
                    var hitNormal = (Vector3.Angle(hit.normal, Vector3.up) < maxSteepness) ? hit.normal : Vector3.up;
                    var groundRotation = Quaternion.FromToRotation(Vector3.up, hitNormal);

                    // Adjust position based on ground and tweak position (over-extending because of the targetPositionWeight
                    // This division shouldn't be needed (it's done afterwards), but if we don't use it, it doesn't work as
                    // well on slopes, for some reason, I'm probably missing something.
                    // This way, the feet float a bit above the ground on idle states, etc, but looks fine on slopes...
                    var groundPos = foot.footBone.position;
                    if (hackDualExtensionOffset)
                        groundPos.y = hit.point.y + yOffset / foot.foot.data.targetPositionWeight;
                    else
                        groundPos.y = hit.point.y + yOffset;

                    // Over-extend target because of value on the constraint
                    float deltaY = (groundPos.y - foot.footBone.position.y) / foot.foot.data.targetPositionWeight;
                    groundPos.y = foot.footBone.position.y + deltaY;

                    foot.footEffector.position = groundPos;
                    foot.footEffector.rotation = groundRotation * foot.originalRotation * foot.rotationTweak;
                }
                else
                {
                    targetWeight = 0.0f;

                    foot.footEffector.position = foot.footBone.position;
                    foot.footEffector.rotation = foot.footBone.rotation;
                }

                foot.currentWeight = Mathf.SmoothDamp(foot.currentWeight, targetWeight, ref foot.velocityWeight, 0.05f, float.MaxValue, Time.deltaTime);
                foot.foot.weight = foot.currentWeight;
            }
        }

        bool GetGroundHit(Vector3 sourcePos, out RaycastHit hit)
        {
            if (raycastRadius > 0)
            {
                return Physics.SphereCast(sourcePos + Vector3.up * raycastOffset, raycastRadius, Vector3.down, out hit, raycastDistance, groundLayers);
            }
            return Physics.Raycast(sourcePos + Vector3.up * raycastOffset, Vector3.down, out hit, raycastDistance, groundLayers);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            foreach (var foot in feet)
            {
                Vector3 origin = foot.footBone.position + Vector3.up * raycastOffset;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin + Vector3.down * raycastDistance);

                if (GetGroundHit(foot.footBone.position, out var hit))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(origin, hit.point);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(hit.point, hit.point + hit.normal * raycastDistance * 0.25f);

                    var groundPos = foot.footBone.position;
                    groundPos.y = hit.point.y + foot.heightOffset;
                    foot.footEffector.position = groundPos;
                    foot.footEffector.rotation = foot.originalRotation * Quaternion.FromToRotation(Vector3.up, hit.normal) * foot.rotationTweak;
                }
            }
        }
    }
}


#endif 