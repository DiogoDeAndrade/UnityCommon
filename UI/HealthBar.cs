using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthBar : ResourceBar
{
    [Header("Health Bar")]
    public HealthSystem healthSystem;

    protected override float GetNormalizedResource()
    {
        return Mathf.Clamp01(healthSystem.normalizedHealth);
    }
}
