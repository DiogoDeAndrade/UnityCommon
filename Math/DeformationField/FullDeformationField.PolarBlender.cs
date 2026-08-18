using System;
using UnityEngine;

/// <summary>
/// How the rotational parts of the influencing nodes' transforms are averaged.
///
/// All three are order-independent, which is a requirement rather than a preference here: the
/// influence order is arbitrary-but-deterministic, so anything order-dependent would quietly make
/// SortInfluences part of the result. That is why sequential slerp is absent - it is the obvious
/// fourth option and it is the one that would do exactly that.
///
/// They agree closely when the rotations are clustered and diverge when they are far apart, which is
/// precisely the regime that produces the inversions - so this is not a choice between equivalent
/// implementations of one idea.
/// </summary>
public enum EDFieldRotationBlend
{
    /// <summary>
    /// The exact minimiser of the weighted chordal distance, sum of w*||R - Ri||_F^2: average the
    /// rotation matrices and project the result back onto SO(3).
    ///
    /// The smallest possible departure from the linear blend - blend the same way, then put the
    /// result back in the group - which makes it the one whose result is easiest to attribute.
    /// </summary>
    Chordal,
    /// <summary>
    /// Normalized linear blend of quaternions. Cheapest, and an approximation of Chordal rather than
    /// a different objective. Needs hemisphere alignment because of the double cover, and degrades
    /// when the rotations are far apart, where the sum approaches zero.
    /// </summary>
    Nlerp,
    /// <summary>
    /// The intrinsic (Karcher/Frechet) mean: the minimiser of the weighted squared geodesic distance
    /// on SO(3), found by iteration. The principled answer and the slowest - worth having as the
    /// reference the cheap ones are measured against rather than as a default.
    /// </summary>
    Karcher
}

/// <summary>
/// What is done with the non-rotational part of each node's transform.
///
/// The polar decomposition M = R*S puts everything that is not rotation into S, which is symmetric
/// and carries scale and shear together. These two are the journal's formulations 2 and 1.
/// </summary>
public enum EDFieldScaleBlend
{
    /// <summary>
    /// Blend S whole. Shear is preserved, because S carries it - a convex combination of symmetric
    /// matrices is symmetric, so the result is still a valid stretch.
    /// </summary>
    Full,
    /// <summary>
    /// Blend only the principal stretches, discarding the axes they act along, and reattach them to
    /// the blended rotation's frame. This is "translation, rotation and scale" in the usual sense and
    /// it is strictly weaker than Full - the loss is the orientation of each node's stretch.
    /// </summary>
    Diagonal
}

public partial class FullDeformationField
{
    /// <summary>
    /// The 3x3 linear part, on its own.
    ///
    /// Matrix4x4 cannot express these operations without dragging the translation row and column
    /// through them - an inverse of an affine matrix is not the inverse of its linear part - and the
    /// polar iteration below inverts on every step. Small, immutable and by value, so nothing here
    /// allocates or is shared between threads.
    /// </summary>
    internal struct Mat3
    {
        public float m00, m01, m02;
        public float m10, m11, m12;
        public float m20, m21, m22;

        public static Mat3 identity => new Mat3 { m00 = 1.0f, m11 = 1.0f, m22 = 1.0f };

        public static Mat3 FromLinearPart(Matrix4x4 m)
        {
            return new Mat3
            {
                m00 = m.m00, m01 = m.m01, m02 = m.m02,
                m10 = m.m10, m11 = m.m11, m12 = m.m12,
                m20 = m.m20, m21 = m.m21, m22 = m.m22
            };
        }

        /// <summary>The affine matrix with this linear part and the given translation column.</summary>
        public Matrix4x4 ToAffine(Vector3 translation)
        {
            Matrix4x4 m = Matrix4x4.identity;

            m.m00 = m00; m.m01 = m01; m.m02 = m02; m.m03 = translation.x;
            m.m10 = m10; m.m11 = m11; m.m12 = m12; m.m13 = translation.y;
            m.m20 = m20; m.m21 = m21; m.m22 = m22; m.m23 = translation.z;

            return m;
        }

