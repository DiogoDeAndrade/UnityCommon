using System;
using UC;
using UnityEngine;

public static class BezierHelpers
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

    static public float ComputeDistanceNaive(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 point, int subDivs, ref float closestT, ref Vector3 closestPoint)
    {
        float   minDistSqr = float.MaxValue;
        float   tInc = 1.0f / (float)(subDivs - 1);

        Vector3 prevPoint = ComputeCubic(p0, p1, p2, p3, 0.0f);
        for (int i = 1; i < subDivs; i++)
        {
            float   t = tInc * (float)i;
            Vector3 bezierPoint = ComputeCubic(p0, p1, p2, p3, t);

            var closestPointOnSegment = LineHelpers.GetClosestPoint(prevPoint, bezierPoint, point, out float tSeg);

            float distSqr = (closestPointOnSegment - point).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closestPoint = closestPointOnSegment;
                // t - tInc => start of the segment (loop is on the "next" point, the end point of the segment)
                // tSeg * tInc => offset along the linear segment, multiplied by the size of each segment
                closestT = (t - tInc) + (tSeg * tInc);
            }

            prevPoint = bezierPoint;
        }

        return Mathf.Sqrt(minDistSqr);
    }

    // This solves by minimizing the squared distance function D(t) = ||B(t) - P||^2 with an iterative root finder for the quintic
    // Based on: https://blog.pkh.me/p/46-fast-calculation-of-the-distance-to-cubic-bezier-curves-on-the-gpu.html
    static public float ComputeDistanceFast(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 point, ref float closestT, ref Vector3 closestPoint)
    {
        // Start by testing the distance to the boundary points at t=0 (p0) and t=1 (p3)
        Vector3 dp0 = p0 - point,
                dp3 = p3 - point;
        float   dist0 = Vector3.Dot(dp0, dp0);
        float   dist3 = Vector3.Dot(dp3, dp3);
        float   minDist;
        
        if (dist0 < dist3)
        {
            minDist = dist0;
            closestT = 0.0f;
        }
        else
        {
            minDist = dist3;
            closestT = 1.0f;
        }

        // Bezier cubic points to polynomial coefficients
        Vector3 a = -p0 + 3.0f * (p1 - p2) + p3,
                b = 3.0f * (p0 - 2.0f * p1 + p2),
                c = 3.0f * (p1 - p0),
                d = p0;

        // Solve D'(t)=0 where D(t) is the distance squared
        Vector3 dmp = d - point;
        float da = 3.0f * Vector3.Dot(a, a),
              db = 5.0f * Vector3.Dot(a, b),
              dc = 4.0f * Vector3.Dot(a, c) + 2.0f * Vector3.Dot(b, b),
              dd = 3.0f * (Vector3.Dot(a, dmp) + Vector3.Dot(b, c)),
              de = 2.0f * Vector3.Dot(b, dmp) + Vector3.Dot(c, c),
              df = Vector3.Dot(c, dmp);

        var roots = new float[5];
        int count = FindRoot5(roots, da, db, dc, dd, de, df);
        for (int i = 0; i < count; i++)
        {
            float t = roots[i];

            // Evaluate the distance to our point p and keep the smallest
            Vector3 dp = ((a * t + b) * t + c) * t + dmp;
            float   dist = Vector3.Dot(dp, dp);
            if (dist < minDist)
            {
                minDist = dist;
                closestT = t;
            }
        }

        // We've been working with the squared distance so far, it's time to get its
        // square root
        
        closestPoint = ComputeCubic(p0, p1, p2, p3, closestT);

        return Mathf.Sqrt(minDist);
    }

    // Find the root of a quadratic polynomial
    static int FindRoot2(float[] r, float a, float b, float c)
    {
        int     count = 0;
        float   d = b * b - 4f * a * c;

        if (d < 0f)
            return count;

        if (d == 0f)
        {
            float s = -0.5f * b / a;
            if (float.IsFinite(s))
            {
                r[count++] = s;
            }

            return count;
        }

        float h = MathF.Sqrt(d);
        float q = -0.5f * (b + ((b > 0f) ? h : -h)); // numerically stable
        float r0 = q / a;
        float r1 = c / q;

        if (float.IsFinite(r0))
        {
            r[count++] = r0;
        }

        if (float.IsFinite(r1))
        {
            r[count++] = r1;
        }

        return count;
    }

    // Find root of quintic polynomial (https://www.cemyuksel.com/research/polynomials/)
    static int FindRoot5(float[] r, float a, float b, float c, float d, float e, float f)
    {
        var r2 = new float[5];
        var r3 = new float[5];
        var r4 = new float[5];

        int n = FindRoot2(r2, 10.0f * a, 4.0f * b, c);                          // degree 2
        n = Find5(r3, r2, n, 0.0f, 0.0f, 10.0f * a, 6.0f * b, 3.0f * c, d);     // degree 3
        n = Find5(r4, r3, n, 0.0f, 5.0f * a, 4.0f * b, 3.0f * c, d + d, e);     // degree 4
        n = Find5(r, r4, n, a, b, c, d, e, f);                                  // degree 5
        
        return n;
    }

    static float Poly5(float a, float b, float c, float d, float e, float f, float t)
    {
        return ((((a * t + b) * t + c) * t + d) * t + e) * t + f;
    }

    public static int Find5(float[] r, float[] r4, int n,
                            float a, float b, float c, float d, float e, float f,
                            float eps = 1e-6f, int maxIter = 32)
    {
        int count = 0;

        // p = (x, y) starting at x=0
        Vector2 p;
        p.x = 0f;
        p.y = Poly5(a, b, c, d, e, f, 0f);

        for (int i = 0; i <= n; i++)
        {
            float x = (i == n) ? 1f : r4[i];
            float y = Poly5(a, b, c, d, e, f, x);

            // Only proceed if there is a sign change or a zero at an endpoint
            if (p.y * y <= 0f)
            {
                float v = Bisect5(a, b, c, d, e, f, new Vector2(p.x, x), new Vector2(p.y, y), eps, maxIter);
                r[count++] = v;
            }

            p = new Vector2(x, y);
        }

        return count;
    }

    // Bisect uses Newtorn's method, more consistent than ITP, sometimes faster, sometimes slower.
    private static float Bisect5(float a, float b, float c, float d, float e, float f,
                                 Vector2 t, Vector2 v, float eps, int maxIter)
    {
        // Midpoint
        float x = 0.5f * (t.x + t.y);

        // Choose a consistent orientation for the bracket
        float s = (v.x < v.y) ? 1f : -1f;

        for (int i = 0; i < maxIter; i++)
        {
            // Evaluate polynomial (y) and derivative (q) in a single Horner pass
            // q accumulates derivative via running sum trick (like dual numbers).
            float y = a * x + b;
            float q = a * x + y;

            y = y * x + c; q = q * x + y;
            y = y * x + d; q = q * x + y;
            y = y * x + e; q = q * x + y;
            y = y * x + f; // final y

            // Update bracket using the oriented sign
            if (s * y < 0f) t = new Vector2(x, t.y);
            else t = new Vector2(t.x, x);

            // Newton step; fall back to bisection if out of bounds or derivative too small
            float next;
            if (MathF.Abs(q) > 1e-20f)
            {
                next = x - y / q;
                if (next < t.x || next > t.y)
                    next = 0.5f * (t.x + t.y);
            }
            else
            {
                next = 0.5f * (t.x + t.y);
            }

            if (MathF.Abs(next - x) < eps)
                return next;

            x = next;
        }

        return x; // best effort after maxIter
    }

    // ITP algorithm (Oliveira & Takahashi, 2020)
    // Faster bracketed root finder to replace Bisect5.
    // a,b,c,d,e,f : quintic coefficients
    // t           : [t.x, t.y] is the current bracket (requires t.y > t.x)
    // v           : (f(t.x), f(t.y)) = values of the polynomial at the bracket ends
    // eps         : desired absolute tolerance on x
    //
    // Can replace Newton's, but it has larger time variance (less consistent), can be slower or faster.
    private static float Itp5(float a, float b, float c, float d, float e, float f,
                              Vector2 t, Vector2 v, float eps)
    {
        float diff = t.y - t.x;
        if (diff <= 0f) return 0.5f * (t.x + t.y); // degenerate bracket guard

        // K1 and n0 as suggested by the CRAN note (K2=2 implied by delta formula).
        float K1 = 0.2f / diff;
        int n0 = 1;

        // Support both orientations (paper assumes f(a)<0<f(b)).
        float s = (v.x < v.y) ? 1f : -1f;

        // n_{1/2} = ceil(log2(diff/eps) - 1); n_max = n_{1/2} + n0
        // Use base-2 log via change of base.
        float log2 = MathF.Log(diff / eps) / MathF.Log(2f);
        int nh = (int)MathF.Ceiling(log2 - 1f);
        int n_max = nh + n0;

        // q = eps * 2^{n_max}
        float q = eps * MathF.Pow(2f, n_max);

        // Optional: hard cap to avoid infinite loops in pathological cases
        int safeMax = 128;
        int k = 0;

        while (diff > 2f * eps && k++ < safeMax)
        {
            // Interpolation (Regula-Falsi); fall back to midpoint if denominator is ~0
            float xh = 0.5f * (t.x + t.y);
            float denom = (v.y - v.x);
            float xf = MathF.Abs(denom) > 1e-30f ? (v.y * t.x - v.x * t.y) / denom : xh;

            // Truncation
            float sigma = MathF.Sign(xh - xf);
            float delta = K1 * diff * diff;        // (K2=2 baked-in, avoids pow)
            float xt = (delta <= MathF.Abs(xh - xf)) ? xf + sigma * delta : xh;

            // Projection
            float r = q - 0.5f * diff;
            float x = (MathF.Abs(xt - xh) <= r) ? xt : (xh - sigma * r);

            // Update bracket
            float y = Poly5(a, b, c, d, e, f, x);
            float side = s * y;
            if (side > 0f) { t.y = x; v.y = y; }
            else if (side < 0f) { t.x = x; v.x = y; }
            else
            {
                return x; // exact (or close enough) root
            }

            diff = t.y - t.x;
            q *= 0.5f;
        }

        return 0.5f * (t.x + t.y);
    }
}
