using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class BasicHoldIK : MonoBehaviour
    {
        public enum Mode { Right, Left, Both, BothReversed };

        [SerializeField]
        private Mode mode = Mode.Right;

        [SerializeField]
        private Transform weaponGrip;                       // Will be moved to match the main hand

        [SerializeField, ShowIf(nameof(isBothHands))]
        private Transform weaponOffGrip;                    // Will be moved to match the off‐hand

        [SerializeField]
        private bool flipH;

        [SerializeField]
        private Transform weaponSight;                      // The point from which we calculate aim direction

        [SerializeField]
        private Transform target;                           // The aiming target

        [SerializeField]
        private bool aimEnable = false;                     // Toggle for engaging IK

        [SerializeField]
        private Quaternion aimRotationTweak = Quaternion.identity;  // Fine‐tune aiming rotation

        [SerializeField]
        private float aimSmoothTime = 0.15f;                        // Smoothing time for main‐hand weight

        [SerializeField, ShowIf(nameof(hasLookIK))]
        private bool linkToLook = false;                   // Should we force the look IK?

        private Animator animator;
        private BasicLookAtIK lookIK;

        // Main‐hand IK weight
        private float aimWeight = 0f;
        private float aimVelocity = 0f;

        // Off‐hand IK weight (only used when mode == Both or BothReversed)
        private float offHandWeight = 0f;
        private float offHandVelocity = 0f;
        private float offHandSmoothTime = 0.15f;  // You can tweak this independently if desired

        private bool isBothHands => mode == Mode.Both || mode == Mode.BothReversed;
        private bool hasLookIK => GetComponent<BasicLookAtIK>() != null;

        void Start()
        {
            animator = GetComponent<Animator>();
            lookIK = GetComponent<BasicLookAtIK>();
        }

        void Update()
        {
            if (animator == null || weaponGrip == null)
                return;

            // 1) Smoothly blend main‐hand aimWeight toward 1 or 0 depending on aimEnable
            float targetWeight = aimEnable ? 1f : 0f;
            aimWeight = Mathf.SmoothDamp(aimWeight, targetWeight, ref aimVelocity, aimSmoothTime);

            // 2) Smoothly blend offHandWeight toward 1 or 0 when in Both/BothReversed mode
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

            // 3) Move weaponGrip to match the main hand bone
            HumanBodyBones mainBone = GetMainHand();
            Transform mainHand = animator.GetBoneTransform(mainBone);
            if (mainHand != null)
            {
                weaponGrip.position = mainHand.position;
                weaponGrip.rotation = mainHand.rotation;
                weaponGrip.localScale = flipH ? new Vector3(-1, 1, 1) : Vector3.one;
            }

            // 4) If linkToLook is enabled and we have a BasicLookAtIK, force it to look at the target
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
            if (animator == null || weaponSight == null || target == null || weaponGrip == null)
                return;

            // --- MAIN HAND IK (exactly as before) ---
            AvatarIKGoal mainIK = GetMainHandIK();

            // Compute aim direction & rotation
            Vector3 aimDir = (target.position - weaponSight.position).normalized;
            Quaternion aimRot = Quaternion.LookRotation(aimDir, Vector3.up);
            Vector3 aimPos = weaponSight.position + aimDir * 0.1f;

            animator.SetIKPositionWeight(mainIK, aimWeight);
            animator.SetIKRotationWeight(mainIK, aimWeight);
            animator.SetIKPosition(mainIK, aimPos);
            animator.SetIKRotation(mainIK, aimRot * aimRotationTweak);

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
