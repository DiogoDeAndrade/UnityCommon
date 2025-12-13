using UnityEngine;

namespace UC.RPG
{
    public class ResourceInstance
    {
        public delegate void OnChange(ResourceInstance resource, ChangeData data);
        public event OnChange onChange;
        public delegate void OnResourceEmpty(ResourceInstance resource, GameObject changeSource);
        public event OnResourceEmpty onResourceEmpty;
        public delegate void OnResourceNotEmpty(ResourceInstance resource, GameObject healSource);
        public event OnResourceNotEmpty onResourceNotEmpty;

        private ResourceType    _type;
        private float           _value;
        private float           _maxValue;
        private bool            _resourceEmpty;
        private IRPGOwner       _owner;

        public ResourceInstance(ResourceType type, IRPGOwner owner)
        {
            _type = type;
            _value = type.defaultValue;
            _maxValue = type.maxValue;
            _resourceEmpty = (_value <= 0.0f);
            _owner = owner;
        }

        public ResourceType type => _type;
        public object owner => _owner;
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

                onChange?.Invoke(this, new ChangeData { deltaValue = this._value - prevValue });
            }
        }

        public bool Change(ChangeData changeData, bool canAddOnEmpty = true)
        {
            float prevValue = value;
            bool ret = true;

            if (changeData.deltaValue < 0)
            {
                if (_resourceEmpty) ret = false;
                else
                {
                    value = Mathf.Clamp(value + changeData.deltaValue, 0.0f, type.maxValue);

                    onChange?.Invoke(this, changeData);

                    if (value <= 0.0f)
                    {
                        value = 0.0f;
                        _resourceEmpty = true;

                        onResourceEmpty?.Invoke(this, changeData.source);
                    }
                }
            }
            else if (changeData.deltaValue > 0)
            {
                if (canAddOnEmpty)
                {
                    if (value < type.maxValue)
                    {
                        value = Mathf.Clamp(value + changeData.deltaValue, 0.0f, type.maxValue);

                        onChange?.Invoke(this, changeData);

                        if ((value > 0.0f) && (_resourceEmpty))
                        {
                            onResourceNotEmpty?.Invoke(this, changeData.source);
                            _resourceEmpty = false;
                        }
                    }
                }
                else if (_resourceEmpty) ret = false;
                else
                {
                    if (value < type.maxValue)
                    {
                        value = Mathf.Clamp(value + changeData.deltaValue, 0.0f, type.maxValue);

                        onChange?.Invoke(this, changeData);
                    }
                    else ret = false;
                }
            }
            else if (changeData.deltaValue == 0.0f)
            {
                // Notify zero damage anyway
                onChange?.Invoke(this, changeData);
            }

            return ret;
        }

        public void SetResource(float r, bool notify = false)
        {
            float prevValue = value;
            value = r;
            _resourceEmpty = (value <= 0.0f);

            if (notify) onChange?.Invoke(this, new ChangeData { deltaValue = value - prevValue });
        }
    }
}
