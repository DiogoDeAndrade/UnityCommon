using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UC
{

    public abstract class UIDiscreteSOOption : ScriptableObject
    {
        public abstract string GetDisplayName();
    }

    public class UIDiscreteSOSelector<T> : UIControl<T> where T : UIDiscreteSOOption, IEquatable<T>
    {
        [SerializeField] protected Image leftArrow;
        [SerializeField] protected Image rightArrow;
        [SerializeField] protected T[] options;
        [SerializeField] protected TextMeshProUGUI optionName;
        [SerializeField] protected bool randomInitialSelection;
        [SerializeField, HideIf(nameof(randomInitialSelection))]
        protected int initialSelection = 0;

        int selectedItem = 0;

        protected void Awake()
        {
            if (randomInitialSelection)
                selectedItem = UnityEngine.Random.Range(0, options.Length);
            else
                selectedItem = initialSelection;

            _value = _prevValue = options[selectedItem];
        }

        protected override void Update()
        {
            base.Update();

            if (optionName)
            {
                optionName.text = options[selectedItem].GetDisplayName();
            }
        }

        public override void NotifyEnable()
        {
            base.NotifyEnable();

            if (leftArrow) leftArrow.enabled = true;
            if (rightArrow) rightArrow.enabled = true;
        }

        public override void NotifyDisable()
        {
            base.NotifyDisable();

            if (leftArrow) leftArrow.enabled = false;
            if (rightArrow) rightArrow.enabled = false;
        }

        public override void MoveHorizontal(float dz, bool isDown)
        {
            if (!isDown) return;

            if (dz > 0.0f)
            {
                selectedItem = (selectedItem + 1) % options.Length;
                ChangeValue(options[selectedItem]);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);
            }
            else if (dz < 0.0f)
            {
                selectedItem--;
                if (selectedItem < 0) selectedItem = options.Length - 1;
                ChangeValue(options[selectedItem]);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);
            }
        }

        public override void ChangeValue(T newValue)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (newValue == options[i])
                {
                    selectedItem = i;
                    break;
                }
            }

            base.ChangeValue(newValue);
        }
    }
}