        public static Mat3 operator *(Mat3 a, Mat3 b)
        {
            return new Mat3
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

        public static Mat3 operator *(Mat3 a, float s)
        {
            return new Mat3
            {
                m00 = a.m00 * s, m01 = a.m01 * s, m02 = a.m02 * s,
                m10 = a.m10 * s, m11 = a.m11 * s, m12 = a.m12 * s,
                m20 = a.m20 * s, m21 = a.m21 * s, m22 = a.m22 * s
            };
        }

        public static Mat3 operator +(Mat3 a, Mat3 b)
        {
            return new Mat3
            {
                m00 = a.m00 + b.m00, m01 = a.m01 + b.m01, m02 = a.m02 + b.m02,
                m10 = a.m10 + b.m10, m11 = a.m11 + b.m11, m12 = a.m12 + b.m12,
                m20 = a.m20 + b.m20, m21 = a.m21 + b.m21, m22 = a.m22 + b.m22
            };
        }

        public Mat3 transpose => new Mat3
        {
            m00 = m00, m01 = m10, m02 = m20,
            m10 = m01, m11 = m11, m12 = m21,
            m20 = m02, m21 = m12, m22 = m22
        };

        public float determinant =>
            m00 * (m11 * m22 - m12 * m21) -
            m01 * (m10 * m22 - m12 * m20) +
            m02 * (m10 * m21 - m11 * m20);

        /// <summary>False for a singular matrix, which the polar iteration has to stop on rather than
        /// divide by.</summary>
        public bool TryInvert(out Mat3 inverse)
        {
            float det = determinant;

            if (Mathf.Abs(det) < 1e-12f)
            {
                inverse = identity;
                return false;
            }

            float invDet = 1.0f / det;

            inverse = new Mat3
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

        /// <summary>Sum of squared entries. Used as the convergence measure, so it never needs a root.</summary>
        public float sumOfSquares =>
            m00 * m00 + m01 * m01 + m02 * m02 +
            m10 * m10 + m11 * m11 + m12 * m12 +
            m20 * m20 + m21 * m21 + m22 * m22;

        public static Mat3 Difference(Mat3 a, Mat3 b) => a + (b * -1.0f);

        /// <summary>Averages the matrix with its own transpose. The scale factor is symmetric in exact
        /// arithmetic and drifts off it in float, and every consumer below assumes symmetry.</summary>
        public Mat3 Symmetrized()
        {
            float xy = 0.5f * (m01 + m10);
            float xz = 0.5f * (m02 + m20);
            float yz = 0.5f * (m12 + m21);

            return new Mat3
            {
                m00 = m00, m01 = xy,  m02 = xz,
                m10 = xy,  m11 = m11, m12 = yz,
                m20 = xz,  m21 = yz,  m22 = m22
            };
        }

        public static Mat3 Diagonal(Vector3 d) => new Mat3 { m00 = d.x, m11 = d.y, m22 = d.z };

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

        public static Mat3 FromQuaternion(Quaternion q)
        {
            float x = q.x, y = q.y, z = q.z, w = q.w;

            return new Mat3
            {
                m00 = 1.0f - 2.0f * (y * y + z * z), m01 = 2.0f * (x * y - z * w),        m02 = 2.0f * (x * z + y * w),
                m10 = 2.0f * (x * y + z * w),        m11 = 1.0f - 2.0f * (x * x + z * z), m12 = 2.0f * (y * z - x * w),
                m20 = 2.0f * (x * z - y * w),        m21 = 2.0f * (y * z + x * w),        m22 = 1.0f - 2.0f * (x * x + y * y)
            };
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

        /// <summary>Rodrigues: the rotation matrix of a rotation vector - the SO(3) exponential.</summary>
        public static Mat3 FromRotationVector(Vector3 v)
        {
            float angle = v.magnitude;

            if (angle < 1e-8f) return identity;

            Vector3 axis = v / angle;

            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            float t = 1.0f - c;

            float x = axis.x, y = axis.y, z = axis.z;

            return new Mat3
            {
                m00 = t * x * x + c,     m01 = t * x * y - s * z, m02 = t * x * z + s * y,
                m10 = t * x * y + s * z, m11 = t * y * y + c,     m12 = t * y * z - s * x,
                m20 = t * x * z - s * y, m21 = t * y * z + s * x, m22 = t * z * z + c
            };
        }
    }

    /// <summary>
    /// Decomposed blending: every node's transform is split into translation, rotation and stretch,
    /// each part is combined in the space where it lives, and the result is recomposed.
    ///
    /// **The decomposition is polar**, M = R*S with R orthogonal and S symmetric. One decomposition
    /// serves both scale modes: S carries scale and shear together, so Full keeps the shear and
    /// Diagonal throws it away by taking only S's eigenvalues. An SVD would produce the same rotation
    /// and the same principal stretches by a longer route.
    ///
    /// **Reflections go into S, never into R.** A node whose transform has already inverted gives a
    /// polar factor with determinant -1, and blending that as though it were a rotation is
    /// meaningless. Negating both factors leaves M unchanged, puts R back in SO(3), and leaves the
    /// inversion visible as a negative eigenvalue of S - which is where EDDeterminantTerm is already
    /// looking for it.
    ///
    /// **It inherits DeformPosition from the base**, unlike the linear blend. Combining once and then
    /// acting is what a decomposed blend means; applying each node's transform separately and
    /// averaging the results would be a different operation, not a faster spelling of this one.
    ///
    /// The rotation and stretch parts act about the world origin, with translation carrying the rest,
    /// exactly as the linear blend does. That is a real limitation of both - a blend pivoted on the
    /// point being deformed would behave better far from the origin - but it is deliberately not
    /// changed here, so that this differs from the baseline in the decomposition and nothing else.
    /// </summary>
    public sealed class PolarBlender : TransformBlender
    {
        /// <summary>
        /// Everything the blend needs from one node, computed once per pass.
        ///
        /// Held together rather than in parallel arrays because they are always read together, and
        /// because the perturbation override then replaces one value instead of five.
        /// </summary>
        private struct NodeParts
        {
            public bool         valid;
            public Vector3      translation;
            public Mat3         rotation;
            public Mat3         stretch;
            public Quaternion   quaternion;
            /// <summary>S's eigenvalues, descending. Only filled for the Diagonal scale mode.</summary>
            public Vector3      principalStretch;
        }

        private readonly EDFieldRotationBlend   rotationBlend;
        private readonly EDFieldScaleBlend      scaleBlend;
        private readonly NodeParts[]            nodeParts;

        private NodeParts                       overrideParts;

        /// <summary>
        /// How many times the intrinsic mean refines. It converges quadratically for clustered
        /// rotations, so this is a runaway guard rather than a quality setting - the loop leaves as
        /// soon as the update stops moving.
        /// </summary>
        private const int   KarcherMaxIterations = 8;
        private const float KarcherTolerance = 1e-7f;

        public PolarBlender(FullDeformationField field, Func<int, Frame?> getCurrentNodeFrame,
                            EDFieldRotationBlend rotationBlend, EDFieldScaleBlend scaleBlend)
            : base(field, getCurrentNodeFrame)
        {
            this.rotationBlend = rotationBlend;
            this.scaleBlend = scaleBlend;

            int count = field.deformationNodeCount;

            nodeParts = new NodeParts[count];

            for (int i = 0; i < count; i++)
            {
                if (!TryGetNodeMatrix(i, out Matrix4x4 m)) continue;

                nodeParts[i] = Decompose(m);
            }
        }

        public override string Describe() => DescribeBlend(EDFieldBlendMode.Polar, rotationBlend, scaleBlend);

        public override void SetNodeOverride(int nodeIndex, Frame frame)
        {
            base.SetNodeOverride(nodeIndex, frame);

            // Only if the base accepted it - an out-of-range index leaves the frozen state alone, and
            // decomposing here would then describe a node nothing is going to ask about.
            if (overriddenNodeIndex != nodeIndex) return;

            TryGetNodeMatrix(nodeIndex, out Matrix4x4 m);

            overrideParts = Decompose(m);
        }

        /// <summary>
        /// The decomposition in force for a node: the perturbed one while a column of the clearance
        /// Jacobian is being measured, the frozen one otherwise.
        /// </summary>
        private bool TryGetParts(int nodeIndex, out NodeParts parts)
        {
            if (nodeIndex == overriddenNodeIndex)
            {
                parts = overrideParts;

                return parts.valid;
            }

            if ((nodeIndex < 0) || (nodeIndex >= nodeParts.Length))
            {
                parts = default;

                return false;
            }

            parts = nodeParts[nodeIndex];

            return parts.valid;
        }

        private NodeParts Decompose(Matrix4x4 m)
        {
            NodeParts parts = default;

            parts.valid = true;
            parts.translation = new Vector3(m.m03, m.m13, m.m23);

            Mat3 linear = Mat3.FromLinearPart(m);

            PolarDecompose(linear, out parts.rotation, out parts.stretch);

            parts.quaternion = parts.rotation.ToQuaternion();

            if (scaleBlend == EDFieldScaleBlend.Diagonal)
            {
                parts.principalStretch = SymmetricEigenvalues(parts.stretch);
            }

            return parts;
        }

        public override bool TryGetMatrix(Vector3 position, bool trilinear, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;

            Vector3 translation = Vector3.zero;
            Mat3 stretchSum = default;
            Vector3 principalSum = Vector3.zero;
            Mat3 rotationSum = default;

            float weightSum = 0.0f;

            // The influence carrying the most weight, ties broken by the lower node index. Nlerp needs
            // it as the hemisphere reference and Karcher as a starting point, and both would otherwise
            // be reading the influence order - which is deterministic but arbitrary, and so is exactly
            // the kind of thing that must not reach the result. The same rule SortInfluences uses.
            int referenceNode = -1;
            float referenceWeight = 0.0f;

            InfluenceEnumerator influences = field.EnumerateInfluences(position, trilinear);

            while (influences.MoveNext(out int nodeIndex, out float weight))
            {
                if (!TryGetParts(nodeIndex, out NodeParts parts)) continue;

                translation += weight * parts.translation;

                if (scaleBlend == EDFieldScaleBlend.Diagonal) principalSum += weight * parts.principalStretch;
                else                                          stretchSum = stretchSum + (parts.stretch * weight);

                // Accumulated whatever the rotation mode, because Chordal uses it as its answer and
                // Karcher as its initial guess. Nlerp is the only one that ignores it.
                rotationSum = rotationSum + (parts.rotation * weight);

                if ((referenceNode < 0) || (weight > referenceWeight))
                {
                    referenceNode = nodeIndex;
                    referenceWeight = weight;
                }

                weightSum += weight;
            }

            if (weightSum <= DistanceEpsilon) return false;

            float invWeightSum = 1.0f / weightSum;

            translation *= invWeightSum;
            rotationSum = rotationSum * invWeightSum;

            Mat3 rotation = BlendRotation(position, trilinear, rotationSum, referenceNode);

            Mat3 stretch = (scaleBlend == EDFieldScaleBlend.Diagonal)
                         ? (Mat3.Diagonal(principalSum * invWeightSum))
                         : ((stretchSum * invWeightSum).Symmetrized());

            matrix = (rotation * stretch).ToAffine(translation);

            return true;
        }

        private Mat3 BlendRotation(Vector3 position, bool trilinear, Mat3 weightedMean, int referenceNode)
        {
            switch (rotationBlend)
            {
                case EDFieldRotationBlend.Nlerp:
                    return BlendRotationNlerp(position, trilinear, referenceNode);

                case EDFieldRotationBlend.Karcher:
                    return BlendRotationKarcher(position, trilinear, ProjectToRotation(weightedMean));

                default:
                    // The exact chordal mean is the orthogonal polar factor of the averaged rotation
                    // matrices. Which is to say: blend exactly as the linear blend does, then put the
                    // result back on SO(3).
                    return ProjectToRotation(weightedMean);
            }
        }

        private Mat3 BlendRotationNlerp(Vector3 position, bool trilinear, int referenceNode)
        {
            if (!TryGetParts(referenceNode, out NodeParts reference)) return Mat3.identity;

            Quaternion referenceQuaternion = reference.quaternion;

            float x = 0.0f, y = 0.0f, z = 0.0f, w = 0.0f;

            // A second pass rather than a running alignment, because aligning against whatever arrived
            // first is an order dependency wearing a disguise.
            InfluenceEnumerator influences = field.EnumerateInfluences(position, trilinear);

            while (influences.MoveNext(out int nodeIndex, out float weight))
            {
                if (!TryGetParts(nodeIndex, out NodeParts parts)) continue;

                Quaternion q = parts.quaternion;

                // q and -q are the same rotation; summing them unaligned cancels instead of averaging.
                float dot = q.x * referenceQuaternion.x + q.y * referenceQuaternion.y +
                            q.z * referenceQuaternion.z + q.w * referenceQuaternion.w;

                float sign = (dot < 0.0f) ? (-weight) : (weight);

                x += sign * q.x;
                y += sign * q.y;
                z += sign * q.z;
                w += sign * q.w;
            }

            float length = Mathf.Sqrt(x * x + y * y + z * z + w * w);

            // The rotations were spread far enough that the sum cancelled. There is no meaningful
            // linear answer here, and the reference is the least arbitrary thing left.
            if (length < 1e-6f) return reference.rotation;

            float invLength = 1.0f / length;

            return Mat3.FromQuaternion(new Quaternion(x * invLength, y * invLength, z * invLength, w * invLength));
        }

        private Mat3 BlendRotationKarcher(Vector3 position, bool trilinear, Mat3 initial)
        {
            Mat3 current = initial;

            for (int iteration = 0; iteration < KarcherMaxIterations; iteration++)
            {
                Mat3 currentTranspose = current.transpose;

                Vector3 update = Vector3.zero;
                float weightSum = 0.0f;

                // Re-walked per iteration. The update is a weighted mean of the logarithms taken at
                // the current estimate, so every influence is needed again each time - this mode is
                // the expensive one by construction, and is here as the reference the cheap ones are
                // measured against.
                InfluenceEnumerator influences = field.EnumerateInfluences(position, trilinear);

                while (influences.MoveNext(out int nodeIndex, out float weight))
                {
                    if (!TryGetParts(nodeIndex, out NodeParts parts)) continue;

                    update += weight * (currentTranspose * parts.rotation).ToRotationVector();

                    weightSum += weight;
                }

                if (weightSum <= DistanceEpsilon) break;

                update /= weightSum;

                current = current * Mat3.FromRotationVector(update);

                if (update.sqrMagnitude < KarcherTolerance * KarcherTolerance) break;
            }

            return current;
        }

        /// <summary>
        /// The rotation closest to a matrix - its orthogonal polar factor, discarding the stretch.
        /// </summary>
        private static Mat3 ProjectToRotation(Mat3 m)
        {
            PolarDecompose(m, out Mat3 rotation, out _);

            return rotation;
        }

        /// <summary>
        /// M = rotation * stretch, with rotation in SO(3) and stretch symmetric.
        ///
        /// Newton's iteration, R = (R + R^-T)/2, which converges quadratically to the orthogonal
        /// factor. The per-step scaling is Higham's: without it the iteration is slow for a badly
        /// conditioned matrix, and a badly conditioned node transform is exactly the case this method
        /// exists to handle well.
        ///
        /// A determinant at or below zero is not repaired here. It is carried into the stretch by the
        /// sign flip at the end, where it stays visible.
        /// </summary>
        internal static void PolarDecompose(Mat3 m, out Mat3 rotation, out Mat3 stretch)
        {
            Mat3 current = m;

            for (int iteration = 0; iteration < 24; iteration++)
            {
                if (!current.TryInvert(out Mat3 inverse))
                {
                    // Singular, so there is no polar factor to find. Falling back to the identity
                    // rotation puts the whole of a degenerate transform into the stretch, which keeps
                    // the recomposition exact rather than inventing a rotation for it.
                    rotation = Mat3.identity;
                    stretch = m;

                    return;
                }

                Mat3 inverseTranspose = inverse.transpose;

                float currentNorm = Mathf.Sqrt(current.sumOfSquares);
                float inverseNorm = Mathf.Sqrt(inverseTranspose.sumOfSquares);

                float gamma = ((currentNorm > 1e-12f) && (inverseNorm > 1e-12f))
                            ? (Mathf.Sqrt(inverseNorm / currentNorm))
                            : (1.0f);

                Mat3 next = (current * gamma + inverseTranspose * (1.0f / gamma)) * 0.5f;

                float change = Mat3.Difference(next, current).sumOfSquares;

                current = next;

                if (change < 1e-14f) break;
            }

            rotation = current;
            stretch = (rotation.transpose * m).Symmetrized();

            // A reflection is not a rotation, and averaging one as though it were is meaningless.
            // Negating both factors leaves the product unchanged - three dimensions, so the sign
            // survives the determinant - puts the rotation back in SO(3), and leaves the inversion
            // where it belongs, as a negative eigenvalue of the stretch.
            if (rotation.determinant < 0.0f)
            {
                rotation = rotation * -1.0f;
                stretch = stretch * -1.0f;
            }
        }

        /// <summary>
        /// The eigenvalues of a symmetric matrix, descending - which for the polar stretch factor are
        /// the principal stretches, and the singular values of the transform it came from.
        ///
        /// Cyclic Jacobi. Only the eigenvalues are kept, because the Diagonal scale mode is defined by
        /// discarding the axes they act along - that loss is the whole content of the mode, not an
        /// approximation in how it is computed.
        ///
        /// Sorted so that the largest stretch of one node blends with the largest of another. There is
        /// no correspondence between two nodes' stretch axes to do better with, which is the same
        /// statement from the other side.
        /// </summary>
        internal static Vector3 SymmetricEigenvalues(Mat3 s)
        {
            Mat3 a = s;

            for (int sweep = 0; sweep < 12; sweep++)
            {
                float off = a.m01 * a.m01 + a.m02 * a.m02 + a.m12 * a.m12;

                if (off < 1e-16f) break;

                RotateJacobi(ref a, 0, 1);
                RotateJacobi(ref a, 0, 2);
                RotateJacobi(ref a, 1, 2);
            }

            float e0 = a.m00, e1 = a.m11, e2 = a.m22;

            // Three values, so a sorting network is shorter than anything that would call itself a
            // sort.
            if (e0 < e1) (e0, e1) = (e1, e0);
            if (e1 < e2) (e1, e2) = (e2, e1);
            if (e0 < e1) (e0, e1) = (e1, e0);

            return new Vector3(e0, e1, e2);
        }

        /// <summary>
        /// One Jacobi rotation, zeroing the (p, q) off-diagonal entry of a symmetric matrix.
        ///
        /// Only the eigenvalues are wanted, so the accumulated eigenvector basis is not tracked - the
        /// rotation is applied to the matrix and discarded.
        /// </summary>
        private static void RotateJacobi(ref Mat3 a, int p, int q)
        {
            float apq = Get(a, p, q);

            if (Mathf.Abs(apq) < 1e-12f) return;

            float app = Get(a, p, p);
            float aqq = Get(a, q, q);

            float theta = 0.5f * (aqq - app) / apq;
            float sign = (theta >= 0.0f) ? (1.0f) : (-1.0f);
            float t = sign / (Mathf.Abs(theta) + Mathf.Sqrt(theta * theta + 1.0f));

            float c = 1.0f / Mathf.Sqrt(t * t + 1.0f);
            float s = t * c;

            Mat3 rotation = Mat3.identity;

            Set(ref rotation, p, p, c);
            Set(ref rotation, q, q, c);
            Set(ref rotation, p, q, s);
            Set(ref rotation, q, p, -s);

            a = (rotation.transpose * a * rotation).Symmetrized();
        }

        private static float Get(Mat3 m, int row, int column)
        {
            if (row == 0) return (column == 0) ? (m.m00) : ((column == 1) ? (m.m01) : (m.m02));
            if (row == 1) return (column == 0) ? (m.m10) : ((column == 1) ? (m.m11) : (m.m12));

            return (column == 0) ? (m.m20) : ((column == 1) ? (m.m21) : (m.m22));
        }

        private static void Set(ref Mat3 m, int row, int column, float value)
        {
            if (row == 0)
            {
                if (column == 0) m.m00 = value; else if (column == 1) m.m01 = value; else m.m02 = value;
                return;
            }

            if (row == 1)
            {
                if (column == 0) m.m10 = value; else if (column == 1) m.m11 = value; else m.m12 = value;
                return;
            }

            if (column == 0) m.m20 = value; else if (column == 1) m.m21 = value; else m.m22 = value;
        }
    }
}
