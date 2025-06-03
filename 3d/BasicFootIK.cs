using System.Collections.Generic;
using UnityEngine;

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
    public class BasicFootIK : MonoBehaviour
    {
        [SerializeField, Tooltip("What layer to target with this IK.\nSet -1 for any")]
        int animationLayer = -1;
        [SerializeField, Tooltip("What's the raycasting offset up, relative to what the humanoid rig states is the foot position.")] 
        private float raycastOffset = 0.25f;
        [SerializeField, Tooltip("How far down to cast the ray looking for the ground")] 
        private float raycastDistance = 0.25f;
        [SerializeField, Tooltip("What is the ground exactly?")] 
        private LayerMask groundLayers;
        [SerializeField, Tooltip("After finding the ground position, how much to add to that position to set the foot IK")] 
        private Vector3 footPositionTweak = Vector3.zero;

#if UNITY_EDITOR
        class DebugIKInfo
        {
            public bool hit;
            public Vector3 footPos;
            public RaycastHit hitInfo;
            public float weight;
        }
        Dictionary<AvatarIKGoal, DebugIKInfo> debugIKInfo;
#endif

        Animator animator;
        static int leftFootHash = Animator.StringToHash("LeftFoot");
        static int rightFootHash = Animator.StringToHash("RightFoot");

        void Start()
        {
            animator = GetComponent<Animator>();
#if UNITY_EDITOR
            if (!animator.HasParameter("LeftFoot"))
            {
                Debug.LogError($"Animator should have parameter called LeftFoot that defines if the left foot should be up (1) or down (0)");
            }
            if (!animator.HasParameter("RightFoot"))
            {
                Debug.LogError($"Animator should have parameter called RightFoot that defines if the left foot should be up (1) or down (0)");
            }

            var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;

            if (controller != null)
            {
                bool anyIKPass = false;
                for (int i = 0; i < controller.layers.Length; i++)
                {
                    anyIKPass |= controller.layers[i].iKPass;
                }
                if (!anyIKPass)
                {
                    Debug.LogError($"No IK Pass is detected on animator! Foot IK will not work.");
                }
            }

            debugIKInfo = new();
            debugIKInfo[AvatarIKGoal.RightFoot] = new();
            debugIKInfo[AvatarIKGoal.LeftFoot] = new();
#endif
        }

        void OnAnimatorIK(int layerIndex)
        {
            if ((layerIndex != animationLayer) && (animationLayer != -1)) return;

            RunIK(AvatarIKGoal.LeftFoot, raycastOffset, animator.GetFloat(leftFootHash));
            RunIK(AvatarIKGoal.RightFoot, raycastOffset, animator.GetFloat(rightFootHash));
        }

        void RunIK(AvatarIKGoal foot, float offsetUp, float footWeight)
        {
            animator.SetIKPositionWeight(foot, footWeight);
            animator.SetIKRotationWeight(foot, footWeight);

            if (GetGroundHit(foot, offsetUp, out var hit))
            {
#if UNITY_EDITOR
                debugIKInfo[foot].hit = true;
                debugIKInfo[foot].hitInfo = hit;
                debugIKInfo[foot].weight = footWeight;
#endif

                animator.SetIKPosition(foot, hit.point + footPositionTweak);
                Quaternion footRotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;
                animator.SetIKRotation(foot, footRotation);
            }
            else
            {
#if UNITY_EDITOR
                debugIKInfo[foot].hit = false;
#endif
            }
        }

        bool GetGroundHit(AvatarIKGoal foot, float offsetUp, out RaycastHit hit)
        {
            Vector3 sourcePos = animator.GetIKPosition(foot) + Vector3.up * offsetUp;
#if UNITY_EDITOR
            debugIKInfo[AvatarIKGoal.LeftFoot].footPos = sourcePos;
#endif
            return Physics.Raycast(sourcePos, Vector3.down, out hit, raycastDistance, groundLayers);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (debugIKInfo != null)
            {
                foreach (var foot in debugIKInfo)
                {
                    if (foot.Value.weight > 0.0f)
                    {
                        Gizmos.color = Color.yellow.ChangeAlpha(foot.Value.weight);
                        Gizmos.DrawLine(foot.Value.footPos,
                                        foot.Value.footPos + Vector3.down * raycastDistance * 1.5f);
                        if (foot.Value.hit)
                        {
                            Gizmos.color = Color.green.ChangeAlpha(foot.Value.weight);
                            Gizmos.DrawSphere(foot.Value.hitInfo.point, 0.05f);
                            Gizmos.DrawLine(foot.Value.hitInfo.point, foot.Value.hitInfo.point + foot.Value.hitInfo.normal * 0.2f);
                        }
                    }
                }
            }
        }
#endif
    }

}