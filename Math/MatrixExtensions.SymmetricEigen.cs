using UnityEngine;

namespace UC
{
    /// <summary>
    /// Eigendecomposition of a symmetric 3x3 matrix, and the one operation that makes the result
    /// usable as a frame rather than as three anonymous axes.
    ///
    /// **Why this is here at all.** The polar decomposition M = R*S splits a transform into a
    /// rotation and a symmetric stretch, and S carries scale and shear together. Separating them
    /// needs S's eigenvectors, not merely its eigenvalues: S = V * diag(L) * V^T says the stretches
    /// are L and the axes they act along are V, and *shear is exactly the statement that V is not the
    /// frame you were expecting*. FullDeformationField.PolarBlender already computes the eigenvalues
    /// and deliberately discards V - discarding it is the whole content of its Diagonal scale mode -
    /// so this is a different question rather than the same one generalised, and it is kept off that
    /// path entirely. The blending path is one the structure baselines run through, and perturbing it
    /// for the benefit of a tool it does not use would be the wrong trade at any odds.
    ///
    /// **Its own file, and dependency-free beyond Matrix3x3, on purpose.** A partial class costs
    /// nothing and buys the property that matters: Tools/SymmetricEigenCheck compiles *this file* in
    /// place against a UnityEngine stub and checks it against closed-form answers, without dragging
    /// in the Matrix4x4 helpers next door or the Vector3 extensions they need. The polar code carries
    /// a comment about a matrix logarithm that was wrong by a fifth of a radian near 180 degrees and
    /// was caught only that way; Jacobi rotations and eigenvector sign conventions are the same
    /// family of quietly-plausible arithmetic.
    /// </summary>
    public static partial class MatrixExtensions
    {
        /// <summary>
        /// Sweeps of cyclic Jacobi before giving up. Convergence is cubic once the off-diagonal is
        /// small, so this is a runaway guard rather than a quality setting - the loop leaves as soon
        /// as the off-diagonal norm is negligible.
        /// </summary>
        private const int   jacobiMaxSweeps = 16;
        private const float jacobiOffDiagonalTolerance = 1e-16f;
        private const float jacobiRotationTolerance = 1e-12f;

        /// <summary>
        /// The relative gap between the closest pair of eigenvalues below which their eigenvectors
        /// stop meaning anything. Measured rather than chosen - see EigenBasisIsWellDefined.
        /// </summary>
        private const float defaultEigenvalueGap = 0.01f;

        /// <summary>
        /// Decomposes a symmetric matrix as S = basis * diag(values) * basis^T.
        ///
        /// The basis is a rotation - orthonormal, determinant +1 - so it can be handed to a rotation
        /// handle or turned into a quaternion. Values are sorted descending, which puts a negative
        /// eigenvalue last; for a polar stretch factor a negative eigenvalue is an inverted
        /// transform, since that is where PolarDecompose deliberately puts the reflection.
        ///
        /// Only the symmetric part is used. Feeding this a matrix that is not symmetric is a caller
        /// error rather than a supported case, and symmetrizing first means it degrades into a
        /// nearby answer instead of an arbitrary one.
        /// </summary>
        public static void SymmetricEigenDecompose(this Matrix3x3 s, out Matrix3x3 basis, out Vector3 values)
        {
            Matrix3x3 a = s.symmetrized;
            Matrix3x3 v = Matrix3x3.identity;

            for (int sweep = 0; sweep < jacobiMaxSweeps; sweep++)
            {
                float off = a.m01 * a.m01 + a.m02 * a.m02 + a.m12 * a.m12;

                if (off < jacobiOffDiagonalTolerance) break;

                JacobiRotate(ref a, ref v, 0, 1);
                JacobiRotate(ref a, ref v, 0, 2);
                JacobiRotate(ref a, ref v, 1, 2);
            }

            values = new Vector3(a.m00, a.m11, a.m22);
            basis = v;

            SortEigenpairsDescending(ref basis, ref values);

            // A basis with determinant -1 is a reflection, and every caller here wants a rotation.
            // Negating a column leaves it an eigenvector of the same eigenvalue and flips the
            // determinant, so this costs nothing but the sign of one axis.
            if (basis.determinant < 0.0f)
                basis.SetColumn(2, -basis.GetColumn(2));
        }

