using System;
using UC;
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
            public Matrix3x3         rotation;
            public Matrix3x3         stretch;
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

            Matrix3x3 linear = new Matrix3x3(m);

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
            Matrix3x3 stretchSum = default;
            Vector3 principalSum = Vector3.zero;
            Matrix3x3 rotationSum = default;

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

            Matrix3x3 rotation = BlendRotation(position, trilinear, rotationSum, referenceNode);

            Matrix3x3 stretch = (scaleBlend == EDFieldScaleBlend.Diagonal)
                         ? (Matrix3x3.FromDiagonal(principalSum * invWeightSum))
                         : ((stretchSum * invWeightSum).symmetrized);

            matrix = (rotation * stretch).ToMatrix(translation);

            return true;
        }

        private Matrix3x3 BlendRotation(Vector3 position, bool trilinear, Matrix3x3 weightedMean, int referenceNode)
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

        private Matrix3x3 BlendRotationNlerp(Vector3 position, bool trilinear, int referenceNode)
        {
            if (!TryGetParts(referenceNode, out NodeParts reference)) return Matrix3x3.identity;

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

            return new Matrix3x3(new Quaternion(x * invLength, y * invLength, z * invLength, w * invLength));
        }

        private Matrix3x3 BlendRotationKarcher(Vector3 position, bool trilinear, Matrix3x3 initial)
        {
            Matrix3x3 current = initial;

            for (int iteration = 0; iteration < KarcherMaxIterations; iteration++)
            {
                Matrix3x3 currentTranspose = current.transposed;

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

                current = current * Matrix3x3.FromRotationVector(update);

                if (update.sqrMagnitude < KarcherTolerance * KarcherTolerance) break;
            }

            return current;
        }

        /// <summary>
        /// The rotation closest to a matrix - its orthogonal polar factor, discarding the stretch.
        /// </summary>
        private static Matrix3x3 ProjectToRotation(Matrix3x3 m)
        {
            PolarDecompose(m, out Matrix3x3 rotation, out _);

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
        public static void PolarDecompose(Matrix3x3 m, out Matrix3x3 rotation, out Matrix3x3 stretch)
        {
            Matrix3x3 current = m;

            for (int iteration = 0; iteration < 24; iteration++)
            {
                if (!current.TryInvert(out Matrix3x3 inverse))
                {
                    // Singular, so there is no polar factor to find. Falling back to the identity
                    // rotation puts the whole of a degenerate transform into the stretch, which keeps
                    // the recomposition exact rather than inventing a rotation for it.
                    rotation = Matrix3x3.identity;
                    stretch = m;

                    return;
                }

                Matrix3x3 inverseTranspose = inverse.transposed;

                float currentNorm = Mathf.Sqrt(current.sumOfSquares);
                float inverseNorm = Mathf.Sqrt(inverseTranspose.sumOfSquares);

                float gamma = ((currentNorm > 1e-12f) && (inverseNorm > 1e-12f))
                            ? (Mathf.Sqrt(inverseNorm / currentNorm))
                            : (1.0f);

                Matrix3x3 next = (current * gamma + inverseTranspose * (1.0f / gamma)) * 0.5f;

                float change = (next - current).sumOfSquares;

                current = next;

                if (change < 1e-14f) break;
            }

            rotation = current;
            stretch = (rotation.transposed * m).symmetrized;

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
        public static Vector3 SymmetricEigenvalues(Matrix3x3 s)
        {
            Matrix3x3 a = s;

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
        private static void RotateJacobi(ref Matrix3x3 a, int p, int q)
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

            Matrix3x3 rotation = Matrix3x3.identity;

            Set(ref rotation, p, p, c);
            Set(ref rotation, q, q, c);
            Set(ref rotation, p, q, s);
            Set(ref rotation, q, p, -s);

            a = (rotation.transposed * a * rotation).symmetrized;
        }

        private static float Get(Matrix3x3 m, int row, int column)
        {
            if (row == 0) return (column == 0) ? (m.m00) : ((column == 1) ? (m.m01) : (m.m02));
            if (row == 1) return (column == 0) ? (m.m10) : ((column == 1) ? (m.m11) : (m.m12));

            return (column == 0) ? (m.m20) : ((column == 1) ? (m.m21) : (m.m22));
        }

        private static void Set(ref Matrix3x3 m, int row, int column, float value)
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
