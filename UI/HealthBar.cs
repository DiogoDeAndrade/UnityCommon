using UnityEngine;

namespace UC
{

    public class HealthBar : ResourceBar
    {
        [Header("Health Bar")]
        public HealthSystem healthSystem;

        protected override float GetNormalizedResource()
        {
            return Mathf.Clamp01(healthSystem.normalizedHealth);
        }

        protected override float GetResourceCount()
        {
            return healthSystem.health;
        }
    }
}