using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
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
        /// Which nodes influence a point in space, and by how much.
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
        /// Where the field carries a point. Trilinear sampling is what the field deforms geometry
        /// with; the nearest-cell variant is offered because seeing the difference between them is
        /// the point of one of these tools.
        /// </summary>
        public bool TryGetDeformedPosition(Vector3 position, out Vector3 deformedPosition, bool trilinear = false)
        {
            deformedPosition = position;

            FullDeformationField field = GetField();

            if (field == null) return false;

            deformedPosition = (trilinear) ? (field.DeformPositionFromNodeFramesTrilinear(position, GetNodeFrame))
                                           : (field.DeformPositionFromNodeFrames(position, GetNodeFrame));

            return true;
        }

        /// <summary>
        /// Where one node alone would carry a point, ignoring every other influence. Useful for
        /// seeing which node is responsible for a movement that looks wrong.
        /// </summary>
        public bool TryGetSingleInfluenceDeformedPosition(Vector3 position, int nodeIndex, out Vector3 deformedPosition)
        {
            deformedPosition = position;

            FullDeformationField field = GetField();

            if (field == null) return false;

            deformedPosition = field.DeformPositionFromSingleNodeFrame(position, nodeIndex, GetNodeFrame);

            return true;
        }

        private FullDeformationField GetField()
        {
            return (deformation != null) ? (deformation.GetDeformationField()) : (null);
        }

        private FullDeformationField.Frame GetNodeFrame(int nodeIndex)
        {
            return deformation.GetDebugNodeFrame(nodeIndex);
        }
    }
}
#endif
