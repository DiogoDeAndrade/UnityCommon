using UnityEngine;

namespace UC
{
    /// A quaternion-like structure that does NOT normalize automatically.
    /// This allows storing multi-turn rotations and unbounded rotation magnitudes.
    [System.Serializable]
    public struct UnnormalizedQuaternion
    {
        public float x, y, z, w;

        public UnnormalizedQuaternion(float x, float y, float z, float w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        public UnnormalizedQuaternion(Quaternion q)
        {
            x = q.x; y = q.y; z = q.z; w = q.w;
        }

        // Identity (no rotation)
        public static readonly UnnormalizedQuaternion identity =
            new UnnormalizedQuaternion(0f, 0f, 0f, 1f);

        public static UnnormalizedQuaternion FromAxisAngle(Vector3 axis, float angle)
        {
            axis.Normalize();
            float half = angle * 0.5f;
            float s = Mathf.Sin(half);
            float c = Mathf.Cos(half);

            return new UnnormalizedQuaternion(axis.x * s, axis.y * s, axis.z * s, c);
        }

        public Quaternion ToUnityQuaternion()
        {
            Quaternion q = new Quaternion(x, y, z, w);
            return q.normalized; // Unity cannot represent multi-turn, but OK for orientation
        }

        public float Magnitude()
        {
            return Mathf.Sqrt(x * x + y * y + z * z + w * w);
        }

        public UnnormalizedQuaternion Normalized()
        {
            float m = Magnitude();
            return new UnnormalizedQuaternion(x / m, y / m, z / m, w / m);
        }

        public static UnnormalizedQuaternion operator *(UnnormalizedQuaternion a, UnnormalizedQuaternion b)
        {
            return new UnnormalizedQuaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z
            );
        }

        public static UnnormalizedQuaternion operator *(UnnormalizedQuaternion q, float s)
        {
            return new UnnormalizedQuaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        }

        public static UnnormalizedQuaternion operator *(float s, UnnormalizedQuaternion q)
        {
            return new UnnormalizedQuaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        }

        public static UnnormalizedQuaternion operator /(UnnormalizedQuaternion q, float v)
        {
            float s = 1.0f / v;
            return new UnnormalizedQuaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        }


        public static UnnormalizedQuaternion operator +(UnnormalizedQuaternion a, UnnormalizedQuaternion b)
        {
            return new UnnormalizedQuaternion(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        }

        public static float Dot(UnnormalizedQuaternion a, UnnormalizedQuaternion b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        }

        public static UnnormalizedQuaternion Lerp(UnnormalizedQuaternion a, UnnormalizedQuaternion b, float t)
        {
            return new UnnormalizedQuaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
        }

        public static UnnormalizedQuaternion Slerp(UnnormalizedQuaternion a, UnnormalizedQuaternion b, float t)
        {
            float dot = Dot(a, b);

            // Ensure shortest path
            if (dot < 0f)
            {
                b = b * -1f;
                dot = -dot;
            }

            dot = Mathf.Clamp(dot, -1f, 1f);

            const float THRESHOLD = 0.9995f;
            if (dot > THRESHOLD)
            {
                // Very small angle = linear interpolation
                return Lerp(a, b, t);
            }

            float theta0 = Mathf.Acos(dot);
            float theta = theta0 * t;

            float sin0 = Mathf.Sin(theta0);
            float sinT = Mathf.Sin(theta);

            float s0 = Mathf.Sin(theta0 - theta) / sin0;
            float s1 = sinT / sin0;

            return new UnnormalizedQuaternion(a.x * s0 + b.x * s1, a.y * s0 + b.y * s1, a.z * s0 + b.z * s1, a.w * s0 + b.w * s1);
        }

        public void ToAxisAngle(out Vector3 axis, out float angle)
        {
            float m = Magnitude();
            float halfAngle = Mathf.Acos(w / m);
            angle = halfAngle * 2f;

            float s = Mathf.Sin(halfAngle);
            if (Mathf.Abs(s) < 0.00001f)
            {
                axis = Vector3.right; // arbitrary
            }
            else
            {
                axis = new Vector3(x, y, z) / (m * s);
            }
        }

        public Vector3 ToEulerAngles()
        {
            return ToUnityQuaternion().eulerAngles;
        }

        public UnnormalizedQuaternion Inverse()
        {
            float normSq = x * x + y * y + z * z + w * w;

            // Protect against divide-by-zero (should not happen unless q is zero quaternion)
            if (normSq < 1e-12f)
                return new UnnormalizedQuaternion(0, 0, 0, 0);

            float inv = 1.0f / normSq;

            return new UnnormalizedQuaternion(-x * inv, -y * inv, -z * inv, w * inv);
        }

        public static UnnormalizedQuaternion Inverse(UnnormalizedQuaternion q)
        {
            float normSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;

            // Protect against divide-by-zero (should not happen unless q is zero quaternion)
            if (normSq < 1e-12f)
                return new UnnormalizedQuaternion(0, 0, 0, 0);

            float inv = 1.0f / normSq;

            return new UnnormalizedQuaternion(-q.x * inv, -q.y * inv, -q.z * inv, q.w * inv);
        }
    }
}