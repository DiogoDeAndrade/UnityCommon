using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;

namespace UC
{
    [System.Serializable]
    public struct DualQuaternion
    {
        public Quaternion real; // rotation
        public Quaternion dual; // translation part

        public static DualQuaternion identity => new DualQuaternion(Quaternion.identity, new Quaternion(0f, 0f, 0f, 0f));
        public static DualQuaternion zero => new DualQuaternion(new Quaternion(0f, 0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f));

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
            if (mag <= 0f)
            {
                real = Quaternion.identity;
                dual = new Quaternion(0f, 0f, 0f, 0f);
                return;
            }

            real = real.Divide(mag);
            dual = dual.Divide(mag);

            float dot = Quaternion.Dot(real, dual);
            dual = new Quaternion(
                dual.x - real.x * dot,
                dual.y - real.y * dot,
                dual.z - real.z * dot,
                dual.w - real.w * dot
            );
        }

        public static DualQuaternion Normalized(DualQuaternion dq)
        {
            dq.Normalize();
            return dq;
        }

        public static DualQuaternion operator *(DualQuaternion dq, float s)
        {
            return new DualQuaternion(new Quaternion(dq.real.x * s, dq.real.y * s, dq.real.z * s, dq.real.w * s),
                                      new Quaternion(dq.dual.x * s, dq.dual.y * s, dq.dual.z * s, dq.dual.w * s));
        }

        public static DualQuaternion operator *(DualQuaternion a, DualQuaternion b)
        {
            return new DualQuaternion(
                a.real * b.real,
                (a.real * b.dual).Add(a.dual * b.real)
            );
        }

        public static DualQuaternion operator +(DualQuaternion a, DualQuaternion b)
        {
            return new DualQuaternion(new Quaternion(a.real.x + b.real.x, a.real.y + b.real.y, a.real.z + b.real.z, a.real.w + b.real.w),   
                                      new Quaternion(a.dual.x + b.dual.x, a.dual.y + b.dual.y, a.dual.z + b.dual.z, a.dual.w + b.dual.w));
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
            Quaternion rInv = Quaternion.Inverse(real);
            Quaternion dInv = (rInv * dual * rInv).Negate();
            return new DualQuaternion(rInv, dInv);
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

        // Blend weight some quaternions together, then normalize the result.
        public static DualQuaternion BlendWeighted(IList<DualQuaternion> dqs, IList<float> weights)
        {
            if ((dqs == null) || (weights == null) || (dqs.Count == 0) || (dqs.Count != weights.Count)) return identity;

            int first = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    first = i;
                    break;
                }
            }

            if (first < 0) return identity;

            Quaternion reference = dqs[first].real;
            DualQuaternion accum = zero;

            for (int i = 0; i < dqs.Count; i++)
            {
                float w = weights[i];
                if (w <= 0f) continue;

                DualQuaternion dq = dqs[i];

                // Hemisphere correction
                if (Quaternion.Dot(reference, dq.real) < 0f)
                {
                    dq.real = new Quaternion(-dq.real.x, -dq.real.y, -dq.real.z, -dq.real.w);
                    dq.dual = new Quaternion(-dq.dual.x, -dq.dual.y, -dq.dual.z, -dq.dual.w);
                }

                accum += dq * w;
            }

            accum.Normalize();
            return accum;
        }

        public Vector3 TransformPoint(Vector3 p)
        {
            GetRotationTranslation(out Quaternion rotation, out Vector3 translation);
            return rotation * p + translation;
        }
    }
}
