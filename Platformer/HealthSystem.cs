using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{    
    public float maxHealth = 100.0f;
    public float invulnerabilityTime = 2.0f;
    public bool  invulnerabilityBlink = true;
    [ShowIf("invulnerabilityBlink")]
    public float            blinkTime = 0.2f;
    [ShowIf("invulnerabilityBlink")]
    public Renderer[]       renderers;

    protected float _health = 100.0f;
    protected bool  _dead;
    protected float invulnerabilityTimer;

    TimeScaler2d        timeScaler;
    float               blinkTimer;

    public bool isInvulnerable
    {
        get { return invulnerabilityTimer > 0.0f; }
        set
        {
            if (invulnerabilityTimer <= 0.0f)
            {
                blinkTimer = blinkTime;
            }
            invulnerabilityTimer = invulnerabilityTime;

        }
    }

    public bool isDead
    {
        get
        {
            return _dead;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _health = maxHealth;
        _dead = false;

        timeScaler = GetComponent<TimeScaler2d>();

        if (invulnerabilityBlink)
        {
            if ((renderers == null) || (renderers.Length == 0))
            {
                renderers = new SpriteRenderer[1];
                renderers[0] = GetComponent<SpriteRenderer>();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (invulnerabilityTimer > 0.0f)
        {
            invulnerabilityTimer -= Time.deltaTime;

            if (invulnerabilityTimer <= 0.0f)
            {
                invulnerabilityTimer = 0.0f;
                if (invulnerabilityBlink)
                {
                    foreach (var sr in renderers)
                        sr.enabled = true;
                }
            }
            else
            {
                if (invulnerabilityBlink)
                {
                    blinkTimer -= Time.deltaTime;
                    if (blinkTimer < 0.0f)
                    {
                        blinkTimer += blinkTime;

                        foreach (var sr in renderers)
                            sr.enabled = !sr.enabled;
                    }
                }
            }
        }
    }

    public void DealDamage(float damage)
    {
        _health -= damage;
        if (_health < 0.0f)
        {
            _health = 0.0f;
            _dead = true;
        }
    }
}
