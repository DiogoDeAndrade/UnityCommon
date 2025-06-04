#if UNITY_3D_ANIMATION_RIG_AVAILABLE

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This script controls the feet.
// It assumes there are animator parameters called "LeftFoot" and "RightFoot" that define if the respective feet should be up (1) or down (0).
// This makes sense to be driven by the animations themselves, so after you import an animation (from an FBX), you can add a Curve with that name
// that can be used to drive that value
// The tweak value will be added to the foot position, and it's kind of a hack to make sure the feet are not floating above the ground on some types of avatars
// This all assumes the animations are Humanoid type.

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
            public Vector3              positionTweak;
            public Quaternion           rotationTweak = Quaternion.identity;
            [HideInInspector]
            public Quaternion           originalRotation = Quaternion.identity;
            [HideInInspector]
            public Vector3              animatedPosition;
            [HideInInspector]
            public Quaternion           animatedRotation;
        }

        [SerializeField, Tooltip("What's the raycasting offset up, relative to what the humanoid rig states is the foot position."), Header("IK Setup")]
        private List<Foot> feet;
        [SerializeField, Tooltip("What's the raycasting offset up, relative to what the humanoid rig states is the foot position."), Header("Ground Setup")]
        private float raycastOffset = 0.25f;
        [SerializeField, Tooltip("How far down to cast the ray looking for the ground")]
        private float raycastDistance = 0.25f;
        [SerializeField, Tooltip("What is the ground exactly?")]
        private LayerMask groundLayers;
        [SerializeField, Tooltip("After finding the ground position, how much to add to that position to set the foot IK")]
        private Vector3 footPositionTweak = Vector3.zero;

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
            }
        }

        private void OnAnimatorMove()
        {
            // Cache values of the animator
            foreach (var foot in feet)
            {
                foot.animatedPosition = foot.footBone.position;
                foot.animatedRotation = foot.footBone.rotation;
            }
        }

        private void LateUpdate()
        {
            foreach (var foot in feet)
            {
                float w = 0.0f;
                if (foot.footAnimatorHash != 0)
                {
                    w = animator.GetFloat(foot.footAnimatorHash);
                }
                
                if (GetGroundHit(foot.footBone.position, out var hit))
                {
                    if (foot.footAnimatorHash == 0) w = 1.0f;

                    var groundPos = foot.animatedPosition;
                    groundPos.y = hit.point.y;
                    foot.footEffector.position = groundPos + foot.positionTweak;
                    foot.footEffector.rotation = foot.originalRotation * Quaternion.FromToRotation(Vector3.up, hit.normal) * foot.rotationTweak;
                }
                else
                {
                    w = 0.0f;
                }

                foot.foot.weight = w;
            }
        }

        bool GetGroundHit(Vector3 sourcePos, out RaycastHit hit)
        {
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
                    groundPos.y = hit.point.y;
                    foot.footEffector.position = groundPos + foot.positionTweak;
                    foot.footEffector.rotation = foot.originalRotation * Quaternion.FromToRotation(Vector3.up, hit.normal) * foot.rotationTweak;
                }
            }
        }
    }
}


#endif 