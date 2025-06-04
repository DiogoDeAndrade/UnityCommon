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
// For best results, have several MultiAimConstraints under the aim rig, one for the hand, another for the forearm and another for the upper arm.
// I like having different weights to them (1, 0.75 and 0.5, respectively) to get a more natural aiming pose.
// The hand constraint can have the World Up axis set to SceneUp, it usually has a better result.
// The result is not 100% perfect, but it works well enough for most cases. When shooting, cheat by making the shot go in the direction of the target object and not the
// barrel direction, they should be close enough, but not exactly the same.

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class AnimRigWeaponHoldIK : MonoBehaviour
    {
        public enum Mode { Right, Left, Both, BothReversed };

        [SerializeField, Tooltip("Which hand to hold the weapon?\nBoth reversed means left-handed grip on the weapon.")]
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
        [SerializeField, Tooltip("Object that the constraint is following")]
        private Transform   targetObjectIK;
        [SerializeField, ShowIf(nameof(isBothHands)), Tooltip("Offhand IK rig")]
        private Rig         offHandAimIK;
        [SerializeField, Header("Aiming"), Tooltip("Enable aiming")]
        private bool        aimEnable;
        [SerializeField, Tooltip("Target object")]
        private Transform   target;
        [SerializeField, Tooltip("Transition in/out time for aiming")]
        private float transitionSpeed = 0.15f;
        [SerializeField, ShowIf(nameof(hasLookIK)), Tooltip("Should we force the look IK?"), Header("Links")]
        private bool linkToLook = false;
        [SerializeField, ShowIf(nameof(hasHandControl)), Tooltip("Should we force open/close hand when actually holding something?")]
        private bool linkToHandControl = false;

        private bool isBothHands => mode == Mode.Both || mode == Mode.BothReversed;
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
                    case Mode.BothReversed:
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

            HumanBodyBones mainBone = GetMainHand();
            Transform mainHand = animator.GetBoneTransform(mainBone);
            if (mainHand != null)
            {
                weaponGrip.position = mainHand.position;
                weaponGrip.rotation = mainHand.rotation;
                weaponGrip.localScale = flipH ? new Vector3(-1, 1, 1) : Vector3.one;
            }

            if (canAim)
            {
                mainHandAimIK.weight = Mathf.SmoothDamp(mainHandAimIK.weight, 1.0f, ref weightVelocity, transitionSpeed, float.MaxValue, Time.deltaTime);

                var localAimAxis = gripAimConstraint.data.aimAxis;
                var axis = GetAxisFromEnum(localAimAxis);
                
                var adjustedTarget = ComputeVirtualTarget(weaponGrip, barrelObject, target.position, axis);

                targetObjectIK.position = adjustedTarget;
            }
            else
            {
                mainHandAimIK.weight = Mathf.SmoothDamp(mainHandAimIK.weight, 0.0f, ref weightVelocity, transitionSpeed, float.MaxValue, Time.deltaTime);
            }

            if (isBothHands)
            {
                offHandAimIK.weight = mainHandAimIK.weight;
            }

            if ((linkToLook) && (lookIK != null))
            {
                if (aimEnable)
                    lookIK.ForceLook(target);
                else
                    lookIK.ForceLook(null);
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

        HumanBodyBones GetMainHand()
        {
            // For BothReversed, “main” is still the hand on the opposite side of the weapon
            return (mode == Mode.Left || mode == Mode.BothReversed)
                ? HumanBodyBones.LeftHand
                : HumanBodyBones.RightHand;
        }
    }
}

#endif