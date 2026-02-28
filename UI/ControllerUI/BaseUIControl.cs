using NaughtyAttributes;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UC
{

    public class BaseUIControl : MonoBehaviour
    {
        public enum HighlightMode { ImageEnable, ColorSwitch, Animator, Outline };

        public delegate void OnSelect(BaseUIControl newCntrol, BaseUIControl prevControl);
        public delegate void OnDeselect(BaseUIControl control);
        public delegate void OnChange(BaseUIControl control);
        public delegate void OnInteract(BaseUIControl control);
        public delegate void OnUIEnableToggle(bool value, BaseUIControl control);
        public delegate bool CanSelect(BaseUIControl control);

        [SerializeField] 
        private HighlightMode   highlightMode = HighlightMode.ImageEnable;
        [SerializeField, ShowIf(nameof(needsHighlighterImage))] 
        protected Image         highlighterImage;
        [SerializeField, ShowIf(nameof(needsHighlighterImageEffect))]
        protected UIImageEffect highlighterImageEffect;
        [SerializeField, ShowIf(nameof(isOutline))]
        protected Color         outlineColor = Color.yellow;
        [SerializeField, ShowIf(nameof(isOutline)), Min(0.0f)]
        protected float         outlineWidth = 1.0f;
        [SerializeField] 
        protected TextMeshProUGUI highlighterText;
        [SerializeField, ShowIf(nameof(needHighlightColor))] 
        protected Color         highlightColor;
        [SerializeField] 
        protected BaseUIControl _navUp;
        [SerializeField] 
        protected BaseUIControl _navDown;
        [SerializeField] 
        protected BaseUIControl _navLeft;
        [SerializeField] 
        protected BaseUIControl _navRight;
        [SerializeField] 
        protected AudioClip changeSnd;

        protected UIGroup   parentGroup;
        Color               defaultTextColor;
        Color               defaultImageColor;
        CanvasGroup         canvasGroup;
        Animator            animator;

        private bool needHighlightColor => (highlighterText != null) || (highlightMode == HighlightMode.ColorSwitch);
        private bool needsHighlighterImage => (highlightMode == HighlightMode.ImageEnable);
        private bool needsHighlighterImageEffect => (highlightMode == HighlightMode.Outline);
        private bool isOutline => (highlightMode == HighlightMode.Outline);

        public bool isSelected => (parentGroup.uiEnable) && (parentGroup.selectedControl == this);
        public bool isSelectable
        {
            get
            {
                if (canSelect == null) return true;
                bool ok = true;
                foreach (CanSelect handler in canSelect.GetInvocationList())
                    ok &= handler(this);
                return ok;
            }
        }
        public BaseUIControl navUp => _navUp;
        public BaseUIControl navDown => _navDown;
        public BaseUIControl navLeft => _navLeft;
        public BaseUIControl navRight => _navRight;

        public event OnSelect onSelect;
        public event OnDeselect onDeselect;
        public event OnChange onChange;
        public event OnInteract onInteract;
        public event OnUIEnableToggle onUIToggle;
        public event CanSelect canSelect;

        protected virtual void Start()
        {
            parentGroup = GetComponentInParent<UIGroup>();
            canvasGroup = GetComponent<CanvasGroup>();

            if (highlighterText)
            {
                defaultTextColor = highlighterText.color;
            }
            if (highlighterImage)
            {
                defaultImageColor = highlighterImage.color;
            }
            if (highlightMode == HighlightMode.Animator)
            {
                animator = GetComponent<Animator>();
            }
        }

        protected virtual void Update()
        {
            if (canSelect != null)
            {
                var result = true;
                foreach (CanSelect handler in canSelect.GetInvocationList())
                {
                    result &= handler(this);
                }

                if (result)
                {
                    if (canvasGroup) canvasGroup.alpha = 1.0f;
                }
                else
                {
                    if (canvasGroup) canvasGroup.alpha = 0.25f;
                }
            }
            if (highlighterImage)
            {
                if (highlightMode == HighlightMode.ImageEnable)
                {
                    highlighterImage.enabled = isSelected && parentGroup.uiEnable;
                }
                else if (highlightMode == HighlightMode.ColorSwitch)
                {
                    highlighterImage.enabled = true;
                    highlighterImage.color = (isSelected && parentGroup.uiEnable) ? (highlightColor) : (defaultImageColor);
                }
            }
            if (animator)
            {
                animator.SetBool("Highlight", isSelected && parentGroup.uiEnable);
                animator.SetBool("Enable", isSelectable && parentGroup.uiEnable);
            }
            if (highlightMode == HighlightMode.Outline)
            {
                if (isSelected && parentGroup.uiEnable)
                    highlighterImageEffect.SetOutline(outlineWidth, outlineColor);
                else
                    highlighterImageEffect.SetOutline(0.0f, outlineColor);
            }
            if (highlighterText)
            {
                highlighterText.color = (isSelected) ? (highlightColor) : (defaultTextColor);
            }
        }

        public virtual void NotifySelect(BaseUIControl prevControl)
        {
            onSelect?.Invoke(this, prevControl);
        }

        public virtual void NotifyDeselect()
        {
            onDeselect?.Invoke(this);
        }

        protected virtual void NotifyChange()
        {
            onChange?.Invoke(this);
        }

        protected virtual void NotifyInteract()
        {
            onInteract?.Invoke(this);
        }

        public virtual void NotifyEnable()
        {
            onUIToggle?.Invoke(true, this);
        }

        public virtual void NotifyDisable()
        {
            onUIToggle?.Invoke(false, this);
        }

        public void SetGroup(UIGroup grp)
        {
            parentGroup = grp;
        }

        public virtual void MoveHorizontal(float dz, bool isDown)
        {
            if (isDown)
            {
                if ((dz < -0.1f) && (_navLeft)) parentGroup.SetControl(NextSelectable(this, c => c._navLeft));
                else if ((dz > 0.1f) && (_navRight)) parentGroup.SetControl(NextSelectable(this, c => c._navRight));
            }
        }

        private BaseUIControl NextSelectable(BaseUIControl from, Func<BaseUIControl, BaseUIControl> step)
        {
            if (from == null) return null;
            var start = from;
            var cur = step(from);
            int hops = 0;

            while (cur != null && !cur.isSelectable && cur != start && hops < 128)
            {
                cur = step(cur);
                hops++;
            }

            // If we found a selectable control, use it; otherwise, stay where we are.
            return (cur != null && cur.isSelectable) ? cur : from;
        }

        public virtual void Interact()
        {
            NotifyInteract();
        }

        public void SetNav(BaseUIControl up, BaseUIControl down, BaseUIControl left, BaseUIControl right) { _navLeft = left; _navRight = right; _navUp = up; _navDown = down; }

        public void SetNavLeft(BaseUIControl control) { _navLeft = control; }
        public void SetNavRight(BaseUIControl control) { _navRight = control; }
        public void SetNavUp(BaseUIControl control) { _navUp = control; }
        public void SetNavDown(BaseUIControl control) { _navDown = control; }
    }

    public class UIControl<T> : BaseUIControl where T : IEquatable<T>
    {
        protected T _prevValue;
        protected T _value;

        public T value => _value;
        public T prevValue => _prevValue;

        public virtual void ChangeValue(T newValue)
        {
            _prevValue = _value;
            _value = newValue;
            if (!_prevValue.Equals(_value))
            {
                NotifyChange();
            }
        }
    }
}