
namespace UC
{
    /// <summary>
    /// Predicate component placed on the same GameObject as a <see cref="NavMeshLink2d"/>.
    /// All conditions on a link must return true for the link to be usable by the pathfinder
    /// (a link with no conditions is always open). Evaluated during A* neighbour expansion,
    /// so a "closed" link is simply never offered to the search.
    /// </summary>
    public interface INavMeshLinkCondition
    {
        bool NavCanPass(NavMeshAgent2d agent, NavMeshLink2d link);
    }
}