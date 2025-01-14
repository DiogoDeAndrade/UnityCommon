using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIFloatSelector : UIControl<float>
{
    public enum AutoEvent { None, SetPlayerPref };

    [SerializeField] protected Image            leftArrow;
    [SerializeField] protected Image            rightArrow;
    [SerializeField] protected float            increment = 0.1f;
    [SerializeField] protected Vector2          minMaxValue = new Vector2(-1.0f, 1.0f);
    [SerializeField] protected float            defaultValue = 0.0f;
    [SerializeField] protected float            changeCooldown = 0.1f;
    [SerializeField] protected TextMeshProUGUI  valueIndicator;
    [SerializeField] private AutoEvent          valueChangeEvent;
    [SerializeField, ShowIf("needKey")] private string key;

    bool needKey => valueChangeEvent == AutoEvent.SetPlayerPref;


    string originalText;
    float cooldownTimer;

    protected void Awake()
    {
        if ((valueChangeEvent == AutoEvent.SetPlayerPref) && (!string.IsNullOrEmpty(key)))
        {
            _value = _prevValue = PlayerPrefs.GetFloat(key, defaultValue);
        }
        else
        {
            _value = _prevValue = defaultValue;
        }

        if (valueIndicator) originalText = valueIndicator.text;
    }

    protected override void Update()
    {
        base.Update();

        if (valueIndicator)
        {
            valueIndicator.text = string.Format(originalText, value);
        }

        if (cooldownTimer > 0.0f)
        {
            cooldownTimer -= Time.deltaTime;
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

            if ((valueChangeEvent == AutoEvent.SetPlayerPref) && (!string.IsNullOrEmpty(key))) PlayerPrefs.SetFloat(key, value);
        }
        else if (dz < 0.0f) 
        {
            float val = Mathf.Clamp(value - increment, minMaxValue.x, minMaxValue.y);
            ChangeValue(val);
            if (changeSnd) SoundManager.PlaySound(SoundType.SecondaryFX, changeSnd);

            cooldownTimer = changeCooldown;

            if ((valueChangeEvent == AutoEvent.SetPlayerPref) && (!string.IsNullOrEmpty(key))) PlayerPrefs.SetFloat(key, value);
        }
    }
}
