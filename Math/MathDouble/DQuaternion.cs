using System;

namespace UC.DoubleMath
{
    [Serializable]
    public struct DQuaternion
    {
        public double x, y, z, w;


        public DQuaternion(double x, double y, double z, double w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        public static DQuaternion operator *(DQuaternion a, double s) => new(a.x * s, a.y * s, a.z * s, a.w * s);


        public DQuaternion normalized
        {
            get
            {
                double n = magnitude;
                return this * n;
            }
        }

        public double magnitude
        {
            get
            {
                return Math.Sqrt(x * x + y * y + z * z);
            }
        }

        public static DQuaternion identity => new DQuaternion(0.0, 0.0, 0.0, 1.0);

    }
}