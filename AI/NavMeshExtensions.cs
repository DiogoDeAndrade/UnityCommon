using UC;
using UnityEngine;
using UnityEngine.AI;

public static class NavMeshExtensions
{
    public static bool HasReachedDestination(this NavMeshAgent agent)
    {
        if ((!agent.enabled) || (!agent.isOnNavMesh))
            return false;

        // Still calculating the path.
        if (agent.pathPending)
            return false;

        // Not close enough yet.
        if (agent.remainingDistance > agent.stoppingDistance)
            return false;

        // If it still has velocity or path, let it finish.
        if ((agent.hasPath) && (agent.velocity.sqrMagnitude > 0.01f))
            return false;

        return true;
    }

    public static bool HasPath(this NavMeshAgent agent, Vector3 sourcePos, Vector3 targetPos)
    {
        const float xzTolerance = 0.05f;
        const float sampleDistance = 2.0f;

        if (agent == null || !agent.enabled)
            return false;

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        if (!NavMesh.SamplePosition(sourcePos, out NavMeshHit startHit, sampleDistance, filter))
        {
            return false;
        }

        Vector2 sourceXZ = sourcePos.xz();
        Vector2 sampledSourceXZ = startHit.position.xz();

        if (Vector2.Distance(sourceXZ, sampledSourceXZ) > xzTolerance)
        {
            return false;
        }

        if (!NavMesh.SamplePosition(targetPos, out NavMeshHit targetHit, sampleDistance, filter))
        {
            return false;
        }

        Vector2 targetXZ = targetPos.xz();
        Vector2 sampledXZ = targetHit.position.xz();

        if (Vector2.Distance(targetXZ, sampledXZ) > xzTolerance)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();

        if (!NavMesh.CalculatePath(startHit.position, targetHit.position, filter, path))
        {
            return false;
        }

        return (path.status == NavMeshPathStatus.PathComplete);
    }
}
