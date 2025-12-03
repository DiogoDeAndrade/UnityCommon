using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace UC
{

    public class UIStringSelector : UIControl<string>
    {
        public enum AutoEvent { None, SetPlayerPref };

        [SerializeField] 
        protected Image leftArrow;
        [SerializeField] 
        protected Image rightArrow;
        [SerializeField] 
        protected string defaultValue = "";
        [SerializeField, ResizableTextArea] 
        protected string possibleValues;
        [SerializeField] 
        protected float changeCooldown = 0.1f;
        [SerializeField] 
        protected TextMeshProUGUI valueIndicatorText;
        [SerializeField] 
        private AutoEvent valueChangeEvent;
        [SerializeField, ShowIf(nameof(needKey))] 
        private string key;

        bool needKey => valueChangeEvent == AutoEvent.SetPlayerPref;

        string          originalText;
        float           cooldownTimer;
        List<string>    _possibleValues;
        int index = 0;

        protected void Awake()
        {
            _possibleValues = new(possibleValues.Split('\n'));

            if ((valueChangeEvent == AutoEvent.SetPlayerPref) && (!string.IsNullOrEmpty(key)))
            {
                _value = PlayerPrefs.GetString(key, defaultValue);
                if (string.IsNullOrEmpty(_value))
                {
                    _value = _possibleValues[0];
                }

            }
            else
            {
                _value = defaultValue;
            }
            index = GetIndexByString(_value);
            _prevValue = _value = _possibleValues[index];            

            if (valueIndicatorText) originalText = valueIndicatorText.text;
        }

        int GetIndexByString(string s)
        {
            var ret = _possibleValues.IndexOf(s);
            if (ret < 0) ret = 0;

            return ret;
        }

        protected override void Update()
        {
            base.Update();

            if (valueIndicatorText)
            {
                valueIndicatorText.text = string.Format(originalText, value);
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
                index = (index + 1) % _possibleValues.Count;
                ChangeValue(_possibleValues[index]);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);

                cooldownTimer = changeCooldown;

                UpdateValue();
            }
            else if (dz < 0.0f)
            {
                index = index - 1; if (index < 0) index = _possibleValues.Count - 1;
                ChangeValue(_possibleValues[index]);
                if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);

                cooldownTimer = changeCooldown;

                UpdateValue();
            }
        }

        void UpdateValue()
        {
            if ((valueChangeEvent == AutoEvent.SetPlayerPref) && (!string.IsNullOrEmpty(key)))
            {
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
            }
        }

        public void SetOptions(List<string> options, string currentOption)
        {
            _possibleValues = options;
            possibleValues = "";
            defaultValue = currentOption;
            foreach (var p in _possibleValues)
            {
                if (possibleValues != "") possibleValues += "\n";
                possibleValues += p;
            }
            _value = _prevValue = currentOption;
            index = GetIndexByString(currentOption);
        }
    }
}
