using System;

namespace UC.DoubleMath
{
    public class DoubleQuaternion
    {
        public double x, y, z, w;


        public DoubleQuaternion(double x, double y, double z, double w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        public static DoubleQuaternion operator *(DoubleQuaternion a, double s) => new(a.x * s, a.y * s, a.z * s, a.w * s);


        public DoubleQuaternion normalized
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

        public static DoubleQuaternion identity => new DoubleQuaternion(0.0, 0.0, 0.0, 1.0);

    }
}