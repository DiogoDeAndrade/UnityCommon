using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The navigation queries the graph construction needs from whatever produced the navmesh,
    /// bundled so they travel together instead of being threaded through as loose arguments.
    ///
    /// Keeps the solver independent of the navmesh implementation, the same way the delegates it
    /// wraps always did - nothing here names a concrete navmesh type.
    /// </summary>
    public sealed class EDNavQueries
    {
        public HasLOS               hasLOS;
        public TryGetSurfaceNormal  tryGetSurfaceNormal;
        public float                agentRadius = 0.5f;
        public Vector3              upVector = Vector3.up;
    }
}
#endif
