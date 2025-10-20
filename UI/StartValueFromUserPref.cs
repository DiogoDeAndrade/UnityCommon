using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class StartValueFromUserPref : MonoBehaviour
{
    public enum KeyType { Float, Vec4_W, Bool };

    [SerializeField] private KeyType    keyType;
    [SerializeField] private string     userPrefName;
    [SerializeField, ShowIf(nameof(isFloat))] 
    private float      defaultValue = 1.0f;
    [SerializeField, ShowIf(nameof(isBool))] 
    private bool       defaultValueB = false;

    Slider slider;
    Toggle toggle;

    bool isFloat => (keyType == KeyType.Float) || (keyType == KeyType.Vec4_W);
    bool isBool => (keyType == KeyType.Bool);

    void Start()
    {
        slider = GetComponent<Slider>();
        toggle = GetComponent<Toggle>();
        if ((slider) || (toggle))
        {
            switch (keyType)
            {
                case KeyType.Float:
                    {
                        float v = PlayerPrefs.GetFloat(userPrefName, defaultValue);
                        slider.value = v;
                    }
                    break;
                case KeyType.Bool:
                    {
                        bool val = PlayerPrefsHelpers.GetBool(userPrefName, defaultValueB);
                        toggle.isOn = val;
                    }
                    break;
                case KeyType.Vec4_W:
                    {
                        Vector4 val = PlayerPrefsHelpers.GetVector4(userPrefName, new Vector4(0.0f, 0.0f, 0.0f, defaultValue));
                        slider.value = val.w;
                    }
                    break;
                default:
                    break;
            }
        }
        Destroy(this);
    }

    [Button("Clear Player Prefs")]
    void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
    }
}
