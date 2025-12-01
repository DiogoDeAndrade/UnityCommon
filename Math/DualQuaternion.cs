using UnityEngine;

namespace UC
{
    [System.Serializable]
    public struct DualQuaternion
    {
        public Quaternion real; // rotation
        public Quaternion dual; // translation part

        public DualQuaternion(Quaternion real, Quaternion dual)
        {
            this.real = real;
            this.dual = dual;
        }

        public DualQuaternion(Vector3 translation, Quaternion rotation)
        {
            this.real = this.dual = Quaternion.identity;
            FromRotationTranslation(rotation, translation);
        }

        public void FromRotationTranslation(Quaternion rotation, Vector3 translation)
        {
            Quaternion t = new Quaternion(translation.x, translation.y, translation.z, 0f);
            Quaternion dual = (t * rotation).Multiply(0.5f);

            this.real = rotation;
            this.dual = dual;

            Normalize();
        }

        public void FromMatrix4x4(Matrix4x4 m)
        {
            Quaternion r = m.rotation;
            Vector3 t = m.GetColumn(3);
            
            FromRotationTranslation(r, t);
        }

        public void Normalize()
        {
            float mag = Mathf.Sqrt(Quaternion.Dot(real, real));
            if (mag > 0f)
            {
                real = real.Divide(mag);
                dual = dual.Divide(mag);
            }
        }

        public static DualQuaternion Normalized(DualQuaternion dq)
        {
            dq.Normalize();
            return dq;
        }

        public static DualQuaternion operator *(DualQuaternion a, DualQuaternion b)
        {
            return new DualQuaternion(
                a.real * b.real,
                (a.real * b.dual).Add(a.dual * b.real)
            );
        }

        public DualQuaternion Conjugate()
        {
            return new DualQuaternion(
                new Quaternion(-real.x, -real.y, -real.z, real.w),
                new Quaternion(-dual.x, -dual.y, -dual.z, dual.w)
            );
        }

        public DualQuaternion Inverse()
        {
            DualQuaternion c = Conjugate();
            // For unit dual quaternion, inverse = (real*, -dual*)
            c.dual = new Quaternion(-c.dual.x, -c.dual.y, -c.dual.z, -c.dual.w);
            return c;
        }

        public void GetRotationTranslation(out Quaternion rotation, out Vector3 translation)
        {
            DualQuaternion dq = this;
            dq.Normalize();

            rotation = dq.real;

            // t = 2 * (dual * real^-1).xyz
            Quaternion rinv = Quaternion.Inverse(dq.real);
            Quaternion tQ = (dq.dual * rinv).Multiply(2.0f);
            translation = new Vector3(tQ.x, tQ.y, tQ.z);
        }
        
        public Matrix4x4 ToMatrix4x4()
        {
            GetRotationTranslation(out Quaternion r, out Vector3 t);
            return Matrix4x4.TRS(t, r, Vector3.one);
        }

        
        /// Normalized linear blending of dual quaternions (DQS-style).
        /// This is the common dual quaternion skinning blend.
        public static DualQuaternion Lerp(DualQuaternion a, DualQuaternion b, float t)
        {
            // Ensure shortest path (like quaternions)
            if (Quaternion.Dot(a.real, b.real) < 0f)
            {
                b.real = new Quaternion(-b.real.x, -b.real.y, -b.real.z, -b.real.w);
                b.dual = new Quaternion(-b.dual.x, -b.dual.y, -b.dual.z, -b.dual.w);
            }

            DualQuaternion result = new DualQuaternion
            (
                QuaternionExtension.UnnormalizedLerp(a.real, b.real, t),
                QuaternionExtension.UnnormalizedLerp(a.dual, b.dual, t)
            );

            result.Normalize();
            return result;
        }

        /// Screw-like interpolation between two dual quaternions.
        /// Uses SLERP for rotation and a blended dual part, then normalizes.
        ///
        /// This yields a nicer screw-style motion than pure linear blend,
        /// while still being relatively cheap and robust.
        public static DualQuaternion ScrewInterpolate(DualQuaternion a, DualQuaternion b, float t)
        {
            // Ensure shortest rotation path
            if (Quaternion.Dot(a.real, b.real) < 0f)
            {
                b.real = new Quaternion(-b.real.x, -b.real.y, -b.real.z, -b.real.w);
                b.dual = new Quaternion(-b.dual.x, -b.dual.y, -b.dual.z, -b.dual.w);
            }

            // SLERP on the rotation (real part)
            Quaternion real = QuaternionExtension.UnnormalizedSlerp(a.real, b.real, t);

            // LERP on the dual part (translation-related)
            Quaternion dual = QuaternionExtension.UnnormalizedLerp(a.dual, b.dual, t);

            DualQuaternion result = new DualQuaternion(real, dual);
            result.Normalize();
            return result;
        }
        
        public Vector3 TransformPoint(Vector3 p)
        {
            DualQuaternion dq = this;
            dq.Normalize();

            Quaternion r = dq.real;
            Quaternion d = dq.dual;

            // Rotate point
            Quaternion rp = r * new Quaternion(p.x, p.y, p.z, 0f) * Quaternion.Inverse(r);

            // Extract translation
            Quaternion t = (d * Quaternion.Inverse(r)).Multiply(2.0f);

            return new Vector3(rp.x + t.x, rp.y + t.y, rp.z + t.z);
        }
    }
}
