using UnityEngine;

public static class Bezier
{
    static public void SplitCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, out Vector3[] firstCurve, out Vector3[] secondCurve)
    {
        Vector3 a = Vector3.Lerp(p0, p1, t);
        Vector3 b = Vector3.Lerp(p1, p2, t);
        Vector3 c = Vector3.Lerp(p2, p3, t);

        Vector3 d = Vector3.Lerp(a, b, t);
        Vector3 e = Vector3.Lerp(b, c, t);

        Vector3 f = Vector3.Lerp(d, e, t); // Point at t

        firstCurve = new Vector3[] { p0, a, d, f };
        secondCurve = new Vector3[] { f, e, c, p3 };
    }

    static public Vector3 ComputeCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float it = (1 - t);
        float t2 = t * t;
        float t3 = t2 * t;
        float it2 = it * it;
        float it3 = it2 * it;

        return p0 * it3 + 3 * p1 * it2 * t + 3 * p2 * it * t2 + p3 * t3;
    }

    static public Vector3 ComputeCubic(Vector3[] pt, float t)
    {
        return ComputeCubic(pt[0], pt[1], pt[2], pt[3], t);
    }

    static public Vector3 ComputeCubicDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float it = 1 - t;

        // Derivative of cubic Bézier:
        return 3 * it * it * (p1 - p0) +
               6 * it * t * (p2 - p1) +
               3 * t * t * (p3 - p2);
    }

    static public Vector3 ComputeCubicDerivative(Vector3[] pt, float t)
    {
        return ComputeCubicDerivative(pt[0], pt[1], pt[2], pt[3], t);
    }
}
