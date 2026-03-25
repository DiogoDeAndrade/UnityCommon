using System;
using UnityEngine;

namespace UC.DoubleMath
{
    [Serializable]
    public struct DVector3
    {
        public double x, y, z;

        public DVector3(double x, double y, double z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public static double Dot(DVector3 a, DVector3 b)
            => a.x * b.x + a.y * b.y + a.z * b.z;

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

        public static DVector3 zero => new DVector3(0.0, 0.0, 0.0);
        public static DVector3 one => new DVector3(1, 1, 1);
    }
}