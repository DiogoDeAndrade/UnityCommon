using UnityEngine;
using System;

namespace UC
{
    [Serializable]
    public class CooldownTimer
    {
        public float cooldown;
        [NonSerialized]
        public float timer;

        public CooldownTimer()
        {
            cooldown = 0.0f;
        }

        public CooldownTimer(float cooldown)
        {
            this.cooldown = cooldown;
            timer = 0.0f;
        }

        public static implicit operator CooldownTimer(float cooldown)
        {
            return new CooldownTimer(cooldown);
        }

        public static implicit operator float(CooldownTimer timer)
        {
            return (timer != null) ? (timer.cooldown) : (0.0f);
        }

        public void Start()
        {
            timer = cooldown;
        }

        public bool Update()
        {
            if (timer > 0)
            {
                timer -= Time.deltaTime;

                if (timer <= 0.0f) return true;
            }

            return false;
        }
    }
}