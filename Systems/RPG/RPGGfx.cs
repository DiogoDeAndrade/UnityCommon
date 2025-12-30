using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{
    public class RPGGfx : MonoBehaviour
    {
        [SerializeField]
        private Animator            _primaryAnimator;
        [SerializeField]
        private Animator[]          _animators;
        [SerializeField]
        private SpriteRenderer[]    _spriteRenderers;
        [SerializeField]
        private SpriteEffect[]      _spriteEffects;
        [SerializeField]
        private SpriteRenderer[]    _shadowRenderers;

        public Animator primaryAnimator => _primaryAnimator;
        public Animator[] animators { get => _animators; }

        public void SetAnimator(RuntimeAnimatorController controller)
        {
            foreach (var animator in _animators)
            {
                animator.runtimeAnimatorController = controller;
            }
        }

        public void SetShadow(bool enableShadow, Vector3 scale)
        {
            foreach (var sr in _shadowRenderers)
            {
                sr.enabled = enableShadow;
                sr.transform.localScale = scale;
            }
        }

        public void DesyncAnimation()
        {
            StartCoroutine(DesyncAnimationCR());
        }

        private IEnumerator DesyncAnimationCR()
        {
            yield return null;

            foreach (var animator in animators)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                animator.Play(state.shortNameHash, 0, Random.value);
            }
        }

        public void SetBool(string name, bool v)
        {
            foreach (var animator in animators)
            {
                animator.SetBool(name, v);
            }
        }

        public void SetInteger(string name, int v)
        {
            foreach (var animator in animators)
            {
                animator.SetInteger(name, v);
            }
        }

        public void SetFloat(string name, float v)
        {
            foreach (var animator in animators)
            {
                animator.SetFloat(name, v);
            }
        }

        public void SetTrigger(string name)
        {
            foreach (var animator in animators)
            {
                animator.SetTrigger(name);
            }
        }

        public void FlashColor(float duration, Color color)
        {
            foreach (var sr in _spriteEffects)
            {
                sr.FlashColor(0.2f, color);
            }
        }

        public void SetOutline(float width, Color color)
        {
            foreach (var sr in _spriteEffects)
            {
                sr.SetOutline(width, color);
            }
        }

        public void SetSprite(Sprite displaySprite, Color displaySpriteColor)
        {
            foreach (var sr in _spriteRenderers)
            {
                sr.enabled = (displaySprite != null);
                sr.sprite = displaySprite;
                sr.color = displaySpriteColor;
            }
        }
    }
}
