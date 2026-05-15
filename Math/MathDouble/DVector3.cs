using System;
using UnityEngine;

namespace UC.DoubleMath
{
    [Serializable]
    public struct DVector3
    {
        public double x, y, z;

        public DVector3(Vector3 v)
        {
            this.x = (double)v.x; this.y = (double)v.y; this.z = (double)v.z;
        }

        public DVector3(double x, double y, double z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public static double Dot(DVector3 a, DVector3 b)
            => a.x * b.x + a.y * b.y + a.z * b.z;

        public static double Distance(DVector3 a, DVector3 b)
            => (b - a).magnitude;

        public static DVector3 operator +(DVector3 a, DVector3 b)
            => new(a.x + b.x, a.y + b.y, a.z + b.z);

        public static DVector3 operator *(DVector3 a, double s)
            => new(a.x * s, a.y * s, a.z * s);

        public static DVector3 operator /(DVector3 a, double s)
            => new(a.x / s, a.y / s, a.z / s);

        public static DVector3 operator *(double s, DVector3 a)
            => new(a.x * s, a.y * s, a.z * s);

        public static DVector3 operator -(DVector3 a, DVector3 b)
            => new(a.x - b.x, a.y - b.y, a.z - b.z);

        public void Normalize()
        {
            double n = magnitude;
            x /= n; y /= n; z /= n;
        }

        public DVector3 normalized
        {
            get
            {
                double n = magnitude;
                return this / n;
            }
        }

        public double sqrMagnitude
        {
            get
            {
                return (x * x + y * y + z * z);
            }
        }

        public double magnitude
        {
            get
            {
                return Math.Sqrt(x * x + y * y + z * z);
            }
        }

        public Vector3 ToVector3() {  return new Vector3((float)x, (float)y, (float)z); }

        public static DVector3 Cross(DVector3 a, DVector3 b)
        {
            return new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        }

        public static DVector3 ProjectOnPlane(DVector3 a, DVector3 b)
        {
            var bn = b.normalized;
            return a - Dot(a, bn) * bn;
        }

        public static DVector3 zero => new DVector3(0.0, 0.0, 0.0);
        public static DVector3 one => new DVector3(1, 1, 1);
    }
}