using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{    
    public float maxHealth = 100.0f;
    public float invulnerabilityTime = 2.0f;
    public bool  invulnerabilityBlink = true;

    protected float _health = 100.0f;
    protected bool  _dead;
    protected float invulnerabilityTimer;

    TimeScaler2d        timeScaler;
    Animator            animator;
    float               blinkTimer;

    public bool isInvulnerable
    {
        get { return invulnerabilityTimer > 0.0f; }
        set
        {
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
        animator = GetComponent<Animator>();
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
                animator.SetBool("Invulnerable", false);
            }
            else
            {
                animator.SetBool("Invulnerable", true);
            }
        }
        else
        {
            animator.SetBool("Invulnerable", false);
        }
    }

    public bool DealDamage(float damage)
    {
        if (isInvulnerable) return false;
        if (_dead) return false;

        _health -= damage;
        if (_health <= 0.0f)
        {
            _health = 0.0f;
            _dead = true;
        }

        return true;
    }
}
