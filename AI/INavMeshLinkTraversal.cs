namespace UC
{
    /// <summary>
    /// Traversal handler placed on the same GameObject as a <see cref="NavMeshAgent2d"/>.
    /// Only consulted for non-auto-traverse links: when the agent reaches the link it asks whether
    /// this handler can perform the crossing, and if so the handler is started. The agent's path
    /// following is held until someone calls <see cref="NavMeshAgent2d.TraversalComplete"/>.
    /// </summary>
    public interface INavMeshLinkTraversal
    {
        bool CanHandleTraversal(NavMeshAgent2d agent, NavMeshLink2d link);
        void BeginTraversal(NavMeshAgent2d agent, NavMeshLink2d link);
    }
}