using System;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The deformation-quality measure sampled on the output mesh: the triangles of the geometry
    /// the output is generated from, with their rest areas. See EDDeformationQualityTerm for the
    /// measure itself; this only says where the samples come from.
    ///
    /// The mesh is supplied by the owner through EmbededDeformation.SetOutputMesh, already
    /// subdivided the way the output will be, so the triangles scored are the triangles produced.
    /// The simplifier is not applied: it runs on the deformed output, after the fact, and there is
    /// no rest mesh to measure its result against. A vertex nothing influences is still measured,
    /// because the output really does leave it at rest - measuring it is measuring the output.
    ///
    /// Two forms, two entries in the energy model: one row over the whole mesh, or one row per
    /// deformation node over the triangles whose centroid that node dominates.
    /// </summary>
    [Serializable]
    public abstract class EDOutputMeshQualityTerm : EDDeformationQualityTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new MeshQualityInstance(this, deformation, normalizeWeights);

        public sealed class MeshQualityInstance : QualityInstance
        {
            public MeshQualityInstance(EDOutputMeshQualityTerm term, EmbededDeformation deformation, bool normalizeWeights)
                : base(term, deformation, normalizeWeights)
            {
            }

            protected override bool TryBuildSamples(out Vector3[] vertices, out int[] indices, out int arity, out string reason)
            {
                vertices = null;
                indices = null;
                arity = 3;
                reason = null;

                if (!deformation.hasOutputMesh)
                {
                    reason = "the deformation carries no output mesh to measure - the owner supplies it before a solve; if this is Run Iteration after a domain reload, press Update Deformation once";

                    return false;
                }

                vertices = deformation.outputMeshVertices;
                indices = deformation.outputMeshTriangles;

                return true;
            }

            protected override bool samplesThroughField => deformation.usesDeformationFieldForOutput;

            protected override bool measureUninfluenced => true;

            public override string sampleLabel => "triangles";

            protected override string describeNote => "(the inverted count on the generated output should agree when the simplifier is off)";
        }
#endif
    }

    /// <summary>
    /// One row over the whole output mesh.
    /// </summary>
    [Serializable]
    [PolymorphicName("Global Output Mesh Quality")]
    public class EDGlobalOutputMeshQualityTerm : EDOutputMeshQualityTerm
    {
        public override string name => "globalOutputMeshQuality";

        protected override bool perNode => false;
    }

    /// <summary>
    /// One row per deformation node, over the triangles whose centroid that node carries the most
    /// weight at. Same energy as the global form, more Gauss-Newton rank - see the base.
    /// </summary>
    [Serializable]
    [PolymorphicName("Per-Node Output Mesh Quality")]
    public class EDPerNodeOutputMeshQualityTerm : EDOutputMeshQualityTerm
    {
        public override string name => "perNodeOutputMeshQuality";

        protected override bool perNode => true;
    }
}
#endif
