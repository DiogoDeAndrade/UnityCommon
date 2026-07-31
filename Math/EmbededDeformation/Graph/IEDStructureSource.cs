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
    /// </summary>
    public interface IEDStructureSource
    {
        int segmentCount { get; }

        void GetSegment(int index, out Vector3 p1, out Vector3 p2);
    }
}
#endif
