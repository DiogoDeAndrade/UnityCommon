using UC;
using UnityEngine;
using UnityEngine.UI;

public class SetUserPrefFromSlides : MonoBehaviour
{
    public enum KeyType { Float, Vec4_W, Bool };

    [SerializeField] private KeyType keyType;
    [SerializeField] private string userPrefName;

    public void ChangeValue(Slider slider)
    {
        switch (keyType)
        {
            case KeyType.Float:
                {
                    PlayerPrefs.SetFloat(userPrefName, slider.value);
                }
                break;
            case KeyType.Bool:
                {
                    PlayerPrefsHelpers.SetBool(userPrefName, (slider.value > 0.5f));
                }
                break;
            case KeyType.Vec4_W:
                {
                    PlayerPrefsHelpers.SetVector4(userPrefName, new Vector4(0.0f, 0.0f, 0.0f, slider.value));
                }
                break;
            default:
                break;
        }
    }
}
