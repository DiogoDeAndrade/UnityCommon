using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class CameraShake3d : MonoBehaviour
    {
        class ShakeElem
        {
            public float strength;
            public float time;
        }

        public enum Mode { Random, NoiseBased };

        [SerializeField] private Mode mode;

        List<ShakeElem> shakeElems = new List<ShakeElem>();
        PerlinNoise3D   perlinNoise;
        Vector3         prevDelta;
        Vector3         currentNoiseIndex;

        static CameraShake3d instance;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }

            prevDelta = Vector3.zero;
            currentNoiseIndex = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
            perlinNoise = new PerlinNoise3D((int)Time.time);
        }

        void LateUpdate()
        {
            // Revert previous movement
            transform.position -= prevDelta;

            float dt = Time.deltaTime;

            float totalStrength = 0;

            shakeElems.ForEach((e) => e.time -= dt);
            shakeElems.ForEach((e) => totalStrength = Mathf.Max(totalStrength, e.strength));

            if (mode == Mode.Random)
            {
                prevDelta = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
                prevDelta = prevDelta.normalized * totalStrength;
            }
            else if (mode == Mode.NoiseBased)
            {
                currentNoiseIndex += (Vector3.up * 0.0111f + Vector3.right * 0.0098f + Vector3.forward * 0.01f) * dt;

                prevDelta.x = (perlinNoise.Evaluate(currentNoiseIndex.x, 0.0f, 0.0f) * 2.0f - 1.0f) * totalStrength;
                prevDelta.y = (perlinNoise.Evaluate(0.0f, currentNoiseIndex.y, 0.0f) * 2.0f - 1.0f) * totalStrength;
                prevDelta.z = (perlinNoise.Evaluate(0.0f, 0.0f, currentNoiseIndex.z) * 2.0f - 1.0f) * totalStrength;
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
