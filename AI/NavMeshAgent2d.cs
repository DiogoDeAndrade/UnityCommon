using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{
    public class NavMeshAgent2d : MonoBehaviour
    {
        private enum PathFollowMode { FollowDirect, FollowPursuit };

        public delegate void OnComplete(NavMeshAgent2d agent);
        public event OnComplete onComplete;
        public delegate bool OnStopped(NavMeshAgent2d agent);
        public OnStopped onStopped;

        [SerializeField] 
        private AgentType       agentType;
        [SerializeField] 
        private PathFollowMode  followMode = PathFollowMode.FollowPursuit;
        [SerializeField] 
        private float           speed;
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           angularSpeed;
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           acceleration;
        [SerializeField, ShowIf(nameof(isFollowDirect))]
        private bool            smoothPath;
        [SerializeField, Range(1, 6), ShowIf(nameof(isSmoothPath))]
        private int             segmentsPerWaypoint= 3; // Fraction of segment length for tangent control points
        [SerializeField, Range(0.0f, 0.5f), ShowIf(nameof(isSmoothPath))]
        private float           tangentScale = 0.3f; // Fraction of segment length for tangent control points
        [SerializeField, ShowIf(nameof(isFollowDirect))]
        private Vector2         followOffset;
        [SerializeField] 
        private float           stoppingDistance = 2.0f;


        bool isFollowDirect => followMode == PathFollowMode.FollowDirect;
        bool isFollowPursuit => followMode == PathFollowMode.FollowPursuit;
        bool isSmoothPath => isFollowDirect && smoothPath;


        protected NavMesh2d     navMesh;
        protected int           regionId = -1;
        protected bool          _isMoving = false;
        protected Vector2       targetPosition;
        protected Vector2[]     lastDeltas;
        protected int           lastDeltasSampleCount = 0;
        protected Vector2       prevPos;
        protected Rigidbody2D   rb;

        private List<NavMesh2d.PathNode>    path;
        private int                         currentPathIndex;
        private Vector2                     velocity;
        private Vector2                     desiredDir;

        public void SetMaxSpeed(float maxSpeed)
        {
            this.speed = maxSpeed;
        }

        public bool isMoving => _isMoving;

        void Start()
        {
            navMesh = NavMesh2d.Get(agentType);
            regionId = navMesh.GetRegion(transform.position);

            lastDeltas = new Vector2[16];
            prevPos = transform.position;
            rb = GetComponent<Rigidbody2D>();

            path = new List<NavMesh2d.PathNode>();
        }

        void Update()
        {
            if ((rb == null) || (rb.bodyType != RigidbodyType2D.Dynamic))
            {
                TickAgent(Time.deltaTime, false);
            }

            if (_isMoving)
            {
                var lastDelta = transform.position.xy() - prevPos;
                lastDeltas[lastDeltasSampleCount++ % lastDeltas.Length] = lastDelta;

                prevPos = transform.position;

                if (lastDeltasSampleCount > lastDeltas.Length)
                {
                    float accum = 0.0f;
                    foreach (var sample in lastDeltas)
                    {
                        accum += sample.magnitude;
                    }

                    if (accum < 1e-3)
                    {
                        if (onStopped != null)
                        {
                            if (!onStopped.Invoke(this))
                            {
                                ForceStop();
                                ResetStopDetection();
                            }
                        }
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if ((rb != null) && (rb.bodyType == RigidbodyType2D.Dynamic))
            {
                TickAgent(Time.fixedDeltaTime, true);
            }
        }

        private void TickAgent(float dt, bool useRigidbody)
        {
            if (!_isMoving || path == null || currentPathIndex >= path.Count)
            {
                ForceStop();
                return;
            }

            if (followMode == PathFollowMode.FollowPursuit)
            {
                TickAgentPursuit(dt, useRigidbody);
            }
            else
            {
                TickAgentDirect(dt, useRigidbody);
            }
        }

        private void TickAgentPursuit(float dt, bool useRigidbody)
        {
            Vector2 currentPosition = transform.position;
            Vector2 target = path[currentPathIndex].pos;
            Vector2 toTarget = target - currentPosition;
            float distanceToTarget = toTarget.magnitude;

            if (distanceToTarget < stoppingDistance)
            {
                currentPathIndex++;
                if (currentPathIndex >= path.Count)
                {
                    ForceStop();

                    onComplete?.Invoke(this);

                    return;
                }
                target = path[currentPathIndex].pos;
                toTarget = target - currentPosition;
                distanceToTarget = toTarget.magnitude;
            }

            desiredDir = toTarget.normalized;

            // Clamp angle change using angularSpeed
            if (velocity.sqrMagnitude > 0.001f)
            {
                float angleDiff = Vector2.SignedAngle(velocity.normalized, desiredDir);
                float maxTurn = angularSpeed * dt;
                angleDiff = Mathf.Clamp(angleDiff, -maxTurn, maxTurn);
                desiredDir = Quaternion.Euler(0, 0, angleDiff) * velocity.normalized;
            }

            Vector2 desiredVelocity = desiredDir * speed;

            // Accelerate toward desiredVelocity
            velocity = Vector2.MoveTowards(velocity, desiredVelocity, acceleration * dt);

            if (useRigidbody)
            {
                rb.linearVelocity = velocity;
            }
            else
            {
                transform.position += (Vector3)(velocity * dt);
            }
        }

        void TickAgentDirect(float dt, bool useRigidbody)
        {
            if (path == null || path.Count < 2 || !_isMoving)
            {
                ForceStop();
                return;
            }

            Vector2 pos = (Vector2)transform.position;
            float maxMove = speed * dt;

            // Compute an offset goal so the agent can actually complete with lateral offset
            Vector2 goalCenter = path[path.Count - 1].pos;
            Vector2 lastA = path[path.Count - 2].pos;
            Vector2 lastB = path[path.Count - 1].pos;
            Vector2 lastTan = lastB - lastA;
            float lastLen = lastTan.magnitude;
            if (lastLen > 1e-6f) lastTan /= lastLen; else lastTan = Vector2.right;
            Vector2 lastLeft = new Vector2(-lastTan.y, lastTan.x);
            Vector2 goalOffset = goalCenter + lastLeft * followOffset.y;

            // Stop if we are within stopping distance of the offset goal
            if ((goalOffset - pos).sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                ForceStop();
                onComplete?.Invoke(this);
                return;
            }

            // 1) Closest point on the OFFSET path:
            //    For each segment, use its local 'left' to shift our position by -offset,
            //    then project, then re-apply +offset to get the corresponding point on the offset path.
            int startSeg = Mathf.Max(0, currentPathIndex - 1);
            int bestSeg = startSeg;
            float bestT = 0f;
            Vector2 bestCenter = path[startSeg].pos;
            Vector2 bestOffsetPt = bestCenter;
            float bestDistSq = float.MaxValue;

            for (int i = startSeg; i < path.Count - 1; i++)
            {
                Vector2 a = path[i].pos;
                Vector2 b = path[i + 1].pos;
                Vector2 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                if (abLenSq < 1e-8f) continue;

                Vector2 tan = ab / Mathf.Sqrt(abLenSq);
                Vector2 left = new Vector2(-tan.y, tan.x);

                // shift our position by -lateralOffset in this segment's frame
                Vector2 shiftedPos = pos - left * followOffset.y;

                float t = Vector2.Dot(shiftedPos - a, ab) / abLenSq;
                t = Mathf.Clamp01(t);
                Vector2 projCenter = a + ab * t;
                Vector2 projOffset = projCenter + left * followOffset.y;

                float d2 = (projOffset - pos).sqrMagnitude;
                if (d2 < bestDistSq)
                {
                    bestDistSq = d2;
                    bestSeg = i;
                    bestT = t;
                    bestCenter = projCenter;
                    bestOffsetPt = projOffset;
                }
            }

            // 2) Decide target using budget maxMove
            float toProjOffset = Vector2.Distance(pos, bestOffsetPt);
            Vector2 targetPoint;
            int aheadSeg = bestSeg;
            float aheadT = bestT;

            // Treat followOffset.x as additional along-path lookahead (meters)
            float alongLookahead = Mathf.Max(0f, followOffset.x);

            if (toProjOffset >= maxMove - 1e-5f)
            {
                // Cannot reach the offset path this tick, head toward it directly
                targetPoint = bestOffsetPt;
            }
            else
            {
                // Reach the offset path, then advance along the centerline by leftover + lookahead
                float leftover = maxMove - toProjOffset + alongLookahead;

                Vector2 forwardCenter = AdvanceAlongPath(bestSeg, bestT, leftover, out aheadSeg, out aheadT);

                // Lateral offset at the ahead segment's frame
                Vector2 sa = path[aheadSeg].pos;
                Vector2 sb = path[aheadSeg + 1].pos;
                Vector2 tan = sb - sa;
                float tlen = tan.magnitude;
                if (tlen > 1e-6f) tan /= tlen; else tan = Vector2.right;
                Vector2 left = new Vector2(-tan.y, tan.x);

                targetPoint = forwardCenter + left * followOffset.y;

                // Advance path index monotonically
                currentPathIndex = Mathf.Clamp(Mathf.Max(currentPathIndex, aheadSeg + 1), 1, path.Count - 1);
            }

            // 3) Move directly to targetPoint (no steering/accel). Fallback to tangent if degenerate.
            Vector2 dir = targetPoint - pos;
            float d = dir.magnitude;
            if (d < 1e-6f)
            {
                Vector2 a2 = path[bestSeg].pos;
                Vector2 b2 = path[Mathf.Min(bestSeg + 1, path.Count - 1)].pos;
                Vector2 tan2 = b2 - a2;
                if (tan2.sqrMagnitude < 1e-12f) tan2 = Vector2.right;
                dir = tan2.normalized;
            }
            else
            {
                dir /= d;
            }

            desiredDir = dir;
            velocity = desiredDir * speed;

            if (useRigidbody && rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.linearVelocity = velocity;
            }
            else
            {
                transform.position = (Vector2)transform.position + velocity * dt;
            }
        }

        // Advance forward along the polyline by a given arc length starting at (segIndex, t).
        private Vector2 AdvanceAlongPath(int segIndex, float t, float distance, out int outSegIndex, out float outT)
        {
            outSegIndex = Mathf.Clamp(segIndex, 0, Mathf.Max(0, path.Count - 2));
            outT = Mathf.Clamp01(t);

            Vector2 a = path[outSegIndex].pos;
            Vector2 b = path[outSegIndex + 1].pos;

            while (true)
            {
                Vector2 ab = b - a;
                float segLen = ab.magnitude;

                if (segLen < 1e-6f)
                {
                    // Skip degenerate segment
                    outSegIndex++;
                    if (outSegIndex >= path.Count - 1)
                    {
                        outSegIndex = path.Count - 2;
                        outT = 1f;
                        return path[path.Count - 1].pos;
                    }
                    a = path[outSegIndex].pos;
                    b = path[outSegIndex + 1].pos;
                    outT = 0f;
                    continue;
                }

                float remaining = (1f - outT) * segLen;

                if (distance <= remaining + 1e-6f)
                {
                    float dtStep = distance / segLen;
                    outT += dtStep;
                    return a + ab * outT;
                }

                // consume this segment and move on
                distance -= remaining;
                outSegIndex++;
                if (outSegIndex >= path.Count - 1)
                {
                    outSegIndex = path.Count - 2;
                    outT = 1f;
                    return path[path.Count - 1].pos;
                }

                a = path[outSegIndex].pos;
                b = path[outSegIndex + 1].pos;
                outT = 0f;
            }
        }

        void ForceStop()
        {
            if (_isMoving)
                _isMoving = false;
            velocity = Vector2.zero;
            if ((rb) && (rb.bodyType == RigidbodyType2D.Dynamic))
            {
                rb.linearVelocity = Vector2.zero;
            }
            targetPosition = transform.position;
        }

        public bool SetDestination(Vector2 newTargetPosition)
        {
            if (navMesh == null) return false;
            if (newTargetPosition == targetPosition) return true;

            List<NavMesh2d.PathNode>    newPath = null;
            List<int>                   polyIds = null;

            if (!navMesh.GetPointOnNavMesh(transform.position, regionId, out int startPolyId, out Vector3 startPosOnNavmesh))
            {
                return false;
            }
            if (!navMesh.GetPointOnNavMesh(newTargetPosition, regionId, out int endPolyId, out Vector3 endPosOnNavmesh))
            {
                return false;
            }

            var result = navMesh.PlanPathOnNavmesh(startPosOnNavmesh, startPolyId, endPosOnNavmesh, endPolyId, regionId, ref polyIds, ref newPath);
            if (result == NavMesh2d.PathState.NoPath || newPath.Count < 2)
            {
                _isMoving = false;
                return false;
            }

            if (smoothPath)
            {
                newPath = SmoothPath(newPath);
            }

            targetPosition = newTargetPosition;
            path = newPath;
            currentPathIndex = 1; // start pursuing the first target (index 1), since [0] is usually the current pos
            lastDeltasSampleCount = 0;
            _isMoving = true;
            return true;
        }
        
        // Chaikin’s corner-cutting (Lane-Riesenfeld)
        private List<NavMesh2d.PathNode> SmoothPath(List<NavMesh2d.PathNode> inputPath)
        {
            // Interpret: iterations = segmentsPerWaypoint, cut ratio r = tangentScale
            int iters = Mathf.Clamp(segmentsPerWaypoint, 1, 6);
            float r = Mathf.Clamp(tangentScale, 0.05f, 0.49f);

            if (inputPath == null || inputPath.Count < 3 || iters <= 0)
                return inputPath;

            // Copy to a working list of Vector2
            var pts = new List<Vector2>(inputPath.Count);
            for (int i = 0; i < inputPath.Count; i++) pts.Add(inputPath[i].pos);

            for (int k = 0; k < iters; k++)
            {
                if (pts.Count < 3) break;

                var outPts = new List<Vector2>(pts.Count * 2);
                // Keep first endpoint
                outPts.Add(pts[0]);

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i];
                    Vector2 b = pts[i + 1];

                    // Chaikin split: Q = (1-r)*a + r*b, R = r*a + (1-r)*b
                    Vector2 q = a + (b - a) * r;
                    Vector2 s = a + (b - a) * (1f - r);

                    outPts.Add(q);
                    outPts.Add(s);
                }

                // Keep last endpoint
                outPts.Add(pts[pts.Count - 1]);

                pts = outPts;
            }

            // Optional: collapse tiny segments to avoid zero-length steps
            var cleaned = new List<Vector2>(pts.Count);
            const float eps2 = 1e-6f;
            Vector2 prev = pts[0];
            cleaned.Add(prev);
            for (int i = 1; i < pts.Count; i++)
            {
                if ((pts[i] - prev).sqrMagnitude > eps2)
                {
                    cleaned.Add(pts[i]);
                    prev = pts[i];
                }
            }
            // Ensure at least start and end
            if (cleaned.Count < 2)
            {
                cleaned.Clear();
                cleaned.Add(pts[0]);
                cleaned.Add(pts[pts.Count - 1]);
            }

            // Repack as PathNode
            var smoothed = new List<NavMesh2d.PathNode>(cleaned.Count);
            for (int i = 0; i < cleaned.Count; i++) smoothed.Add(new NavMesh2d.PathNode(cleaned[i]));
            return smoothed;
        }


        public void ResetStopDetection()
        {
            lastDeltasSampleCount = 0;
        }

        private void OnDrawGizmos()
        {
            if (_isMoving)
            {
                if (path != null && path.Count > 1)
                {
                    Gizmos.color = Color.cyan;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Gizmos.DrawLine(path[i].pos, path[i + 1].pos);
                    }

                    if (currentPathIndex < path.Count)
                    {
                        var target = path[currentPathIndex].pos;
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere(target, 2.0f);
                    }
                }

                Gizmos.color = Color.cyan;
                DebugHelpers.DrawArrow(transform.position, desiredDir, 10.0f, 5.0f, desiredDir.Perpendicular());
                Gizmos.color = Color.yellow;
                DebugHelpers.DrawArrow(transform.position, velocity.normalized, 10.0f, 5.0f, desiredDir.Perpendicular());

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(targetPosition, 3.0f);
            }
        }

    }
}
