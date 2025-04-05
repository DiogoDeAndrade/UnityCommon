using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class CameraShake2d : MonoBehaviour
    {
        class ShakeElem
        {
            public float strength;
            public float time;
        }

        public enum Mode { Random, NoiseBased };

        public Mode mode;
        public TimeScaler2d timeScaler;

        List<ShakeElem> shakeElems = new List<ShakeElem>();
        Vector3 prevDelta;
        Vector3 currentNoiseIndex;

        static CameraShake2d instance;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            if (timeScaler == null) timeScaler = GetComponent<TimeScaler2d>();
            prevDelta = Vector3.zero;
            currentNoiseIndex = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
        }

        void LateUpdate()
        {
            // Revert previous movement
            transform.position -= prevDelta;

            float dt = (timeScaler) ? (timeScaler.deltaTime) : (Time.deltaTime);

            float totalStrength = 0;

            shakeElems.ForEach((e) => e.time -= dt);
            shakeElems.ForEach((e) => totalStrength = Mathf.Max(totalStrength, e.strength));

            if (mode == Mode.Random)
            {
                prevDelta = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), 0.0f);
                prevDelta = prevDelta.normalized * totalStrength;
            }
            else if (mode == Mode.NoiseBased)
            {
                currentNoiseIndex += (Vector3.up * 0.0111f + Vector3.right * 0.0098f + Vector3.forward * 0.01f) * dt;

                float noiseMod = Mathf.PerlinNoise(currentNoiseIndex.z, 0.0f);

                prevDelta.x = (Mathf.PerlinNoise(currentNoiseIndex.x, 0.0f) * 2 - 1) * totalStrength * noiseMod;
                prevDelta.y = (Mathf.PerlinNoise(0.0f, currentNoiseIndex.y) * 2 - 1) * totalStrength * noiseMod;
            }

            transform.position += prevDelta;

            shakeElems.RemoveAll((e) => e.time <= 0);
        }

        public void AddShake(float str, float t)
        {
            ShakeElem elem = new ShakeElem();
            elem.strength = str;
            elem.time = t;

            shakeElems.Add(elem);
        }

        public static void Shake(float str, float t)
        {
            if (instance)
            {
                instance.AddShake(str, t);
            }
        }
    }
}