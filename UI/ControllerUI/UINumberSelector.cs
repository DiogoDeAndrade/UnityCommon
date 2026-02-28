using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UC
{

    public class UIFloatSelector : UIControl<float>
    {
        public enum DisplayMode { Text, ScaleX, ScaleY, Fill };
        public enum AutoEvent { None, SetPlayerPref, SetSoundVolume };
        public enum ValueType { Float, Vec4_W };

        [SerializeField] protected Image leftArrow;
        [SerializeField] protected Image rightArrow;
        [SerializeField] protected float increment = 0.1f;
        [SerializeField] protected Vector2 minMaxValue = new Vector2(-1.0f, 1.0f);
        [SerializeField] protected float defaultValue = 0.0f;
        [SerializeField] protected float changeCooldown = 0.1f;
        [SerializeField] protected DisplayMode displayMode;
        [SerializeField, ShowIf(nameof(needValueIndicatorText))] protected TextMeshProUGUI valueIndicatorText;
        [SerializeField, ShowIf(nameof(needValueIndicatorTransform))] protected RectTransform valueIndicatorTransform;
        [SerializeField, ShowIf(nameof(needFillImage))] protected Image fillImage;
        [SerializeField] private AutoEvent valueChangeEvent;
        [SerializeField, ShowIf(nameof(needKey))] private ValueType valueType;
        [SerializeField, ShowIf(nameof(needKey))] private string key;
        [SerializeField, ShowIf(nameof(needSoundType))] private SoundType soundType;

        bool needKey => valueChangeEvent == AutoEvent.SetPlayerPref;
        bool needSoundType => valueChangeEvent == AutoEvent.SetSoundVolume;
        bool needValueIndicatorText => displayMode == DisplayMode.Text;
        bool needValueIndicatorTransform => displayMode == DisplayMode.ScaleX || displayMode == DisplayMode.ScaleY;
        bool needFillImage => displayMode == DisplayMode.Fill;


        string originalText;
        float cooldownTimer;

        protected void Awake()
        {
            if ((valueChangeEvent == AutoEvent.SetPlayerPref) && (!string.IsNullOrEmpty(key)))
            {
                switch (valueType)
                {
                    case ValueType.Float:
                        _value = _prevValue = PlayerPrefs.GetFloat(key, defaultValue);
                        break;
                    case ValueType.Vec4_W:
                        {
                            _value = _prevValue = PlayerPrefsHelpers.GetVector4(key, new Vector4(0.0f, 0.0f, 0.0f, defaultValue)).w;
                        }
                        break;
                    default:
                        break;
                }
            }
            else if (valueChangeEvent == AutoEvent.SetSoundVolume)
            {
                _value = _prevValue = SoundManager.GetVolume(soundType);
                SoundManager.SetVolume(soundType, _value, true);
            }
            else
            {
                _value = _prevValue = defaultValue;
            }

            if (valueIndicatorText) originalText = valueIndicatorText.text;
        }

        protected override void Update()
        {
            base.Update();

            switch (displayMode)
            {
                case DisplayMode.Text:
                    if (valueIndicatorText)
                    {
                        valueIndicatorText.text = string.Format(originalText, value);
                    }
                    break;
                case DisplayMode.ScaleX:
                    if (valueIndicatorTransform)
                    {
                        valueIndicatorTransform.localScale = new Vector3(Mathf.Clamp01((value - minMaxValue.x) / (minMaxValue.y - minMaxValue.x)), 1, 1);
                    }
                    break;
                case DisplayMode.ScaleY:
                    if (valueIndicatorTransform)
                    {
                        valueIndicatorTransform.localScale = new Vector3(1, Mathf.Clamp01((value - minMaxValue.x) / (minMaxValue.y - minMaxValue.x)), 1);
                    }
                    break;
                case DisplayMode.Fill:
                    if (fillImage)
                    {
                        fillImage.fillAmount = Mathf.Clamp01((value - minMaxValue.x) / (minMaxValue.y - minMaxValue.x));
                    }
                    break;
                default:
                    break;
            }

            if (cooldownTimer > 0.0f)
            {
                cooldownTimer -= parentGroup.GetDeltaTime();
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
            if (cooldownTimer > 0.0f) return;

            if (dz > 0.0f)
            {
                float val = Mathf.Clamp(value + increment, minMaxValue.x, minMaxValue.y);
                ChangeValue(val);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);

                cooldownTimer = changeCooldown;

                UpdateValue();
            }
            else if (dz < 0.0f)
            {
                float val = Mathf.Clamp(value - increment, minMaxValue.x, minMaxValue.y);
                ChangeValue(val);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);

                cooldownTimer = changeCooldown;

                UpdateValue();
            }
        }

        void UpdateValue()
        {
            if ((valueChangeEvent == AutoEvent.SetPlayerPref) && (!string.IsNullOrEmpty(key)))
            {
                switch (valueType)
                {
                    case ValueType.Float:
                        PlayerPrefs.SetFloat(key, value);
                        break;
                    case ValueType.Vec4_W:
                        PlayerPrefs.SetString(key, $"1.0;1.0;1.0;{value}");
                        break;
                    default:
                        break;
                }

                PlayerPrefs.Save();
            }
            else if (valueChangeEvent == AutoEvent.SetSoundVolume)
            {
                SoundManager.SetVolume(soundType, value, true);
            }
        }
    }
}