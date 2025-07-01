#if UNITY_3D_ANIMATION_RIG_AVAILABLE

using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Animations.Rigging;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This script controls holding a weapon. We can have two scripts to hold two different weapons, or one script to hold a single weapon with both hands.
// This uses the AnimationRig package.
// The weapon doesn't require being parented to the hand, it can be a "loose" object, part of what this script does it to move the weapons grip to the hand.
// Note that the weapon will be moved to the main hand position, but the off-hand will be moved to the off-hand grip position through IK (eventually - not working yet).
// If aiming is enabled, animator parameter "Aim" will be set to true; if mode is both, "RifleAim" will be set to true
// Single weapon setup:
//   For best results, have several MultiAimConstraints under the aim rig, one for the hand, another for the forearm and another for the upper arm.
//   I like having different weights to them (1, 0.75 and 0.5, respectively) to get a more natural aiming pose.
//   The hand constraint can have the World Up axis set to SceneUp, it usually has a better result.
// Two-handed rifle setup:
//   Use a Two-Bone IK setup for the off-hand to hold the weapon off grip. This is only activated when the weapon is aiming.
//   For the aiming itself, use the same MultiAimConstraints on the hand as for the single weapon.
//   Rifles and machine guns work best with turning also the body, so a Multi-Aim constraint for the spine is recommended.
//   Depending on the animation used, you might need to adjust the body. For example, on my test case my animation was 35 degrees off the forward Z for the forward, so I added
//   a second target point for the spine, and used a HelperRotatePointAroundPoint to rotate the target 35 degrees relative to the player.
// The result is not 100% perfect, but it works well enough for most cases. When shooting, cheat by making the shot go in the direction of the target object and not the
// barrel direction, they should be close enough, but not exactly the same.

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class AnimRigWeaponHoldIK : MonoBehaviour
    {
        public enum Mode { Right, Left, Both };
        public enum LookMode { None, LookAt, Disable };

        [SerializeField, Tooltip("Which hand to hold the weapon?")]
        private Mode mode = Mode.Right;
        [SerializeField, Tooltip("Transform of the weapon grip")]
        private Transform weaponGrip;
        [SerializeField, Tooltip("Transform of the weapon barrel")]
        private Transform barrelObject;
        [SerializeField, ShowIf(nameof(isBothHands)), Tooltip("Transform of the weapon off grid (for two-handed weapons)")]
        private Transform weaponOffGrip;
        [SerializeField, Tooltip("Should we flip the transforms around the X axis? Sometimes needed for the left-hand weapon.")]
        private bool flipH;
        [SerializeField, Header("IK Config"), Tooltip("Aim IK Rig")]
        private Rig         mainHandAimIK;
        [SerializeField, Tooltip("Hand grip aim constraint")]
        private MultiAimConstraint gripAimConstraint;
        [SerializeField, Tooltip("Main hand bone")]
        private Transform   mainHandBone;
        [SerializeField, Tooltip("Object that the constraint is following")]
        private Transform   targetObjectIK;
        [SerializeField, Tooltip("Target for the off-hand")]
        private Transform targetOffhand;
        [SerializeField, Header("Aiming"), Tooltip("Enable aiming")]
        private bool        aimEnable;
        [SerializeField, Tooltip("Should try to solve IK to adjust the aim to account for barrel?")]
        private bool        adjustAimToBarrel = true;
        [SerializeField, Tooltip("Target object")]
        private Transform   target;
        [SerializeField, Tooltip("Transition in/out time for aiming")]
        private float transitionSpeed = 0.15f;
        [SerializeField, ShowIf(nameof(hasLookIK)), Tooltip("Should we force the look IK?"), Header("Links")]
        private LookMode linkToLook = LookMode.Disable;
        [SerializeField, ShowIf(nameof(hasHandControl)), Tooltip("Should we force open/close hand when actually holding something?")]
        private bool linkToHandControl = false;

        private bool isBothHands => mode == Mode.Both;
        private bool hasLookIK => (lookIK != null) || (GetComponent<AnimRigLookAtIK>() != null);
        private bool hasHandControl => (handControl != null) || (GetComponent<BasicHandControl>() != null);

        bool canAim => (aimEnable) && (target != null) && (weaponGrip != null);

        static int aimHash = Animator.StringToHash("Aim");
        static int aimRifleHash = Animator.StringToHash("RifleAim");

        private Animator animator;
        private BasicHandControl handControl;
        private AnimRigLookAtIK lookIK;
        private float weightVelocity;
        private Vector3 rLocal, fLocal;

        void Start()
        {
            animator = GetComponent<Animator>();
            lookIK = GetComponent<AnimRigLookAtIK>();
            handControl = GetComponent<BasicHandControl>();
            PrecomputeBarrelData();
        }

        // This needs to be called when the barrel object is set or changed
        void PrecomputeBarrelData()
        {
            rLocal = weaponGrip.InverseTransformPoint(barrelObject.position);
            fLocal = weaponGrip.InverseTransformDirection(barrelObject.forward).normalized;
        }

        void Update()
        {
            if (animator == null) return;

            if ((linkToHandControl) && (handControl))
            {
                switch (mode)
                {
                    case Mode.Right:
                        handControl.OpenRightHand(weaponGrip != null, weaponGrip == null);
                        break;
                    case Mode.Left:
                        handControl.OpenLeftHand(weaponGrip != null, weaponGrip == null);
                        break;
                    case Mode.Both:
                        handControl.OpenRightHand(weaponGrip != null, weaponGrip == null);
                        handControl.OpenLeftHand(weaponGrip != null, weaponGrip == null);
                        break;
                    default:
                        break;
                }
            }

            animator.SetBool(aimHash, canAim);
            animator.SetBool(aimRifleHash, canAim && isBothHands);

            if (weaponGrip == null)
                return;

            if (mainHandBone != null)
            {
                weaponGrip.position = mainHandBone.position;
                weaponGrip.rotation = mainHandBone.rotation;
                weaponGrip.localScale = flipH ? new Vector3(-1, 1, 1) : Vector3.one;
            }

            if (canAim)
            {
                mainHandAimIK.weight = Mathf.SmoothDamp(mainHandAimIK.weight, 1.0f, ref weightVelocity, transitionSpeed, float.MaxValue, Time.deltaTime);

                if (adjustAimToBarrel)
                {
                    var localAimAxis = gripAimConstraint.data.aimAxis;
                    var axis = GetAxisFromEnum(localAimAxis);

                    var adjustedTarget = ComputeVirtualTarget(weaponGrip, barrelObject, target.position, axis);

                    targetObjectIK.position = adjustedTarget;
                }
                else
                {
                    targetObjectIK.position = target.position;
                }

                if (isBothHands)
                {
                    targetOffhand.position = weaponOffGrip.position;
                    targetOffhand.rotation = weaponOffGrip.rotation;
                    targetOffhand.localScale = flipH ? new Vector3(-1, 1, 1) : Vector3.one;
                }
            }
            else
            {
                mainHandAimIK.weight = Mathf.SmoothDamp(mainHandAimIK.weight, 0.0f, ref weightVelocity, transitionSpeed, float.MaxValue, Time.deltaTime);
            }

            switch (linkToLook)
            {
                case LookMode.None:
                    break;
                case LookMode.LookAt:
                    if (aimEnable)
                        lookIK.ForceLook(target);
                    else
                        lookIK.ForceLook(null);
                    break;
                case LookMode.Disable:
                    if (aimEnable)
                        lookIK.DisableLook();
                    else
                        lookIK.EnableLook();
                    break;
                default:
                    break;
            }
        }

        private Vector3 GetAxisFromEnum(MultiAimConstraintData.Axis localAimAxis)
        {
            switch (localAimAxis)
            {
                case MultiAimConstraintData.Axis.X: return Vector3.right;
                case MultiAimConstraintData.Axis.X_NEG: return Vector3.left;
                case MultiAimConstraintData.Axis.Y: return Vector3.up;
                case MultiAimConstraintData.Axis.Y_NEG: return Vector3.down;
                case MultiAimConstraintData.Axis.Z: return Vector3.forward;
                case MultiAimConstraintData.Axis.Z_NEG:return Vector3.back;
            }
            return Vector3.forward;
        }

        // Does some iterations to adjust the target position based on the grip and barrel transforms - There must be a better way to do this...
        Vector3 ComputeVirtualTarget(Transform grip, Transform barrel, Vector3 realTarget, Vector3 gripLocalAimAxis, int iterations = 2)
        {
            Vector3 virtualTarget = realTarget; 

            for (int i = 0; i < iterations; ++i)
            {
                Vector3 dir = (virtualTarget - grip.position).normalized;

                Quaternion R = Quaternion.FromToRotation(grip.up, dir);

                Vector3 barrelPosAfter = grip.position + R * rLocal;
                Vector3 barrelFwdAfter = R * fLocal;

                virtualTarget = realTarget - R * rLocal;
            }
            return virtualTarget;
        }

    }
}

#endif