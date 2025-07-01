using System;
using UnityEngine;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This script controls the hand grip.
// It assumes two parameters on the animator: "OpenL" and "OpenR", and two animation layers "LeftHand" and "RightHand"
// When the mode for a specific hand is set to None, the weight of the corresponding layer is set to 0, otherwise it is set to 1.
// Otherwise, we set the value of OpenL or OpenR to true for open hand, and false for closed hand.

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class BasicHandControl : MonoBehaviour
    {
        public enum HandMode { None, Open, Close };

        [SerializeField] private HandMode rightHandMode = HandMode.None;
        [SerializeField] private HandMode leftHandMode = HandMode.None;

        private Animator animator;
        private int rightHandLayerIndex;
        private int leftHandLayerIndex;

        private static readonly int OpenR = Animator.StringToHash("OpenR");
        private static readonly int OpenL = Animator.StringToHash("OpenL");

        void Start()
        {
            animator = GetComponent<Animator>();
            // Cache layer indices
            rightHandLayerIndex = animator.GetLayerIndex("RightHand");
            leftHandLayerIndex = animator.GetLayerIndex("LeftHand");
        }

        void Update()
        {
            SetupHand(rightHandLayerIndex, rightHandMode, OpenR);
            SetupHand(leftHandLayerIndex, leftHandMode, OpenL);
        }

        void SetupHand(int layerIndex, HandMode mode, int openHashId)
        {
            // RIGHT HAND
            if (layerIndex >= 0)
            {
                if (mode == HandMode.None)
                {
                    animator.SetLayerWeight(layerIndex, 0f);
                }
                else
                {
                    animator.SetLayerWeight(layerIndex, 1f);

                    // Set "OpenR" parameter: 1 if Open, 0 if Close
                    animator.SetBool(openHashId, mode == HandMode.Open);
                }
            }
        }

        public void OpenRightHand(bool set, bool v)
        {
            if (set)
            {
                if (v)
                    rightHandMode = HandMode.Open;
                else
                    rightHandMode = HandMode.Close;
            }
            else
            {
                rightHandMode = HandMode.None;
            }
        }

        public void OpenLeftHand(bool set, bool v)
        {
            if (set)
            {
                if (v)
                    leftHandMode = HandMode.Open;
                else
                    leftHandMode = HandMode.Close;
            }
            else
            {
                leftHandMode = HandMode.None;
            }
        }
    }
}
