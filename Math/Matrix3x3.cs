
using UnityEngine;

namespace UC
{

    /// <summary>
    /// A 3x3 matrix.
    /// </summary>
    public struct Matrix3x3
    {
        public float m00, m01, m02;
        public float m10, m11, m12;
        public float m20, m21, m22;

        public Matrix3x3(Matrix4x4 m)
        {
            m00 = m.m00;
            m01 = m.m01;
            m02 = m.m02;
            m10 = m.m10;
            m11 = m.m11;
            m12 = m.m12;
            m20 = m.m20;
            m21 = m.m21;
            m22 = m.m22;
        }

        public Matrix3x3(Quaternion q)
        {
            float x = q.x, y = q.y, z = q.z, w = q.w;

            m00 = 1.0f - 2.0f * (y * y + z * z);
            m01 = 2.0f * (x * y - z * w);
            m02 = 2.0f * (x * z + y * w);
            m10 = 2.0f * (x * y + z * w);
            m11 = 1.0f - 2.0f * (x * x + z * z);
            m12 = 2.0f * (y * z - x * w);
            m20 = 2.0f * (x * z - y * w);
            m21 = 2.0f * (y * z + x * w);
            m22 = 1.0f - 2.0f * (x * x + y * y);
        }

        public static Matrix3x3 identity => new Matrix3x3 { m00 = 1.0f, m11 = 1.0f, m22 = 1.0f };

        public static Matrix3x3 FromDiagonal(Vector3 d) => new Matrix3x3 { m00 = d.x, m11 = d.y, m22 = d.z };

        /// <summary>Rodrigues: the rotation matrix of a rotation vector - the SO(3) exponential.</summary>
        public static Matrix3x3 FromRotationVector(Vector3 v)
        {
            float angle = v.magnitude;

            if (angle < 1e-8f) return identity;

            Vector3 axis = v / angle;

            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            float t = 1.0f - c;

            float x = axis.x, y = axis.y, z = axis.z;

            return new Matrix3x3
            {
                m00 = t * x * x + c,
                m01 = t * x * y - s * z,
                m02 = t * x * z + s * y,
                m10 = t * x * y + s * z,
                m11 = t * y * y + c,
                m12 = t * y * z - s * x,
                m20 = t * x * z - s * y,
                m21 = t * y * z + s * x,
                m22 = t * z * z + c
            };
        }

        /// <summary>
        /// The affine matrix with this linear part and the given translation column.
        /// </summary>
        public Matrix4x4 ToMatrix(Vector3 translation)
        {
            Matrix4x4 m = Matrix4x4.identity;

            m.m00 = m00; m.m01 = m01; m.m02 = m02; m.m03 = translation.x;
            m.m10 = m10; m.m11 = m11; m.m12 = m12; m.m13 = translation.y;
            m.m20 = m20; m.m21 = m21; m.m22 = m22; m.m23 = translation.z;

            return m;
        }


        public Quaternion ToQuaternion()
        {
            // Shepperd's method: pick the branch whose denominator is largest, so the square root is
            // never taken of something near zero.
            float trace = m00 + m11 + m22;

            if (trace > 0.0f)
            {
                float s = Mathf.Sqrt(trace + 1.0f) * 2.0f;

                return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }

            if ((m00 > m11) && (m00 > m22))
            {
                float s = Mathf.Sqrt(1.0f + m00 - m11 - m22) * 2.0f;

                return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }

            if (m11 > m22)
            {
                float s = Mathf.Sqrt(1.0f + m11 - m00 - m22) * 2.0f;

                return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }

            float t = Mathf.Sqrt(1.0f + m22 - m00 - m11) * 2.0f;

            return new Quaternion((m02 + m20) / t, (m12 + m21) / t, 0.25f * t, (m10 - m01) / t);
        }

        /// <summary>
        /// The rotation vector (axis times angle) of a rotation matrix - the SO(3) logarithm.
        ///
        /// **Taken through the quaternion rather than off the matrix directly, and that is the whole
        /// point of it.** The textbook form divides the antisymmetric part by sin(angle), and the
        /// antisymmetric part is a difference of near-equal entries: at 180 degrees it vanishes while
        /// the divisor does too, and the ratio of two quantities that are both noise is noise. The
        /// obvious repair - a special case near pi that rebuilds the axis from the symmetric part -
        /// still leaves a band either side of the switch where neither branch is accurate. Measured
        /// at 179.9 degrees it was wrong by a fifth of a radian.
        ///
        /// Shepperd's method inside ToQuaternion already picks its branch by magnitude, so it is
        /// accurate at every angle; atan2 then recovers the angle with no cancellation anywhere,
        /// because near pi the vector part tends to one rather than to zero. Half the branches and no
        /// bad band.
        /// </summary>
        public Vector3 ToRotationVector()
        {
            Quaternion q = ToQuaternion();

            float x = q.x, y = q.y, z = q.z, w = q.w;

            // q and -q are the same rotation; the positive-w half is the one whose angle is the
            // shorter way round, which is what an averaging step wants to be moving along.
            if (w < 0.0f)
            {
                x = -x; y = -y; z = -z; w = -w;
            }

            float vectorLength = Mathf.Sqrt(x * x + y * y + z * z);

            if (vectorLength < 1e-12f) return Vector3.zero;

            float angle = 2.0f * Mathf.Atan2(vectorLength, w);

            float scale = angle / vectorLength;

            return new Vector3(x * scale, y * scale, z * scale);
        }

