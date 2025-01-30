using NaughtyAttributes;
using System.Runtime.CompilerServices;
using UnityEngine;

public class PlaySound : MonoBehaviour
{
    public enum SoundTrigger { OnStart };

    [SerializeField] private SoundTrigger   trigger;
    [SerializeField] private SoundType      channel;
    [SerializeField] private AudioClip      sound;
    [SerializeField, MinMaxSlider(0.0f, 1.0f)] private Vector2 volume = Vector2.one;
    [SerializeField, MinMaxSlider(0.1f, 2.0f)] private Vector2 pitch = Vector2.one;

    void Start()
    {
        if (trigger == SoundTrigger.OnStart)
        {
            SoundManager.PlaySound(channel, sound, volume.Random(), pitch.Random());
            Destroy(this);
        }
    }
}
