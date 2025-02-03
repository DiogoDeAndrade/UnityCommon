using UnityEngine;

public class ShakeCamera: MonoBehaviour
{
    public enum ShakeTrigger { OnStart };

    [SerializeField] private ShakeTrigger   trigger;
    [SerializeField] private float          time;
    [SerializeField] private float          strength;

    void Start()
    {
        if (trigger == ShakeTrigger.OnStart)
        {
            CameraShake2d.Shake(strength, time);
            Destroy(this);
        }
    }
}
