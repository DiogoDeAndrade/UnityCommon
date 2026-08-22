using System;
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
        /// A read-only view of the state as it stands, for a diagnostic that wants to measure the
        /// current answer through the same code a solve measures a candidate through.
        /// </summary>
        internal EDStateView currentStateView => new EDStateView(currentState);
    }
}
#endif
