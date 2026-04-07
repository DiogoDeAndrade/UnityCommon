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

        public static float GetEmissionRateOverTimeValue(this ParticleSystem ps)
        {
            // This only works if the emission rate is set to a constant value
            var emissionModule = ps.emission;
            return emissionModule.rateOverTime.constant;
        }

        public static void SetEmissionRateOverTimeValue(this ParticleSystem ps, float rate)
        {
            // This only works if the emission rate is set to a constant value
            var emissionModule = ps.emission;
            emissionModule.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
        }

        public static void SetColor(this ParticleSystem ps, Color color)
        {
            var mainModule = ps.main;
            mainModule.startColor = color;
        }
    }
}