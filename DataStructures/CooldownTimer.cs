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

        // Trigger next is used for special cases when cooldown is 0, so that it can trigger the timer anyway, so that you don't have to make different code paths for 0 cooldown timers
        private bool triggerNext = false;

        public bool isDone => (timer <= 0.0f);
        public bool isRunning => (timer > 0.0f);
        public float normalizedTime => Mathf.Clamp01((cooldown > 0.0f) ? (timer / cooldown) : (0.0f));

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
            if (cooldown == 0.0f) triggerNext = true;
            else triggerNext = false;
        }

        public void Stop()
        {
            timer = 0.0f;
        }

        public bool Update()
        {
            if (timer > 0)
            {
                timer -= Time.deltaTime;

                if (timer <= 0.0f) return true;
            }

            if (triggerNext)
            {
                triggerNext = false;
                return true;
            }
            return false;
        }
    }
}