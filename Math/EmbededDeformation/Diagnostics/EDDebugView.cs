using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The nodes influencing one point, and by how much, independent of how that was worked out.
    ///
    /// Both factories drop entries that carry no influence - a zero weight, or the -1 node the field
    /// pads its cells with. So a count of zero means the point genuinely has nothing acting on it,
    /// which is a distinct answer from "there is no deformation here" and needs to be, since a vertex
    /// with no valid binding is one of the things these tools exist to catch.
    /// </summary>
    public readonly struct EDInfluences
    {
        public readonly int[]   nodeIndices;
        public readonly float[] weights;

        private EDInfluences(int[] nodeIndices, float[] weights)
        {
            this.nodeIndices = nodeIndices;
            this.weights = weights;
        }

        public int count => (nodeIndices != null) ? (nodeIndices.Length) : (0);

        /// <summary>
        /// For influences that were computed rather than read out of a cell - the trilinear blend,
        /// which has no backing array to filter.
        /// </summary>
        public static EDInfluences FromArrays(int[] nodeIndices, float[] weights)
        {
            if ((nodeIndices == null) || (weights == null)) return default;

            return new EDInfluences(nodeIndices, weights);
        }

        public static EDInfluences FromField(FullDeformationField.DeformationFieldWeights source)
        {
            if (source.weights == null) return default;

            int kept = 0;

            for (int i = 0; i < source.weights.Length; i++)
            {
                if ((source.weights[i] > 0.0f) && (source.nodeId[i] >= 0))
                    kept++;
            }

            var indices = new int[kept];
            var values = new float[kept];

            int at = 0;

            for (int i = 0; i < source.weights.Length; i++)
            {
                if ((source.weights[i] <= 0.0f) || (source.nodeId[i] < 0)) continue;

                indices[at] = source.nodeId[i];
                values[at] = source.weights[i];

                at++;
            }

            return new EDInfluences(indices, values);
        }

        public static EDInfluences FromBinding(EDVertexBinding source)
        {
            if (source.nodeIndices == null) return default;

            int kept = 0;

            for (int i = 0; i < source.nodeIndices.Length; i++)
            {
                if ((source.weights[i] > 0.0) && (source.nodeIndices[i] >= 0))
                    kept++;
            }

            var indices = new int[kept];
            var values = new float[kept];

            int at = 0;

            for (int i = 0; i < source.nodeIndices.Length; i++)
            {
                if ((source.weights[i] <= 0.0) || (source.nodeIndices[i] < 0)) continue;

                indices[at] = source.nodeIndices[i];
                values[at] = (float)source.weights[i];

                at++;
            }

            return new EDInfluences(indices, values);
        }
    }

    /// <summary>
    /// A read-only window onto a solved deformation, for the scene-view tools that visualise one.
    ///
    /// It exists so those tools do not need the component that happens to own the deformation. They
    /// were reaching through a MonoBehaviour that had grown seven public methods for no other
    /// reason, none of which had anything to do with producing a deformation - only with reporting
    /// one. Everything here is expressible in this assembly's own terms, so this is where it belongs.
    ///
    /// A struct, and a cheap one: these are called from OnDrawGizmos, which runs on every repaint,
    /// and an object allocated per frame to answer a question about another object is a poor trade.
    /// Every accessor tolerates an unbuilt or absent deformation and answers false rather than
    /// throwing, because a gizmo that draws nothing is the correct response to nothing to draw.
    /// </summary>
    public readonly struct EDDebugView
    {
        private readonly EmbededDeformation deformation;

        public EDDebugView(EmbededDeformation deformation)
        {
            this.deformation = deformation;
        }

        public bool isValid => (deformation != null);

        /// <summary>
        /// The deformed frame of a graph node - where it ended up and how it is oriented.
        /// </summary>
        public bool TryGetNodeFrame(int nodeIndex, out FullDeformationField.Frame frame)
        {
            frame = default;

            if (deformation == null) return false;

            return deformation.TryGetDebugNodeFrame(nodeIndex, out frame);
        }

        public Vector3 GetNodePosition(int nodeIndex)
        {
            return (deformation != null) ? (deformation.GetDebugNodePosition(nodeIndex)) : (Vector3.zero);
        }

        /// <summary>
        /// The frame a terminal constraint is asking a node to reach, and the width scale it wants.
        /// Comparing this against the node's actual frame is what shows whether a connector is
        /// being satisfied.
        /// </summary>
        public bool TryGetTerminalTargetFrame(int nodeIndex, out FullDeformationField.Frame frame, out float targetScale)
        {
            frame = default;
            targetScale = 1.0f;

            if (deformation == null) return false;

            return deformation.TryGetTerminalTargetFrame(nodeIndex, out frame, out targetScale);
        }

        /// <summary>
        /// Which mechanism carries points here: the volumetric field, or the node bindings.
        ///
        /// Worth reporting rather than hiding. The two are different deformations, not two
        /// approximations of one, so a tool that showed a field answer for a binding-mode solve
        /// would be showing something the run never computed.
        /// </summary>
        public bool usesDeformationField => (deformation != null) && (deformation.UsesDeformationFieldForDebug);

        /// <summary>
        /// Which nodes influence a point in space, and by how much, whichever mechanism is active.
        ///
        /// Field mode reads the field's cell weights; binding mode binds the point on demand with the
        /// settings the graph was built under. Both answer the same question, which is why callers
        /// get one method - before this existed, every scene-view tool worked only on the two
        /// structure configurations and silently drew nothing for the other five.
        /// </summary>
        public bool TryGetInfluences(Vector3 position, out EDInfluences influences) => TryGetInfluences(position, false, out influences);

        /// <summary>
        /// As TryGetInfluences, but able to report the trilinear blend rather than the containing
        /// cell's own weights.
        ///
        /// Worth having both, and worth a caller stating which it wants. The cell weights are what
        /// the field stores and are a step function across cell boundaries; the trilinear blend is
        /// what deforms geometry. A tool that draws a trilinear deformation while printing cell
        /// weights beside it is showing two different answers as though they were one, and the
        /// disagreement looks like a discontinuity in the field rather than in the readout.
        ///
        /// Ignored in binding mode, which has no cells.
        /// </summary>
        public bool TryGetInfluences(Vector3 position, bool trilinear, out EDInfluences influences)
        {
            influences = default;

            if (deformation == null) return false;

            FullDeformationField field = GetField();

            if (field != null)
            {
                if ((trilinear) && (field.TryGetTrilinearInfluences(position, out int[] nodeIds, out float[] blended)))
                {
                    influences = EDInfluences.FromArrays(nodeIds, blended);

                    return true;
                }

                var weights = field.GetWeights(position);

                influences = EDInfluences.FromField(weights);

                return true;
            }

            var binding = deformation.BindPoint(position);

            if ((binding.nodeIndices == null) || (binding.nodeIndices.Length == 0))
                return false;

            influences = EDInfluences.FromBinding(binding);

            return true;
        }

        /// <summary>
        /// Which nodes influence a point in space, and by how much, through the field specifically.
        /// Prefer TryGetInfluences unless the field is the subject.
        /// </summary>
        public bool TryGetFieldWeights(Vector3 position, out FullDeformationField.DeformationFieldWeights weights)
        {
            weights = new FullDeformationField.DeformationFieldWeights();

            FullDeformationField field = GetField();

            if (field == null) return false;

            weights = field.GetWeights(position);

            return true;
        }

        /// <summary>
        /// The geodesic distance the field itself stored for a node at a position, and the cell it
        /// came from.
        ///
        /// This is the distance the weights were computed from. It is emphatically not the straight
        /// line from the point to the node: the field propagates through the volume, so a node on the
        /// far side of a wall is near in a straight line and far here, which is the entire reason the
        /// field exists. A readout showing the Euclidean distance beside a field weight is showing
        /// two unrelated numbers.
        ///
        /// Per cell, because distance is a per-cell quantity - there is no trilinear version of it.
        /// The cell is returned with it so a caller can say which one it read.
        /// </summary>
        public bool TryGetFieldDistance(Vector3 position, int nodeIndex, out float distance, out Vector3Int cell)
        {
            distance = float.MaxValue;
            cell = new Vector3Int(-1, -1, -1);

            FullDeformationField field = GetField();

            if (field == null) return false;

            cell = field.GetVoxelCoordinate(position);

            var weights = field.GetWeights(position);

            if (weights.nodeId == null) return false;

            for (int i = 0; i < weights.nodeId.Length; i++)
            {
                if (weights.nodeId[i] != nodeIndex) continue;

                distance = weights.distances[i];

                return (distance < float.MaxValue);
            }

            return false;
        }

        public bool TryGetFieldCell(Vector3 position, out Vector3Int cell)
        {
            cell = new Vector3Int(-1, -1, -1);

            FullDeformationField field = GetField();

            if (field == null) return false;

            cell = field.GetVoxelCoordinate(position);

            return true;
        }

        /// <summary>
        /// Where the deformation carries a point.
        ///
        /// Trilinear sampling is what the field deforms geometry with; the nearest-cell variant is
        /// offered because seeing the difference between them is the point of one of these tools. It
        /// means nothing in binding mode, where there are no cells to sample, and is ignored there.
        /// </summary>
        public bool TryGetDeformedPosition(Vector3 position, out Vector3 deformedPosition, bool trilinear = false)
        {
            deformedPosition = position;

            if (deformation == null) return false;

            FullDeformationField field = GetField();

            if (field != null)
            {
                deformedPosition = (trilinear) ? (field.DeformPositionFromNodeFramesTrilinear(position, GetNodeFrame))
                                               : (field.DeformPositionFromNodeFrames(position, GetNodeFrame));

                return true;
            }

            // The same blend the mesh output uses, so what this reports and what the geometry does
            // cannot disagree.
            if (!deformation.TryGetDebugBindingMatrix(position, out Matrix4x4 matrix))
                return false;

            deformedPosition = matrix.MultiplyPoint3x4(position);

            return true;
        }

        /// <summary>
        /// Where one node alone would carry a point, ignoring every other influence. Useful for
        /// seeing which node is responsible for a movement that looks wrong.
        /// </summary>
        public bool TryGetSingleInfluenceDeformedPosition(Vector3 position, int nodeIndex, out Vector3 deformedPosition)
        {
            deformedPosition = position;

            if (deformation == null) return false;

            FullDeformationField field = GetField();

            if (field != null)
            {
                deformedPosition = field.DeformPositionFromSingleNodeFrame(position, nodeIndex, GetNodeFrame);

                return true;
            }

            deformedPosition = deformation.DeformDebugPointBySingleNode(position, nodeIndex);

            return true;
        }

        /// <summary>
        /// The field, but only when this deformation actually maps points through it.
        ///
        /// Guarded on the graph source rather than on the reference. FullDeformationField is
        /// [Serializable] on a [SerializeField] member, so in navmesh mode it comes back from
        /// serialization as a live but empty object - a plain null check hands every navmesh graph an
        /// empty field and it answers, confidently, with nothing in it. Routing every field access
        /// here means no caller can make that mistake individually.
        /// </summary>
        private FullDeformationField GetField()
        {
            if (deformation == null) return null;

            if (!deformation.UsesDeformationFieldForDebug) return null;

            return deformation.GetDeformationField();
        }

        private FullDeformationField.Frame GetNodeFrame(int nodeIndex)
        {
            return deformation.GetDebugNodeFrame(nodeIndex);
        }
    }
}
#endif
