using NaughtyAttributes;
using UnityEngine;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This script controls holding a weapon. We can have two scripts to hold two different weapons, or one script to hold a single weapon with both hands.
// This assumes a humanoid rig.
// The weapon doesn't require being parented to the hand, it can be a "loose" object, part of what this script does it to move the weapons grip to the hand.
// Note that the weapon will be moved to the main hand position, but the off-hand will be moved to the off-hand grip position through IK.
// If aiming is enabled, animator parameter "Aim" will be set to true; if mode is both, "RifleAim" will be set to true

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class BasicWeaponHoldIK : MonoBehaviour
    {
        public enum Mode { Right, Left, Both, BothReversed };

        [SerializeField, Tooltip("What layer to target with this IK.\nSet -1 for any")]
        int animationLayer = -1;
        [SerializeField, Tooltip("Which hand to hold the weapon?\nBoth reversed means left-handed grip on the weapon.")]
        private Mode mode = Mode.Right;
        [SerializeField, Tooltip("Transform of the weapon grip")]
        private Transform weaponGrip;                       
        [SerializeField, ShowIf(nameof(isBothHands)), Tooltip("Transform of the weapon off grid (for two-handed weapons)")]
        private Transform weaponOffGrip;
        [SerializeField, Tooltip("Should we flip the transforms around the X axis? Sometimes needed for the left-hand weapon.")]
        private bool flipH;
        [SerializeField, Tooltip("The point from which we calculate aim direction")]
        private Transform weaponSight;
        [SerializeField, Tooltip("Aiming target")]
        private Transform target;
        [SerializeField, Tooltip("Is IK enabled?")]
        private bool ikEnabled = true;
        [SerializeField, Tooltip("Is aiming enabled?")]
        private bool aimEnable = false;
        [SerializeField, Tooltip("Tweak rotation for the aiming - i'd prefer this not to exist, but without the Rigging Animation package it doesn't seem feasible.")]
        private Quaternion aimRotationTweak = Quaternion.identity;  
        [SerializeField, Tooltip("Time to move in/out of aiming mode")]
        private float aimSmoothTime = 0.15f;                        
        [SerializeField, ShowIf(nameof(hasLookIK)), Tooltip("Should we force the look IK?")]
        private bool linkToLook = false;
        [SerializeField, ShowIf(nameof(hasHandControl)), Tooltip("Should we force open/close hand when actually holding something?")]
        private bool linkToHandControl = false;

        private Animator animator;
        private BasicLookAtIK lookIK;
        private BasicHandControl handControl;

        // Main‐hand IK weight
        private float aimWeight = 0f;
        private float aimVelocity = 0f;

        // Off‐hand IK weight (only used when mode == Both or BothReversed)
        private float offHandWeight = 0f;
        private float offHandVelocity = 0f;
        private float offHandSmoothTime = 0.15f;  // You can tweak this independently if desired

        Vector3     prevAimPos;
        Quaternion  prevAimRot;
        Vector3     prevOffPos;
        Quaternion  prevOffRot;

        private bool isBothHands => mode == Mode.Both || mode == Mode.BothReversed;
        private bool hasLookIK => (lookIK != null) || (GetComponent<BasicLookAtIK>() != null);
        private bool hasHandControl => (handControl != null) || (GetComponent<BasicHandControl>() != null);

        bool canAim => (aimEnable) && (target != null) && (weaponGrip != null);

        static int aimHash = Animator.StringToHash("Aim");
        static int aimRifleHash = Animator.StringToHash("RifleAim");

        void Start()
        {
            animator = GetComponent<Animator>();
            lookIK = GetComponent<BasicLookAtIK>();
            handControl= GetComponent<BasicHandControl>();
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

            float targetWeight = canAim ? 1f : 0f;
            if (!ikEnabled)
            {
                targetWeight = 0.0f;
            }

            aimWeight = Mathf.SmoothDamp(aimWeight, targetWeight, ref aimVelocity, aimSmoothTime);

            if (isBothHands)
            {
                offHandWeight = Mathf.SmoothDamp(offHandWeight, targetWeight, ref offHandVelocity, offHandSmoothTime);
            }
            else
            {
                // If not in a two‐handed mode, force offHandWeight back to zero
                offHandWeight = 0f;
                offHandVelocity = 0f;
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

            if ((linkToLook) && (lookIK != null))
            {
                if (aimEnable)
                    lookIK.ForceLook(target);
                else
                    lookIK.ForceLook(null);
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;
            if ((layerIndex != animationLayer) && (animationLayer != -1)) return;

            AvatarIKGoal mainIK = GetMainHandIK();

            if ((weaponSight == null) || (target == null) || (weaponGrip == null))
            {
                // We need to set some IK targets anyway, so that we can animate back
                animator.SetIKPosition(mainIK, prevAimPos);
                animator.SetIKRotation(mainIK, prevAimRot);
                if (isBothHands)
                {
                    AvatarIKGoal offIK = GetOffHandIK();

                    animator.SetIKPosition(offIK, prevOffPos);
                    animator.SetIKRotation(offIK, prevOffRot);
                }
                return;
            }   

            // Compute aim direction & rotation
            Vector3 aimDir = (target.position - weaponSight.position).normalized;
            Quaternion aimRot = Quaternion.LookRotation(aimDir, Vector3.up) * aimRotationTweak;
            Vector3 aimPos = weaponSight.position + aimDir * 0.1f;

            animator.SetIKPositionWeight(mainIK, aimWeight);
            animator.SetIKRotationWeight(mainIK, aimWeight);
            animator.SetIKPosition(mainIK, aimPos);
            animator.SetIKRotation(mainIK, aimRot);

            prevAimPos = aimPos;
            prevAimRot = aimRot;

            // --- OFF HAND IK (only if in two‐handed mode) ---
            if (isBothHands && weaponOffGrip != null)
            {
                AvatarIKGoal offIK = GetOffHandIK();

                // Position + rotation come straight from weaponOffGrip
                Vector3 offPos = weaponOffGrip.position;
                Quaternion offRot = weaponOffGrip.rotation;

                animator.SetIKPositionWeight(offIK, offHandWeight);
                animator.SetIKRotationWeight(offIK, offHandWeight);
                animator.SetIKPosition(offIK, offPos);
                animator.SetIKRotation(offIK, offRot);

                prevOffPos = offPos;
                prevOffRot = offRot;
            }
        }

        HumanBodyBones GetMainHand()
        {
            // For BothReversed, “main” is still the hand on the opposite side of the weapon
            return (mode == Mode.Left || mode == Mode.BothReversed)
                ? HumanBodyBones.LeftHand
                : HumanBodyBones.RightHand;
        }

        HumanBodyBones GetOffHand()
        {
            if (mode == Mode.Right || mode == Mode.Both)
                return HumanBodyBones.LeftHand;
            else // Mode.Left or Mode.BothReversed
                return HumanBodyBones.RightHand;
        }

        AvatarIKGoal GetMainHandIK()
        {
            return (mode == Mode.Left || mode == Mode.BothReversed)
                ? AvatarIKGoal.LeftHand
                : AvatarIKGoal.RightHand;
        }

        AvatarIKGoal GetOffHandIK()
        {
            if (mode == Mode.Right || mode == Mode.Both)
                return AvatarIKGoal.LeftHand;
            else // Mode.Left or Mode.BothReversed
                return AvatarIKGoal.RightHand;
        }
    }
}
