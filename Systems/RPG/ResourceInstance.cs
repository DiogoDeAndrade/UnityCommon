using UnityEngine;

namespace UC.RPG
{
    public class ResourceInstance
    {
        public delegate void OnChange(ResourceHandler.ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource);
        public event OnChange onChange;
        public delegate void OnResourceEmpty(GameObject changeSource);
        public event OnResourceEmpty onResourceEmpty;
        public delegate void OnResourceNotEmpty(GameObject healSource);
        public event OnResourceNotEmpty onResourceNotEmpty;

        private ResourceType    _type;
        private float           _value;
        private float           _maxValue;
        private bool            _resourceEmpty;

        public ResourceInstance(ResourceType type)
        {
            _type = type;
            _value = type.defaultValue;
            _maxValue = type.maxValue;
            _resourceEmpty = (_value <= 0.0f);
        }

        public ResourceType type => _type;
        public float        normalizedValue => Mathf.Clamp01(_value / _maxValue);
        public bool         isResourceEmpty => _resourceEmpty;
        public bool         isResourceNotEmpty => !_resourceEmpty;

        public float value
        {
            get { return _value; }
            set
            {
                _value = value;
                _resourceEmpty = _value <= 0.0f;
            }
        }

        public float maxValue
        {
            get { return _maxValue; }
            set
            {
                float prevValue = this._value;
                float p = normalizedValue;

                _maxValue = value;
                this._value = _maxValue * p;

                onChange?.Invoke(ResourceHandler.ChangeType.Burst, this._value - prevValue, Vector3.zero, Vector3.zero, null);
            }
        }

        public bool Change(ResourceHandler.ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource, bool canAddOnEmpty = true)
        {
            float prevValue = value;
            bool ret = true;

            if (deltaValue < 0)
            {
                if (_resourceEmpty) ret = false;
                else
                {
                    value = Mathf.Clamp(value + deltaValue, 0.0f, type.maxValue);
                    if (value <= 0.0f)
                    {
                        value = 0.0f;
                        _resourceEmpty = true;

                        onResourceEmpty?.Invoke(changeSource);
                    }
                    else
                    {
                        onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);
                    }
                }
            }
            else if (deltaValue > 0)
            {
                if (canAddOnEmpty)
                {
                    if (value < type.maxValue)
                    {
                        value = Mathf.Clamp(value + deltaValue, 0.0f, type.maxValue);

                        onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);

                        if ((value > 0.0f) && (_resourceEmpty))
                        {
                            onResourceNotEmpty?.Invoke(changeSource);
                            _resourceEmpty = false;
                        }
                    }
                }
                else if (_resourceEmpty) ret = false;
                else
                {
                    if (value < type.maxValue)
                    {
                        value = Mathf.Clamp(value + deltaValue, 0.0f, type.maxValue);

                        onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);
                    }
                    else ret = false;
                }
            }

            return ret;
        }

        public void SetResource(float r, bool notify = false)
        {
            float prevValue = value;
            value = r;
            _resourceEmpty = (value <= 0.0f);

            if (notify) onChange?.Invoke(ResourceHandler.ChangeType.Burst, value - prevValue, Vector3.zero, Vector3.zero, null);
        }
    }
}
