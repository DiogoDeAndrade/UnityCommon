using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

namespace UC
{

    public class UIDiscreteColorSelector : UIControl<Color>
    {
        [SerializeField] protected Image leftArrow;
        [SerializeField] protected Image rightArrow;
        [SerializeField] protected Color[] colors;
        [SerializeField] protected Image colorIndicator;
        [SerializeField] protected bool randomInitialSelection;
        [SerializeField, HideIf(nameof(randomInitialSelection))]
        protected int initialSelection = 0;

        int selectedColor = 0;

        protected void Awake()
        {
            if (randomInitialSelection)
                selectedColor = Random.Range(0, colors.Length);
            else
                selectedColor = initialSelection;

            _value = _prevValue = colors[selectedColor];
        }

        protected override void Update()
        {
            base.Update();

            if (colorIndicator)
            {
                colorIndicator.color = colors[selectedColor];
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
                selectedColor = (selectedColor + 1) % colors.Length;
                ChangeValue(colors[selectedColor]);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);
            }
            else if (dz < 0.0f)
            {
                selectedColor--;
                if (selectedColor < 0) selectedColor = colors.Length - 1;
                ChangeValue(colors[selectedColor]);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);
            }
        }

        public override void ChangeValue(Color newValue)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < colors.Length; i++)
            {
                float d = colors[i].DistanceRGBA(newValue);
                if (d < minDist)
                {
                    minDist = d;
                    selectedColor = i;
                }
            }

            base.ChangeValue(newValue);
        }
    }
}
