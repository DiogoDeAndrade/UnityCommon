using UnityEngine;

namespace UC
{
    public class Hover : MonoBehaviour
    {
        [SerializeField] private Transform  target;
        [SerializeField] private Vector3    direction = Vector3.up;
        [SerializeField] private float      offset;
        [SerializeField] private float      amplitude;
        [SerializeField] private float      frequency;
        [SerializeField] private float      baseOffset = 0.0f;
        [SerializeField] private bool       useAbs = false;

        Vector3 basePos;
        float elapsedTime;

        void Start()
        {
            if (target == null) target = this.transform;

            basePos = target.localPosition;
        }

        // Update is called once per frame
        void Update()
        {
            elapsedTime += Time.deltaTime * frequency * Mathf.Deg2Rad;

            float wave = Mathf.Sin(elapsedTime + baseOffset);
            if (useAbs) wave = Mathf.Abs(wave);
            target.localPosition = basePos + direction * (offset + wave * amplitude);
        }

        public void SetFrequency(float v) { frequency = v; }
        public void SetAmplitude(float v) { amplitude = v; }
    }
}