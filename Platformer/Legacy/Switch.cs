using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityCommon.Legacy
{

    public class Switch : Interactable
    {
        public bool initialState;
        public bool enableToggle = true;
        public AudioSource toggleSound;
        public ActivatedComponent[] activators;
        [ShowIf("OnlyOneActivator")]
        public bool twoWays;

        bool OnlyOneActivator()
        {
            if (activators != null) return activators.Length == 1;

            return false;
        }

        bool state;
        Animator anim;

        public void Start()
        {
            anim = GetComponent<Animator>();

            state = initialState;
            if (anim) anim.SetBool("Switch", state);
        }

        public void Update()
        {
            if ((twoWays) && (OnlyOneActivator()))
            {
                state = activators[0].active;
                if (anim) anim.SetBool("Switch", state);
            }
        }

        override public void Interact()
        {
            if (enableToggle)
            {
                state = !state;
                if (anim) anim.SetBool("Switch", state);
                foreach (var ac in activators)
                {
                    ac.active = !ac.active;
                }
                if (toggleSound) toggleSound.Play();
            }
            else
            {
                if (state == initialState)
                {
                    state = !state;
                    if (anim) anim.SetBool("Switch", state);
                    foreach (var ac in activators)
                    {
                        ac.active = state;
                    }
                    if (toggleSound) toggleSound.Play();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (activators != null)
            {
                Gizmos.color = Color.green;
                foreach (var ac in activators)
                {
                    Gizmos.DrawLine(transform.position, ac.transform.position);
                }
            }
        }
    }

}