        public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
        {
            return new Matrix3x3
            {
                m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20,
                m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21,
                m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22,

                m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20,
                m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21,
                m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22,

                m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20,
                m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21,
                m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22
            };
        }

        /// <summary>
        /// The matrix applied to a vector, columns times components.
        ///
        /// A 3x3 has no translation, so there is no point-versus-vector distinction to get wrong
        /// here - unlike Matrix4x4, where MultiplyPoint3x4 and MultiplyVector differ and picking the
        /// wrong one is silent. That is the reason this is safe to spell as an operator.
        /// </summary>
        public static Vector3 operator *(Matrix3x3 m, Vector3 v)
        {
            return new Vector3(m.m00 * v.x + m.m01 * v.y + m.m02 * v.z,
                               m.m10 * v.x + m.m11 * v.y + m.m12 * v.z,
                               m.m20 * v.x + m.m21 * v.y + m.m22 * v.z);
        }

        public static Matrix3x3 operator *(Matrix3x3 a, float s)
        {
            return new Matrix3x3
            {
                m00 = a.m00 * s,
                m01 = a.m01 * s,
                m02 = a.m02 * s,
                m10 = a.m10 * s,
                m11 = a.m11 * s,
                m12 = a.m12 * s,
                m20 = a.m20 * s,
                m21 = a.m21 * s,
                m22 = a.m22 * s
            };
        }

        public static Matrix3x3 operator +(Matrix3x3 a, Matrix3x3 b)
        {
            return new Matrix3x3
            {
                m00 = a.m00 + b.m00,
                m01 = a.m01 + b.m01,
                m02 = a.m02 + b.m02,
                m10 = a.m10 + b.m10,
                m11 = a.m11 + b.m11,
                m12 = a.m12 + b.m12,
                m20 = a.m20 + b.m20,
                m21 = a.m21 + b.m21,
                m22 = a.m22 + b.m22
            };
        }
        public static Matrix3x3 operator -(Matrix3x3 a, Matrix3x3 b)
        {
            return new Matrix3x3
            {
                m00 = a.m00 - b.m00,
                m01 = a.m01 - b.m01,
                m02 = a.m02 - b.m02,
                m10 = a.m10 - b.m10,
                m11 = a.m11 - b.m11,
                m12 = a.m12 - b.m12,
                m20 = a.m20 - b.m20,
                m21 = a.m21 - b.m21,
                m22 = a.m22 - b.m22
            };
        }

        public Matrix3x3 transposed => new Matrix3x3
        {
            m00 = m00,
            m01 = m10,
            m02 = m20,
            m10 = m01,
            m11 = m11,
            m12 = m21,
            m20 = m02,
            m21 = m12,
            m22 = m22
        };

        public float determinant => m00 * (m11 * m22 - m12 * m21) - m01 * (m10 * m22 - m12 * m20) + m02 * (m10 * m21 - m11 * m20);
        public float sumOfSquares => m00 * m00 + m01 * m01 + m02 * m02 + m10 * m10 + m11 * m11 + m12 * m12 + m20 * m20 + m21 * m21 + m22 * m22;
        
        /// <summary>Averages the matrix with its own transpose, which is the nearest symmetric matrix
        /// in the Frobenius sense. For a quantity that is symmetric in exact arithmetic and drifts off
        /// it in float, this restores the property the algorithm downstream is relying on.</summary>
        public Matrix3x3 symmetrized
        {
            get
            {
                float xy = 0.5f * (m01 + m10);
                float xz = 0.5f * (m02 + m20);
                float yz = 0.5f * (m12 + m21);

                return new Matrix3x3
                {
                    m00 = m00,
                    m01 = xy,
                    m02 = xz,
                    m10 = xy,
                    m11 = m11,
                    m12 = yz,
                    m20 = xz,
                    m21 = yz,
                    m22 = m22
                };
            }
        }

        /// <summary>
        /// False for a singular matrix, rather than returning something the caller has to test the
        /// determinant of afterwards to know whether to trust.
        /// </summary>
        public bool TryInvert(out Matrix3x3 inverse)
        {
            float det = determinant;

            if (Mathf.Abs(det) < 1e-12f)
            {
                inverse = identity;
                return false;
            }

            float invDet = 1.0f / det;

            inverse = new Matrix3x3
            {
                m00 = (m11 * m22 - m12 * m21) * invDet,
                m01 = (m02 * m21 - m01 * m22) * invDet,
                m02 = (m01 * m12 - m02 * m11) * invDet,

                m10 = (m12 * m20 - m10 * m22) * invDet,
                m11 = (m00 * m22 - m02 * m20) * invDet,
                m12 = (m02 * m10 - m00 * m12) * invDet,

                m20 = (m10 * m21 - m11 * m20) * invDet,
                m21 = (m01 * m20 - m00 * m21) * invDet,
                m22 = (m00 * m11 - m01 * m10) * invDet
            };

            return true;
        }        
    }

}