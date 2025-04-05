using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    public class FootstepSound : MonoBehaviour
    {
        [SerializeField] float distancePerStep = 5.0f;
        [SerializeField] float teleportDistance = 50.0f;
        [SerializeField] float stepCooldown = 0.1f;
        [SerializeField] AudioClip footstepSnd;
        [SerializeField, MinMaxSlider(0.1f, 1.0f)] Vector2 volumeVariance = Vector2.one;
        [SerializeField, MinMaxSlider(0.1f, 1.5f)] Vector2 pitchVariance = Vector2.one;

        float accumDist;
        Vector3 prevPos;
        float cooldownTimer;
        MovementPlatformer movementPlatformer;

        void Start()
        {
            prevPos = transform.position;
            movementPlatformer = GetComponent<MovementPlatformer>();
        }

        void Update()
        {
            if (stepCooldown > 0)
            {
                cooldownTimer -= Time.deltaTime;
            }
            if ((movementPlatformer == null) || (movementPlatformer.isGrounded))
            {
                float d = Mathf.Abs(prevPos.x - transform.position.x);
                if (d > teleportDistance)
                {
                    accumDist = 0.0f;
                }
                else
                {
                    accumDist += d;
                    if (accumDist > distancePerStep)
                    {
                        if (cooldownTimer <= 0.0f)
                        {
                            accumDist = 0.0f;
                            SoundManager.PlaySound(SoundType.PrimaryFX, footstepSnd, volumeVariance.Random(), pitchVariance.Random());
                            if (stepCooldown > 0) cooldownTimer = stepCooldown;
                        }
                    }
                }

                prevPos = transform.position;
            }
        }
    }
}