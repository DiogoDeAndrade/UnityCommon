using UnityEngine;

namespace UC
{

    public static class ParticleSystemExtensions
    {
        public static void SetEmission(this ParticleSystem ps, bool b)
        {
            var emissionModule = ps.emission;
            emissionModule.enabled = b;
        }
    }
}