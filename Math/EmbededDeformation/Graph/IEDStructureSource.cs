using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Supplies the skeleton the deformation structure is built from, as a flat ordered list of
    /// segments.
    ///
    /// This exists so the solver does not have to know about Graph2Structure, which is a
    /// MonoBehaviour living in the consuming project rather than in this library. It follows the
    /// same principle as the HasLOS and TryGetSurfaceNormal delegates, but is an interface because
    /// it needs a count alongside its two outputs.
    ///
    /// Segment order is part of the numerical contract: the index is the identity used by
    /// `structure[i]`, by the clearance cache, and by every gizmo, so an implementation must
    /// enumerate deterministically and in a stable order.
    ///
    /// **Segment direction is part of the contract too, and it carries the skeleton's tree.** `p1`
    /// is the parent and `p2` the child. That single rule is enough to recover the whole rooted tree
    /// on the far side - the root is the node that is never anybody's `p2`, the leaves are the nodes
    /// that are never anybody's `p1` - which is what `BuildStructureTree` does, and it is why none of
    /// this needs the tree itself to be handed across.
    ///
    /// It was true before it was written down: the only implementation walks
    /// `Tree&lt;N&gt;.GetSegmentNodes`, which emits `(parent, child)` by construction. Stating it here is
    /// what turns that from an accident into something a second implementation has to honour.
    /// </summary>
    public interface IEDStructureSource
    {
        int segmentCount { get; }

        /// <summary>One skeleton segment. <paramref name="p1"/> is the parent end and <paramref name="p2"/> the child end - see the interface remarks.</summary>
        void GetSegment(int index, out Vector3 p1, out Vector3 p2);
    }
}
#endif
