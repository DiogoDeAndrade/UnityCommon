using UnityEngine;

public static class ParticleSystemExtensions
{
    public static void SetEmitter(this ParticleSystem ps, bool b)
    {
        var emissionModule = ps.emission;
        emissionModule.enabled = b;
    }
}
