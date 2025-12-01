using UnityEngine;

public static class QuaternionExtension
{
    public static Quaternion Divide(this Quaternion q, float v)
    {
        return new Quaternion(q.x / v, q.y / v, q.z / v, q.w / v);
    }
    public static Quaternion Multiply(this Quaternion q, float v)
    {
        return new Quaternion(q.x * v, q.y * v, q.z * v, q.w * v);
    }
    public static Quaternion Add(this Quaternion q1, Quaternion q2)
    {
        return new Quaternion(q1.x + q2.x, q1.y + q2.y, q1.z + q2.z, q1.w + q2.w);
    }

    public static Quaternion UnnormalizedLerp(Quaternion q1, Quaternion q2, float t)
    {
        float invT = 1.0f - t;
        return new Quaternion(q1.x * invT + q2.x * t,
                              q1.y * invT + q2.y * t,
                              q1.z * invT + q2.z * t,
                              q1.w * invT + q2.w * t);
    }

    public static Quaternion UnnormalizedSlerp(Quaternion q1, Quaternion q2, float t)
    {
        // Make sure we take the shortest path
        float dot = Quaternion.Dot(q1, q2);
        if (dot < 0f)
        {
            q2 = new Quaternion(-q2.x, -q2.y, -q2.z, -q2.w);
            dot = -dot;
        }

        dot = Mathf.Clamp(dot, -1f, 1f);

        const float DOT_THRESHOLD = 0.9995f;
        if (dot > DOT_THRESHOLD)
        {
            // Quaternions are very close; fall back to linear blend (still unnormalized)
            return UnnormalizedLerp(q1, q2, t);
        }

        float theta0 = Mathf.Acos(dot);      // angle between a and b
        float theta = theta0 * t;
        float sinTheta0 = Mathf.Sin(theta0);
        float sinTheta = Mathf.Sin(theta);

        float s0 = Mathf.Sin(theta0 - theta) / sinTheta0; // (1 - t) weight
        float s1 = sinTheta / sinTheta0;                  // t weight

        return new Quaternion(
            s0 * q1.x + s1 * q2.x,
            s0 * q1.y + s1 * q2.y,
            s0 * q1.z + s1 * q2.z,
            s0 * q1.w + s1 * q2.w
        );
    }
}
