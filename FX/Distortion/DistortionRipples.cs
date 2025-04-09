using NaughtyAttributes;
using System.Collections.Generic;
using UC;
using UnityEngine;

public class DistortionRipples : MonoBehaviour
{
    [SerializeField] 
    private bool            playOnStart = true;
    [SerializeField, ShowIf(nameof(playOnStart))] 
    private float           startDelay = 0.0f;
    [SerializeField] 
    private bool            destroyOnEnd = true;
    [SerializeField, ShowIf(nameof(neitherStartNorDestroy))]
    private bool            spawnOnMovement = false;
    [SerializeField, ShowIf(nameof(isSpawnOnMovement))]
    private float           deltaDistance = 20.0f;
    [SerializeField, ShowIf(nameof(isSpawnOnMovement))]
    private bool            turnToMovement;
    [SerializeField]
    private bool            localSpace = true;
    [SerializeField] 
    private int             nRipples = 3;
    [SerializeField, ShowIf(nameof(hasMultipleRipples))] 
    private float           delayBetweenRipples = 0.5f;
    [SerializeField] 
    private float           rippleDuration = 1.0f;
    [SerializeField] 
    private float           startAmplitude = 1.0f;
    [SerializeField, ShowIf(nameof(hasMultipleRipples))] 
    private float           amplitudeMultiplier = 0.5f;
    [SerializeField] 
    private AnimationCurve  sizeCurve;
    [SerializeField] 
    private AnimationCurve  strengthCurve;
    [SerializeField]
    private bool            strengthIsSpriteAlpha;
    [SerializeField, Header("Visuals")] 
    private Sprite          rippleSprite;
    [SerializeField, Layer]
    private int             rippleLayer;
    [SerializeField, SortingLayer] 
    private int             spriteSortingLayer;
    [SerializeField] 
    private int             spriteOrderInLayer;
    [SerializeField] 
    private Material        rippleMaterial;

    Vector3                 prevPos;
    Vector3                 lastDir;
    float                   accumDistance;
    int                     rippleCount;
    float                   rippleTimer;
    float                   amplitude;
    List<SpriteRenderer>    ripples = new();

    bool hasMultipleRipples => (nRipples > 1);
    bool neitherStartNorDestroy => !playOnStart && !destroyOnEnd;
    bool isSpawnOnMovement => neitherStartNorDestroy && spawnOnMovement;

    void Start()
    {
        if (playOnStart)
        {
            Play(startDelay);
        }
        prevPos = transform.position;
    }

    void Update()
    {
        ripples.RemoveAll(ripple => ripple == null);

        if (rippleCount > 0)
        {
            rippleTimer -= Time.deltaTime;
            if (rippleTimer < 0)
            {
                RunRipple();
            }
        }
        else
        {
            if (destroyOnEnd)
            {
                if (ripples.Count == 0)
                {
                    Destroy(gameObject);
                }
            }
        }

        if (isSpawnOnMovement)
        {
            Vector3 deltaPos = transform.position - prevPos;
            float   distance = deltaPos.magnitude;
            if (distance > 0.0f)
            {
                lastDir = deltaPos.normalized;
            }

            accumDistance += distance;
            if (accumDistance > deltaDistance)
            {
                Play(0.0f);
                accumDistance -= deltaDistance;
            }

            prevPos = transform.position;
        }
    }

    public void Play(float startDelay)
    {
        rippleCount = nRipples;
        amplitude = startAmplitude;

        if (startDelay > 0)
        {
            rippleTimer = startDelay;
        }
        else
        {
            RunRipple();
        }
    }

    void RunRipple()
    {
        GameObject go = new GameObject();
        if (localSpace)
        {
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            if (turnToMovement)
            {
                go.transform.localRotation = Quaternion.LookRotation(Vector3.forward, lastDir.PerpendicularXY());
            }
            else
            {
                go.transform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            go.transform.position = transform.position;
            if (turnToMovement)
            {
                go.transform.rotation = Quaternion.LookRotation(Vector3.forward, lastDir.PerpendicularXY());
            }
            else
            {
                go.transform.rotation = transform.rotation;
            }
        }
        go.layer = rippleLayer;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = rippleSprite;
        sr.sortingLayerID = spriteSortingLayer;
        sr.sortingOrder = spriteOrderInLayer;
        sr.material = new Material(rippleMaterial);

        var ripple = go.AddComponent<DistortionRipple>();
        ripple.SetParams(rippleDuration, sizeCurve, amplitude, strengthCurve, strengthIsSpriteAlpha);

        ripples.Add(sr);

        rippleTimer = delayBetweenRipples;
        amplitude *= amplitudeMultiplier;
        rippleCount--;
    }
    private void OnDrawGizmosSelected()
    {
        if (rippleSprite != null)
        {
            float s = Mathf.Max(rippleSprite.rect.width, rippleSprite.rect.height) * rippleSprite.pixelsPerUnit * 0.5f;
            if ((sizeCurve != null) && (sizeCurve.length > 0))
            {
                float maxSize = 0.0f;
                foreach (var k in sizeCurve.keys)
                {
                    maxSize = Mathf.Max(maxSize, k.value);
                }
                s *= maxSize;
            }

            Gizmos.color = new Color(1.0f, 1.0f, 0.0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, s);
        }
    }
}
