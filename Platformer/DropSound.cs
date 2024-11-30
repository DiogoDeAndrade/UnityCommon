using NaughtyAttributes;
using UnityEngine;

public class DropSound : MonoBehaviour
{
    [SerializeField] private AudioClip dropSnd;
    [SerializeField] float cooldown = 0.1f;
    [SerializeField, MinMaxSlider(0.1f, 1.0f)] Vector2 volumeVariance = Vector2.one;
    [SerializeField, MinMaxSlider(0.1f, 1.5f)] Vector2 pitchVariance = Vector2.one;

    Rigidbody2D rb;
    float cooldownTimer;

    Vector2 prevVelocity;
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        prevVelocity = rb.linearVelocity;
    }

    // Update is called once per frame
    void Update()
    {
        if (cooldown > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if ((prevVelocity.y < -5.0f) && (rb.linearVelocityY > -1e-6))
        {
            if (cooldownTimer <= 0.0f)
            {
                SoundManager.PlaySound(SoundType.PrimaryFX, dropSnd, volumeVariance.Random(), pitchVariance.Random());
                if (cooldown > 0.0f) cooldownTimer = cooldown;
            }
        }

        prevVelocity = rb.linearVelocity;
    }
}
