using System;
using System.Threading.Tasks;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The geometry the output is generated from, held at rest so an energy can measure what the
    /// deformation does to it.
    ///
    /// Every other quantity the energies read is a property of the graph or the navmesh. This is the
    /// first that is a property of the *output*: the mesh a run produces at the end, which until now
    /// nothing in the objective ever saw. It is supplied by the owner, already subdivided exactly as
    /// the output will be, because the subdivision is an output setting on the experiment and the
    /// solver has no business knowing what those are - it only has to measure the same triangles the
    /// output is made of.
    ///
    /// Deliberately not serialized, for the reason the field is not: it is derived data, it is large
    /// - a few hundred thousand vertices on the corridor piece - and a copy surviving a domain reload
    /// could only be stale. The owner re-supplies it before anything reads it, and a term finding
    /// none says so rather than contributing rows of zeros quietly.
    /// </summary>
    public partial class EmbededDeformation
    {
        [NonSerialized]
        private Vector3[]   outputMeshRestVertices;
        [NonSerialized]
        private int[]       outputMeshTriangleIndices;
        [NonSerialized]
        private string      outputMeshBuiltFromDescriptor;

        /// <summary>
        /// Hands over the mesh the output will be generated from: rest positions in world space and
        /// a plain triangle list over them, every submesh concatenated.
        ///
        /// <paramref name="builtFrom"/> names the settings it was produced under, so the owner can
        /// ask whether what is held still matches what it would build now - the same arrangement the
        /// field's descriptors make - and so the golden dump can record it. Compared as a string
        /// rather than field by field so that a setting added later is covered without anyone
        /// remembering to extend the comparison.
        /// </summary>
        public void SetOutputMesh(Vector3[] restWorldVertices, int[] triangles, string builtFrom)
        {
            outputMeshRestVertices = restWorldVertices;
            outputMeshTriangleIndices = triangles;
            outputMeshBuiltFromDescriptor = builtFrom;
        }

        public void ClearOutputMesh()
        {
            outputMeshRestVertices = null;
            outputMeshTriangleIndices = null;
            outputMeshBuiltFromDescriptor = null;
        }

        /// <summary>
        /// Whether there is an output mesh to measure. Tests the data rather than the references,
        /// though here the references are honest - these are [NonSerialized] arrays, not a
        /// [Serializable] class Unity would resurrect empty.
        /// </summary>
        public bool hasOutputMesh => (outputMeshRestVertices != null) &&
                                     (outputMeshTriangleIndices != null) &&
                                     (outputMeshRestVertices.Length > 0) &&
                                     (outputMeshTriangleIndices.Length >= 3);

        public bool OutputMeshMatches(string builtFrom) => (hasOutputMesh) && (outputMeshBuiltFromDescriptor == builtFrom);

        public int outputMeshVertexCount => (hasOutputMesh) ? (outputMeshRestVertices.Length) : (0);
        public int outputMeshTriangleCount => (hasOutputMesh) ? (outputMeshTriangleIndices.Length / 3) : (0);
        public string outputMeshBuiltFrom => outputMeshBuiltFromDescriptor ?? "none";

        // Internal for the output-mesh term. Handed out as the arrays themselves rather than copied:
        // the term reads them once per Reset and never writes.
        internal Vector3[] outputMeshVertices => outputMeshRestVertices;
        internal int[] outputMeshTriangles => outputMeshTriangleIndices;

        /// <summary>
        /// Whether output geometry is carried through the field here, which is the same decision
        /// CreateDeformer makes. A term that measures the output has to deform its vertices the way
        /// the output is deformed, and this is how it finds out which way that is.
        /// </summary>
        internal bool usesDeformationFieldForOutput => usesDeformationField;

        /// <summary>
        /// Where the output would put a rest-space point under the given state - through the blender
        /// when there is one, through the point's binding otherwise.
        ///
        /// **This is DeformMesh's per-vertex arithmetic and has to stay that.** DeformMesh asks the
        /// deformer for the matrix acting at the point and applies it, so that is what happens here:
        /// blender.TryGetMatrix then MultiplyPoint3x4, or TryBlendBindingAffine then MultiplyPoint3x4.
        /// It is *not* DeformClearancePoint, which goes through blender.DeformPosition - under the
        /// linear-affine blend those two are the same number in the reals and different numbers in
        /// float, and a term measuring the output must measure the output's number. A point nothing
        /// influences stays where it is, as it does in the output.
        ///
        /// The state is what a perturbed Jacobian column reads through on the binding path; on the
        /// field path the perturbation reaches the blender as a node override, which is why the
        /// blender is passed in rather than made here.
        /// </summary>
        internal Vector3 DeformOutputPoint(Vector3 restWorldPosition, in EDVertexBinding binding, EDStateView state, FullDeformationField.TransformBlender blender)
        {
            Matrix4x4 matrix;

            bool hasDeformation = (blender != null) ? (blender.TryGetMatrix(restWorldPosition, trilinear: true, out matrix))
                                                    : (TryBlendBindingAffine(binding, state, out matrix));

            return (hasDeformation) ? (matrix.MultiplyPoint3x4(restWorldPosition)) : (restWorldPosition);
        }

        /// <summary>
        /// The rotation of the blend in force at a rest-space point, in the *corotational*
        /// convention, read through the same two paths DeformOutputPoint deforms through.
        ///
        /// This is the reference an orientation measure projects against. A rest normal frozen in
        /// world space reads legitimate local rotation as damage - past ~72 degrees a healthy
        /// triangle scores squashed, past 90 inverted - so the reference has to turn with the map.
        /// It must not turn by the full affine, which follows the map through a fold and would
        /// detect nothing; and it must not turn by the blender's reflected-polar convention either,
        /// which was this method's first draft and is wrong the other way.
        /// PolarBlender.PolarDecompose keeps R proper on a folded map by negating *both* factors,
        /// and that buries a half-turn in R: for a map folded through along one axis it returns
        /// the honest rotation composed with pi about the folded axis, so a reference carried by
        /// it turns *with* the fold, and an in-plane fold - the one kind that is a genuine surface
        /// inversion - reads healthy. What a reference needs is the corotational convention, the
        /// one the invertible-FEM literature factors a folded deformation gradient with: the
        /// reflection confined to the *single* folded axis, the rotation clean of it. It is
        /// recovered from the blender's answer by turning back: the negated stretch is negative
        /// definite, its leading eigenvector is the least-stretched axis - the one that folded -
        /// and a half-turn about that axis composed onto the returned rotation undoes exactly the
        /// half-turn the wholesale negation introduced. The eigen is the harness-verified
        /// SymmetricEigenDecompose, and it runs only when the blend's determinant is negative, so
        /// a healthy neighbourhood pays the polar and nothing else.
        ///
        /// False when nothing influences the point, with the identity in the out - such a point is
        /// left at rest by the output, and the untransported rest frame is the honest reference
        /// there.
        /// </summary>
        internal bool TryGetOutputRotation(Vector3 restWorldPosition, in EDVertexBinding binding, EDStateView state, FullDeformationField.TransformBlender blender, out Matrix3x3 rotation)
        {
            Matrix4x4 matrix;

            bool hasDeformation = (blender != null) ? (blender.TryGetMatrix(restWorldPosition, trilinear: true, out matrix))
                                                    : (TryBlendBindingAffine(binding, state, out matrix));

            if (!hasDeformation)
            {
                rotation = Matrix3x3.identity;

                return false;
            }

            Matrix3x3 linear = new Matrix3x3(matrix);

            FullDeformationField.PolarBlender.PolarDecompose(linear, out rotation, out Matrix3x3 stretch);

            if (linear.determinant < 0.0f)
            {
                // The blender negated both factors - see the summary. The guard on the leading
                // eigenvalue keeps the singular fallback out: there PolarDecompose returns the
                // identity rotation with the whole matrix as stretch, nothing was negated, and
                // there is no half-turn to undo.
                stretch.SymmetricEigenDecompose(out Matrix3x3 stretchBasis, out Vector3 stretchValues);

                if (stretchValues.x < 0.0f)
                    rotation = rotation * Matrix3x3.HalfTurnAbout(stretchBasis.GetColumn(0));
            }

            return true;
        }

        /// <summary>
        /// Transports rest normals by the rotation of the blend in force at each position, under
        /// the current state - the reference the quality energy measures against, offered as a
        /// batch so the inverted-triangle count on the generated output reads against the same
        /// reference and the two numbers stay comparable. See TryGetOutputRotation for which
        /// rotation that is and why the convention is load-bearing.
        ///
        /// The positions are rest *world* positions, the space everything here is indexed by. A
        /// position nothing influences keeps its normal untransported, which matches the output
        /// leaving such geometry at rest. False when there is no graph or state to read a rotation
        /// from, in which case nothing is written and the caller should say its count is against
        /// untransported normals.
        /// </summary>
        public bool TryTransportRestNormals(Vector3[] restWorldPositions, Vector3[] restNormals, Vector3[] transportedNormals)
        {
            if ((restWorldPositions == null) || (restNormals == null) || (transportedNormals == null)) return false;
            if ((restNormals.Length < restWorldPositions.Length) || (transportedNormals.Length < restWorldPositions.Length)) return false;
            if ((nodes == null) || (nodes.Count == 0) || (currentState == null)) return false;

            EDStateView state = currentStateView;

            FullDeformationField.TransformBlender blender = null;
            EDVertexBinding[] bindings = null;

            if (usesDeformationField)
            {
                blender = CreateFieldBlender(state);

                if (blender == null) return false;
            }
            else
            {
                // Bound serially: BindPoint has only ever been driven from serial loops, and this
                // is a per-update diagnostic path. The parallel loop below makes only the calls the
                // residual terms already make from workers.
                bindings = new EDVertexBinding[restWorldPositions.Length];

                for (int i = 0; i < restWorldPositions.Length; i++)
                    bindings[i] = BindPoint(restWorldPositions[i]);
            }

            Parallel.For(0, restWorldPositions.Length, EDDiagnostics.parallelOptions, i =>
            {
                EDVertexBinding binding = (bindings != null) ? (bindings[i]) : (default);

                transportedNormals[i] = (TryGetOutputRotation(restWorldPositions[i], in binding, state, blender, out Matrix3x3 rotation))
                                      ? (rotation * restNormals[i])
                                      : (restNormals[i]);
            });

            return true;
        }

        /// <summary>
        /// A read-only view of the state as it stands, for a diagnostic that wants to measure the
        /// current answer through the same code a solve measures a candidate through.
        /// </summary>
        internal EDStateView currentStateView => new EDStateView(currentState);
    }
}
#endif
