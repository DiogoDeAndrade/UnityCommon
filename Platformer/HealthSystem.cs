using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public enum Faction { Neutral, Friendly, Enemy };

    public delegate void OnHit(float damage);
    public event OnHit  onHit;

    public delegate void OnDead();
    public event OnDead onDead;

    public Faction  faction;
    public float    maxHealth = 100.0f;
    public float    invulnerabilityTime = 2.0f;
    public bool     invulnerabilityBlink = true;

    protected float _health = 100.0f;
    protected bool  _dead;
    protected float invulnerabilityTimer;

    TimeScaler2d        timeScaler;
    Animator            animator;
    float               blinkTimer;

    public float health
    {
        get { return _health; }
    }

    public float normalizedHealth
    {
        get { return _health / maxHealth; }
    }

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

    void Awake()
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

            onDead?.Invoke();
        }
        else
        {
            onHit?.Invoke(damage);
        }

        return true;
    }
}
