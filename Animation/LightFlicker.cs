using UnityEngine;

namespace UC
{
    // Flickering light (candles, fires). Its color follows a gradient that repeats every duration seconds, with the
    // gradient's alpha scaling the light's intensity, and its position wanders around where it started (Perlin noise,
    // so it moves smoothly at the given frequency). With randomStart, each light starts at a random point of the
    // gradient, so lights with the same settings don't flicker in step; without it they all start at the beginning.
    // The position noise always starts at a random point.
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("Seconds for one pass through the gradient")]
        private float       duration = 1.0f;
        [SerializeField, Tooltip("Light color over each pass; the alpha scales the intensity set on the light")]
        private Gradient    colorOverLifetime = new Gradient();
        [SerializeField, Tooltip("Start at a random point of the gradient, so lights with the same settings aren't in step")]
        private bool        randomStart = true;
        [SerializeField, Min(0.0f), Tooltip("How fast the position wanders (noise samples per second)")]
        private float       frequency = 3.0f;
        [SerializeField, Tooltip("How far the light can move from its start position, per axis")]
        private Vector3     amplitude = new Vector3(0.02f, 0.01f, 0.02f);

        Light   flickerLight;
        float   baseIntensity;
        Vector3 basePosition;
        float   time;
        float   seed;

        void Awake()
        {
            flickerLight = GetComponent<Light>();
            baseIntensity = flickerLight.intensity;
            basePosition = transform.localPosition;
            time = (randomStart) ? (Random.Range(0.0f, duration)) : (0.0f);
            seed = Random.Range(0.0f, 100.0f);
        }

        void Update()
        {
            time += Time.deltaTime;

            Color color = colorOverLifetime.Evaluate(Mathf.Repeat(time, duration) / duration);
            flickerLight.color = new Color(color.r, color.g, color.b);
            flickerLight.intensity = baseIntensity * color.a;

            float t = time * frequency;
            transform.localPosition = basePosition + new Vector3(Noise(t, seed) * amplitude.x,
                                                                 Noise(t, seed + 17.0f) * amplitude.y,
                                                                 Noise(t, seed + 31.0f) * amplitude.z);
        }

        // Perlin noise remapped to -1..1
        static float Noise(float t, float row)
        {
            return Mathf.PerlinNoise(t, row) * 2.0f - 1.0f;
        }
    }
}
