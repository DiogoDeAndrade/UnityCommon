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
    ///
    /// Callbacks only. The agent radius and the up vector used to ride along here as well, but they
    /// are plain data that outlives the build and that more than the builder reads, so they are set
    /// on the deformation through SetAgentParameters and queried from there. What is left is what
    /// genuinely cannot be data: two live delegates into the scene.
    /// </summary>
    public sealed class EDNavQueries
    {
        public HasLOS               hasLOS;
        public TryGetSurfaceNormal  tryGetSurfaceNormal;
    }
}
#endif
