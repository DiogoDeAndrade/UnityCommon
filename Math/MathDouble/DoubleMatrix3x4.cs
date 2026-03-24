
using System;
using UnityEngine;

namespace UC.DoubleMath
{
    public class DoubleMatrix3x4
    {
        public double m00, m01, m02, m03;
        public double m10, m11, m12, m13;
        public double m20, m21, m22, m23;

        public DoubleMatrix3x4()
        {
        }

        public DoubleMatrix3x4(double m00, double m01, double m02, double m03,
                               double m10, double m11, double m12, double m13,
                               double m20, double m21, double m22, double m23)
        {
            this.m00 = m00; this.m01 = m01; this.m02 = m02; this.m03 = m03;
            this.m10 = m10; this.m11 = m11; this.m12 = m12; this.m13 = m13;
            this.m20 = m20; this.m21 = m21; this.m22 = m22; this.m23 = m23;
        }

        public DoubleVector3 MultiplyVector(DoubleVector3 v)
        {
            return new DoubleVector3(m00 * v.x + m01 * v.y + m02 * v.z,
                                     m10 * v.x + m11 * v.y + m12 * v.z,
                                     m20 * v.x + m21 * v.y + m22 * v.z);
        }

        public DoubleVector3 MultiplyPoint(DoubleVector3 v)
        {
            return new DoubleVector3(m00 * v.x + m01 * v.y + m02 * v.z + m03,
                                     m10 * v.x + m11 * v.y + m12 * v.z + m13,
                                     m20 * v.x + m21 * v.y + m22 * v.z + m23);
        }

        public DoubleVector3 translation
        {
            get
            {
                return new DoubleVector3(m03, m13, m23);
            }
        }

        public DoubleQuaternion rotation
        {
            get
            {
                // Extract rotation from the upper-left 3x3.
                // This assumes the matrix contains only rotation (or uniform/non-uniform scale
                // that we remove by normalizing the basis vectors).

                // Basis vectors = columns of the matrix
                DoubleVector3 x = new DoubleVector3(m00, m10, m20);
                DoubleVector3 y = new DoubleVector3(m01, m11, m21);
                DoubleVector3 z = new DoubleVector3(m02, m12, m22);

                // Remove scale by normalizing the basis vectors
                x = x.normalized;
                y = y.normalized;
                z = z.normalized;

                // Build normalized rotation matrix
                double rm00 = x.x, rm01 = y.x, rm02 = z.x;
                double rm10 = x.y, rm11 = y.y, rm12 = z.y;
                double rm20 = x.z, rm21 = y.z, rm22 = z.z;

                double trace = rm00 + rm11 + rm22;

                double qw, qx, qy, qz;

                if (trace > 0.0)
                {
                    double s = Math.Sqrt(trace + 1.0) * 2.0; // s = 4 * qw
                    qw = 0.25 * s;
                    qx = (rm21 - rm12) / s;
                    qy = (rm02 - rm20) / s;
                    qz = (rm10 - rm01) / s;
                }
                else if (rm00 > rm11 && rm00 > rm22)
                {
                    double s = Math.Sqrt(1.0 + rm00 - rm11 - rm22) * 2.0; // s = 4 * qx
                    qw = (rm21 - rm12) / s;
                    qx = 0.25 * s;
                    qy = (rm01 + rm10) / s;
                    qz = (rm02 + rm20) / s;
                }
                else if (rm11 > rm22)
                {
                    double s = Math.Sqrt(1.0 + rm11 - rm00 - rm22) * 2.0; // s = 4 * qy
                    qw = (rm02 - rm20) / s;
                    qx = (rm01 + rm10) / s;
                    qy = 0.25 * s;
                    qz = (rm12 + rm21) / s;
                }
                else
                {
                    double s = Math.Sqrt(1.0 + rm22 - rm00 - rm11) * 2.0; // s = 4 * qz
                    qw = (rm10 - rm01) / s;
                    qx = (rm02 + rm20) / s;
                    qy = (rm12 + rm21) / s;
                    qz = 0.25 * s;
                }

                return new DoubleQuaternion(qx, qy, qz, qw).normalized;
            }
        }

        public static DoubleMatrix3x4 TRS(DoubleVector3 currentTranslation, DoubleQuaternion currentRotation, DoubleVector3 scale)
        {
            // Assuming DoubleQuaternion stores x,y,z,w and is normalized or close to it.
            double x = currentRotation.x;
            double y = currentRotation.y;
            double z = currentRotation.z;
            double w = currentRotation.w;

            double sx = scale.x;
            double sy = scale.y;
            double sz = scale.z;

            double xx = x * x;
            double yy = y * y;
            double zz = z * z;
            double xy = x * y;
            double xz = x * z;
            double yz = y * z;
            double wx = w * x;
            double wy = w * y;
            double wz = w * z;

            // Rotation matrix (3x3), then scale columns by sx/sy/sz
            double m00 = (1.0 - 2.0 * (yy + zz)) * sx;
            double m01 = (2.0 * (xy - wz)) * sy;
            double m02 = (2.0 * (xz + wy)) * sz;

            double m10 = (2.0 * (xy + wz)) * sx;
            double m11 = (1.0 - 2.0 * (xx + zz)) * sy;
            double m12 = (2.0 * (yz - wx)) * sz;

            double m20 = (2.0 * (xz - wy)) * sx;
            double m21 = (2.0 * (yz + wx)) * sy;
            double m22 = (1.0 - 2.0 * (xx + yy)) * sz;

            return new DoubleMatrix3x4(m00, m01, m02, currentTranslation.x,
                                       m10, m11, m12, currentTranslation.y,
                                       m20, m21, m22, currentTranslation.z);
        }

        public static DoubleMatrix3x4 identity => new DoubleMatrix3x4(1.0, 0.0, 0.0, 0.0,
                                                                      0.0, 1.0, 0.0, 0.0,
                                                                      0.0, 0.0, 1.0, 0.0);
    }
}
