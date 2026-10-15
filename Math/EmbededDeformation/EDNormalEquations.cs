using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The normal equations of a Jacobian, H = Jᵀ·J and g = Jᵀ·f, two ways.
    ///
    /// <see cref="Dense"/> is what the damped loops always did: a transpose and two Math.NET
    /// products, which cost rows times the square of the columns whatever the Jacobian holds. On
    /// Corridor6 (958 rows by 516 columns, 2026-10-15) that was 255 million multiply-adds an
    /// iteration and half the solve, under no row of the report until that day.
    ///
    /// <see cref="Sparse"/> forms the same sums over the nonzero entries of each row and nothing
    /// else. A row of this Jacobian touches one or two nodes' twelve columns - a graph-space term's
    /// row - or the handful of nodes a finite-difference row reaches, and only the global field
    /// term's single row is wide; so the work is the sum over the rows of the square of each row's
    /// nonzero count, hundreds of times less than the dense product on a piece of any size.
    ///
    /// **The arithmetic is the dense product's.** For every entry of H the dense product adds the
    /// product of two columns over all rows; where a row touches neither column that product is an
    /// exact zero, and adding an exact zero changes nothing in floating point, so the set of rounded
    /// additions here is the dense product's set. What can differ is their order: this accumulates
    /// each entry over the rows in increasing row order, the order a plain inner loop uses, and
    /// whether Math.NET's dense product does the same is what Tools/NormalEquationsCheck measures.
    /// The upper triangle is accumulated and mirrored: the dense product computes both halves from
    /// the same operands in the same order, and a·b is the same float as b·a, so the mirror is exact.
    ///
    /// Kept free of Unity types so the harness can compile this file in place.
    /// </summary>
    public static class EDNormalEquations
    {
        /// <summary>Forms H and g the way the option says - the dense product by default.</summary>
        public static void Build(Matrix<double> J, Vector<double> f, bool sparse, out Matrix<double> H, out Vector<double> g)
        {
            if (sparse) Sparse(J, f, out H, out g);
            else Dense(J, f, out H, out g);
        }

        /// <summary>The transpose and two products, exactly as the loops wrote them before the option existed.</summary>
        public static void Dense(Matrix<double> J, Vector<double> f, out Matrix<double> H, out Vector<double> g)
        {
            var JT = J.Transpose();

            H = JT * J;
            g = JT * f;
        }

        /// <summary>The same sums over the nonzero entries of each row only.</summary>
        public static void Sparse(Matrix<double> J, Vector<double> f, out Matrix<double> H, out Vector<double> g)
        {
            int rows = J.RowCount;
            int cols = J.ColumnCount;

            // The dense storage itself where the matrix is dense column-major, which the Jacobian
            // always is; a copy for anything else.
            double[] jv = J.AsColumnMajorArray() ?? J.ToColumnMajorArray();
            double[] fv = f.AsArray() ?? f.ToArray();

            // Each row's nonzero entries, gathered by two sequential passes over the column-major
            // storage - a column at a time, so a row's entries land in increasing column order.
            int[] count = new int[rows];

            for (int c = 0; c < cols; c++)
            {
                int column = c * rows;

                for (int r = 0; r < rows; r++)
                {
                    if (jv[column + r] != 0.0) count[r]++;
                }
            }

            int[] start = new int[rows + 1];

            for (int r = 0; r < rows; r++) start[r + 1] = start[r] + count[r];

            int[] colOf = new int[start[rows]];
            double[] valOf = new double[start[rows]];
            int[] next = new int[rows];

            for (int r = 0; r < rows; r++) next[r] = start[r];

            for (int c = 0; c < cols; c++)
            {
                int column = c * rows;

                for (int r = 0; r < rows; r++)
                {
                    double v = jv[column + r];

                    if (v == 0.0) continue;

                    int at = next[r]++;

                    colOf[at] = c;
                    valOf[at] = v;
                }
            }

            // H's upper triangle (i <= j) and g, every entry accumulated over the rows in increasing
            // row order. hv is column-major: hv[j * cols + i] is H[i, j].
            var h = new DenseMatrix(cols, cols);
            double[] hv = h.AsColumnMajorArray();
            double[] gv = new double[cols];

            for (int r = 0; r < rows; r++)
            {
                double fr = fv[r];
                int end = start[r + 1];

                for (int p = start[r]; p < end; p++)
                {
                    int i = colOf[p];
                    double vi = valOf[p];

                    gv[i] += vi * fr;

                    for (int q = p; q < end; q++)
                    {
                        hv[colOf[q] * cols + i] += vi * valOf[q];
                    }
                }
            }

            for (int j = 0; j < cols; j++)
            {
                for (int i = 0; i < j; i++) hv[i * cols + j] = hv[j * cols + i];
            }

            H = h;
            g = new DenseVector(gv);
        }
    }
}
#endif
