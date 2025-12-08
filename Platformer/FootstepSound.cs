using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    public class FootstepSound : MonoBehaviour
    {
        [SerializeField] bool  is3D = false;
        [SerializeField] bool  linkToRenderer = false;
        [SerializeField] float distancePerStep = 5.0f;
        [SerializeField] float teleportDistance = 50.0f;
        [SerializeField] float stepCooldown = 0.1f;
        [SerializeField] AudioClip footstepSnd;
        [SerializeField, MinMaxSlider(0.1f, 1.0f)] Vector2 volumeVariance = Vector2.one;
        [SerializeField, MinMaxSlider(0.1f, 1.5f)] Vector2 pitchVariance = Vector2.one;

        float accumDist;
        Vector3 prevPos;
        float cooldownTimer;
        MovementPlatformer  movementPlatformer;
        new Renderer            renderer;

        void Start()
        {
            prevPos = transform.position;
            movementPlatformer = GetComponent<MovementPlatformer>();
            if (linkToRenderer)
            {
                renderer = GetComponent<Renderer>();
            }
        }

        void Update()
        {
            if (stepCooldown > 0)
            {
                cooldownTimer -= Time.deltaTime;
            }
            if ((renderer) && (!renderer.enabled)) return;

            float dist = 0.0f;
            if (is3D)
            {
                dist = Vector3.Distance(prevPos, transform.position);
            }
            else
            {
                if ((movementPlatformer == null) || (movementPlatformer.isGrounded))
                {
                    dist = Mathf.Abs(prevPos.x - transform.position.x);
                }
            }

            if (dist > teleportDistance)
            {
                accumDist = 0.0f;
            }
            else
            {
                accumDist += dist;
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