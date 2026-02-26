#if UNITYSPLINE_PRESENT
using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SplineSampler : MonoBehaviour
{
    [SerializeField] 
    private SplineContainer spline;
    [SerializeField] 
    private int             sampleCount = 200;
    [SerializeField] 
    private bool            debugSamples;
    [SerializeField, ShowIf(nameof(debugSamples))] 
    private Color           debugColor;
    [SerializeField, ShowIf(nameof(debugSamples))] 
    private float           debugRadius = 0.1f;

    struct Sample
    {
        public Vector3  pos;
        public Vector3  tangent;
        public Vector3  up;
        public float    distance;
    }
    List<Sample>    samples;
    float           _maxDistance;

    public float maxDistance => _maxDistance;

    void Start()
    {
        BuildCache();
    }

    void BuildCache()
    { 
        if (spline == null)
        {
            spline = GetComponent<SplineContainer>();
        }

        samples = new List<Sample>();

        float   tInc = 1.0f / (sampleCount - 1.0f);
        float   distance = 0.0f;
        Vector3 prevPos = default;
        bool    hasPrev = false;
        
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i * tInc;

            if (spline.Evaluate(t, out var pos, out var tan, out var up))
            {
                if (hasPrev)
                    distance += Vector3.Distance(prevPos, pos);

                var s = new Sample()
                {
                    pos = pos,
                    tangent = tan,
                    up = up,
                    distance = distance
                };
                samples.Add(s);

                prevPos = pos;
                hasPrev = true;
            }
        }

        _maxDistance = distance;
    }

    public bool Evaluate(float t, out Vector3 position, out Vector3 tangent, out Vector3 up)
    {
        bool b = spline.Evaluate(t, out var pos, out var tan, out var u);

        position = pos;
        tangent = tan;
        up = u;
        return b;
    }

    public bool EvaluateByDistance(float dist, out Vector3 position, out Vector3 tangent, out Vector3 up)
    {
        // Defaults in case something is wrong.
        position = transform.position;
        tangent = transform.right;
        up = transform.up;

        if ((samples == null) || (samples.Count == 0))
            return false;

        // Clamp distance to spline range.
        if (_maxDistance <= 1e-6f)
        {
            // Degenerate: all samples on top of each other.
            position = samples[0].pos;
            tangent = samples[0].tangent.normalized;
            up = samples[0].up.normalized;
            return false;
        }

        dist = Mathf.Clamp(dist, 0f, _maxDistance);

        // Quick outs.
        if (dist <= 0f)
        {
            position = samples[0].pos;
            tangent = samples[0].tangent.normalized;
            up = samples[0].up.normalized;
            return true;
        }
        if (dist >= _maxDistance)
        {
            var last = samples[samples.Count - 1];
            position = last.pos;
            tangent = last.tangent.normalized;
            up = last.up.normalized;
            return true;
        }

        // Binary search for first sample with distance >= dist.
        int lo = 0;
        int hi = samples.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (samples[mid].distance < dist) lo = mid + 1;
            else hi = mid;
        }

        int i1 = lo;              // first >= dist
        int i0 = Mathf.Max(0, i1 - 1);

        Sample a = samples[i0];
        Sample b = samples[i1];

        float span = b.distance - a.distance;
        float t = span <= 1e-6f ? 0f : (dist - a.distance) / span;

        position = Vector3.LerpUnclamped(a.pos, b.pos, t);

        // Tangent/up should be normalized for safety after interpolation.
        tangent = Vector3.LerpUnclamped(a.tangent, b.tangent, t);
        if (tangent.sqrMagnitude > 1e-10f) tangent.Normalize();
        else tangent = b.tangent.normalized;

        up = Vector3.LerpUnclamped(a.up, b.up, t);
        if (up.sqrMagnitude > 1e-10f) up.Normalize();
        else up = b.up.normalized;

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugSamples) return;

        if ((samples == null) || (samples.Count == 0))
            BuildCache();

        Gizmos.color = debugColor;
        foreach (var sample in samples)
        {
            Gizmos.DrawSphere(sample.pos, debugRadius);
        }
    }
}
#endif