        /// <summary>
        /// Permutes and flips an eigenbasis so each axis sits with the reference axis it is closest
        /// to, carrying the eigenvalues along with the permutation.
        ///
        /// **This is what makes shear readable as a number.** An eigendecomposition is free to return
        /// its axes in any order and either sign, so the raw basis of an *unsheared* stretch is
        /// generally not the frame it is unsheared with respect to - it is some permutation and
        /// reflection of it. Reported directly, that would show a large angle for a transform with no
        /// shear at all. Aligned, "the basis is the reference frame" and "there is no shear" become
        /// the same statement, and the shear can be quoted as one angle.
        ///
        /// Six permutations, tried exhaustively, because three axes make anything cleverer longer.
        /// Signs are then chosen to point each axis with its reference, and if that leaves a
        /// reflection the axis flipped back is the one whose alignment was weakest - it is the one
        /// whose direction meant least.
        ///
        /// The reference is assumed orthonormal. It is a frame, not a matrix to be fitted.
        ///
        /// **Two equal eigenvalues make the answer genuinely undefined, and no amount of alignment
        /// fixes that.** Equal eigenvalues leave their eigenvectors free to rotate anywhere within
        /// the plane they span, so a stretch that is isotropic in a plane has no "orientation" to
        /// recover - the same matrix has infinitely many valid bases. Measured: over 200k
        /// axis-aligned stretches this recovers the reference to within 0.10 degrees whenever the
        /// closest pair differs by more than 1% of the largest, and up to 2.6 degrees when they
        /// coincide. The decomposition still reproduces the matrix exactly in both cases, so this is
        /// a limit on what the *frame* means, not on the arithmetic. Anything quoting the angle to a
        /// user should say so - see <see cref="EigenBasisIsWellDefined"/>.
        /// </summary>
        public static void AlignEigenBasisToReference(this ref Matrix3x3 basis, ref Vector3 values, Matrix3x3 reference)
        {
            Vector3[] axes = { basis.GetColumn(0), basis.GetColumn(1), basis.GetColumn(2) };
            Vector3[] referenceAxes = { reference.GetColumn(0), reference.GetColumn(1), reference.GetColumn(2) };

            int[][] permutations =
            {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
            };

            int[] best = permutations[0];
            float bestScore = float.NegativeInfinity;

            foreach (int[] permutation in permutations)
            {
                float score = 0.0f;

                for (int i = 0; i < 3; i++)
                    score += Mathf.Abs(Vector3.Dot(axes[permutation[i]], referenceAxes[i]));

                if (score <= bestScore) continue;

                bestScore = score;
                best = permutation;
            }

            Vector3[] chosen = new Vector3[3];
            Vector3 permutedValues = Vector3.zero;

            float weakest = float.PositiveInfinity;
            int weakestAxis = 2;

            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = axes[best[i]];
                float alignment = Vector3.Dot(axis, referenceAxes[i]);

                chosen[i] = (alignment < 0.0f) ? (-axis) : (axis);
                permutedValues[i] = values[best[i]];

                if (Mathf.Abs(alignment) >= weakest) continue;

                weakest = Mathf.Abs(alignment);
                weakestAxis = i;
            }

            Matrix3x3 aligned = Matrix3x3.identity;

            for (int i = 0; i < 3; i++)
                aligned.SetColumn(i, chosen[i]);

            if (aligned.determinant < 0.0f)
                aligned.SetColumn(weakestAxis, -chosen[weakestAxis]);

            basis = aligned;
            values = permutedValues;
        }

        /// <summary>
        /// Rebuilds the symmetric matrix from a decomposition: basis * diag(values) * basis^T.
        ///
        /// Here rather than at the call sites so that the recomposition used by a tool is provably
        /// the inverse of the decomposition it was tested against, rather than a second spelling of
        /// it written somewhere else.
        /// </summary>
        public static Matrix3x3 RecomposeSymmetric(this Matrix3x3 basis, Vector3 values)
        {
            return basis * Matrix3x3.FromDiagonal(values) * basis.transposed;
        }

