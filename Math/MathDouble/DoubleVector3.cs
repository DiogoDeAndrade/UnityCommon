using System;

namespace UC.DoubleMath
{
    public class DoubleVector3
    {
        public double x, y, z;

        public DoubleVector3()
        {
        }

        public DoubleVector3(double x, double y, double z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public static double Dot(DoubleVector3 a, DoubleVector3 b)
            => a.x * b.x + a.y * b.y + a.z * b.z;

        public static DoubleVector3 operator +(DoubleVector3 a, DoubleVector3 b)
            => new(a.x + b.x, a.y + b.y, a.z + b.z);

        public static DoubleVector3 operator *(DoubleVector3 a, double s)
            => new(a.x * s, a.y * s, a.z * s);

        public static DoubleVector3 operator -(DoubleVector3 a, DoubleVector3 b)
            => new(a.x - b.x, a.y - b.y, a.z - b.z);

        public DoubleVector3 normalized
        {
            get
            {
                double n = magnitude;
                return this * n;
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

        public static DoubleVector3 zero => new DoubleVector3(0.0, 0.0, 0.0);
        public static DoubleVector3 one => new DoubleVector3(1, 1, 1);
    }
}