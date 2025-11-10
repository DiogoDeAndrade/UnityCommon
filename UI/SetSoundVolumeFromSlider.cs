using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.UI;

public class SetSoundVolumeFromSlider : MonoBehaviour
{
    [SerializeField] private SoundType soundType;

    public void ChangeVolume(Slider slider)
    {
        SoundManager.SetVolume(soundType, slider.value, true);
    }
}
