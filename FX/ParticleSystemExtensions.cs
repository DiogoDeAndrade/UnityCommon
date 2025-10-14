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

        public static void SetColor(this ParticleSystem ps, Color color)
        {
            var mainModule = ps.main;
            mainModule.startColor = color;
        }
    }
}