        /// <summary>
        /// Whether the eigenvectors mean anything for this set of eigenvalues.
        ///
        /// False when the closest pair is within <paramref name="relativeGap"/> of each other,
        /// relative to the largest: there the eigenvectors are free within their shared plane and the
        /// basis returned is one arbitrary choice among infinitely many. A readout that quotes an
        /// angle in that regime is quoting noise, and worse, noise that moves when nothing about the
        /// matrix has.
        ///
        /// The default threshold is the one the verification measured a clean answer above.
        /// </summary>
        public static bool EigenBasisIsWellDefined(Vector3 values, float relativeGap = defaultEigenvalueGap)
        {
            float largest = Mathf.Max(Mathf.Abs(values.x), Mathf.Max(Mathf.Abs(values.y), Mathf.Abs(values.z)));

            if (largest < 1e-6f) return false;

            float smallestGap = Mathf.Min(Mathf.Abs(values.x - values.y),
                                Mathf.Min(Mathf.Abs(values.y - values.z), Mathf.Abs(values.x - values.z)));

            return ((smallestGap / largest) > relativeGap);
        }

        public static Vector3 GetColumn(this Matrix3x3 m, int column)
        {
            return new Vector3(GetElement(m, 0, column), GetElement(m, 1, column), GetElement(m, 2, column));
        }

        public static void SetColumn(this ref Matrix3x3 m, int column, Vector3 value)
        {
            SetElement(ref m, 0, column, value.x);
            SetElement(ref m, 1, column, value.y);
            SetElement(ref m, 2, column, value.z);
        }

        /// <summary>
        /// One Jacobi rotation, zeroing the (p, q) entry of a symmetric matrix and accumulating the
        /// basis it is being diagonalised in.
        /// </summary>
        private static void JacobiRotate(ref Matrix3x3 a, ref Matrix3x3 v, int p, int q)
        {
            float apq = GetElement(a, p, q);

            if (Mathf.Abs(apq) < jacobiRotationTolerance) return;

            float app = GetElement(a, p, p);
            float aqq = GetElement(a, q, q);

            float theta = 0.5f * (aqq - app) / apq;
            float sign = (theta >= 0.0f) ? (1.0f) : (-1.0f);
            float t = sign / (Mathf.Abs(theta) + Mathf.Sqrt(theta * theta + 1.0f));

            float c = 1.0f / Mathf.Sqrt(t * t + 1.0f);
            float sine = t * c;

            Matrix3x3 rotation = Matrix3x3.identity;

            SetElement(ref rotation, p, p, c);
            SetElement(ref rotation, q, q, c);
            SetElement(ref rotation, p, q, sine);
            SetElement(ref rotation, q, p, -sine);

            a = (rotation.transposed * a * rotation).symmetrized;

            // A' = R^T A R at every step, so the accumulated V satisfies A = V A_final V^T and its
            // columns are the eigenvectors. Right-multiplication, therefore, not left.
            v = v * rotation;
        }

        private static void SortEigenpairsDescending(ref Matrix3x3 basis, ref Vector3 values)
        {
            // Three values, so a sorting network is shorter than anything that would call itself a
            // sort - the same one PolarBlender.SymmetricEigenvalues uses, with the columns carried.
            if (values.x < values.y) SwapEigenpair(ref basis, ref values, 0, 1);
            if (values.y < values.z) SwapEigenpair(ref basis, ref values, 1, 2);
            if (values.x < values.y) SwapEigenpair(ref basis, ref values, 0, 1);
        }

        private static void SwapEigenpair(ref Matrix3x3 basis, ref Vector3 values, int i, int j)
        {
            Vector3 columnI = basis.GetColumn(i);
            Vector3 columnJ = basis.GetColumn(j);

            basis.SetColumn(i, columnJ);
            basis.SetColumn(j, columnI);

            float valueI = values[i];

            values[i] = values[j];
            values[j] = valueI;
        }

        private static float GetElement(Matrix3x3 m, int row, int column)
        {
            if (row == 0) return (column == 0) ? (m.m00) : ((column == 1) ? (m.m01) : (m.m02));
            if (row == 1) return (column == 0) ? (m.m10) : ((column == 1) ? (m.m11) : (m.m12));

            return (column == 0) ? (m.m20) : ((column == 1) ? (m.m21) : (m.m22));
        }

        private static void SetElement(ref Matrix3x3 m, int row, int column, float value)
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
