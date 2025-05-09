﻿using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class HealthSystem : MonoBehaviour
    {
        public enum DamageType { Burst, OverTime };

        public delegate void OnHit(DamageType damageType, float damage, Vector3 damagePosition, Vector3 hitDirection, GameObject damageSource);
        public event OnHit onHit;
        public delegate void OnHeal(float healthGain, GameObject healSource);
        public event OnHeal onHeal;

        public delegate void OnDead(GameObject damageSource);
        public event OnDead onDead;
        public delegate void OnRevive(GameObject healSource);
        public event OnDead onRevive;

        public Faction faction;
        public float maxHealth = 100.0f;
        public float invulnerabilityTime = 2.0f;
        public bool invulnerabilityBlink = true;
        [ShowIf(nameof(invulnerabilityBlink))]
        public bool useAnimatorForBlink = true;
        [ShowIf(nameof(needBlinkTime))]
        public float blinkTime = 0.1f;

        protected float _health = 100.0f;
        protected bool _dead;
        protected float invulnerabilityTimer;

        Animator animator;
        float blinkTimer;
        SpriteRenderer spriteRenderer;

        bool needBlinkTime => invulnerabilityBlink && (!useAnimatorForBlink);

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

        public bool isDead => _dead;
        public bool isAlive => !_dead;
        void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void Start()
        {
            _health = maxHealth;
            _dead = false;
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
                        if (useAnimatorForBlink) animator.SetBool("Invulnerable", false);
                        else spriteRenderer.enabled = true;
                    }
                }
                else
                {
                    if (invulnerabilityBlink)
                    {
                        if (useAnimatorForBlink) animator.SetBool("Invulnerable", true);
                        else
                        {
                            blinkTimer -= Time.deltaTime;
                            if (blinkTimer < 0)
                            {
                                blinkTimer = blinkTime;

                                spriteRenderer.enabled = !spriteRenderer.enabled;
                            }
                        }
                    }
                }
            }
            else
            {
                if ((invulnerabilityBlink) && (!isDead))
                {
                    if (useAnimatorForBlink) animator.SetBool("Invulnerable", false);
                    else if (!spriteRenderer.enabled) spriteRenderer.enabled = true;
                }
            }
        }

        public bool Heal(float delta, bool reviveIfDeath, GameObject healSource)
        {
            if (reviveIfDeath)
            {
                if (_health < maxHealth)
                {
                    onHeal?.Invoke(delta, healSource);

                    _health += delta;
                    if ((_health > 0.0f) && (_dead))
                    {
                        onRevive?.Invoke(healSource);
                        _dead = false;
                    }
                    return true;
                }
            }
            else if (_dead) return false;
            if (_health < maxHealth)
            {
                onHeal?.Invoke(delta, healSource);

                _health += delta;
                return true;
            }
            return false;
        }
        public bool DealDamage(DamageType damageType, float damage, Vector3 damagePosition, Vector3 hitDirection, GameObject damageSource)
        {
            if (isInvulnerable) return false;
            if (_dead) return false;

            _health -= damage;
            if (_health <= 0.0f)
            {
                _health = 0.0f;
                _dead = true;

                onDead?.Invoke(damageSource);
            }
            else
            {
                onHit?.Invoke(damageType, damage, damagePosition, hitDirection, damageSource);

                if (damageType != DamageType.OverTime)
                {
                    if (invulnerabilityTime > 0.0f)
                    {
                        isInvulnerable = true;
                    }
                }
            }

            return true;
        }

        public static HealthSystem[] FindAll()
        {
            return FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
        }

        public static HealthSystem[] FindAll(Vector3 pos, float range)
        {
            List<HealthSystem> ret = new List<HealthSystem>();
            var healthSystems = FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
            foreach (var h in healthSystems)
            {
                if (Vector3.Distance(h.transform.position, pos) < range)
                {
                    ret.Add(h);
                }
            }

            return ret.ToArray();
        }

        public void SetHealth(float h)
        {
            _health = h;
            _dead = (_health <= 0.0f);
        }

        public void ResetHealth()
        {
            _health = maxHealth;
            _dead = false;
        }

        [Button("Deal 10% Damage")]
        void DealOneDamage()
        {
            DealDamage(DamageType.Burst, 0.1f * maxHealth, Vector3.zero, Vector3.up, null);
        }

        [Button("Kill")]
        void Kill()
        {
            DealDamage(DamageType.Burst, _health, Vector3.zero, Vector3.up, null);
        }
    }
}