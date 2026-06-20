using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using System;




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
        private NavMeshAgentType2d       agentType;
        [SerializeField] 
        private PathFollowMode  followMode = PathFollowMode.FollowPursuit;
        [SerializeField] 
        private float           speed;
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           angularSpeed;
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           acceleration;
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           lookaheadTime = 0.25f;   // seconds of time headway
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           minLookahead = 8.0f;     // pixels
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           maxLookahead = 64.0f;    // pixels
        [SerializeField, ShowIf(nameof(isFollowPursuit)), Label("Use Navmesh LOS")] 
        private bool            useNavmeshLoS = true;
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           slowAngleDeg = 45.0f;   // start slowing when heading error > this
        [SerializeField, ShowIf(nameof(isFollowPursuit))] 
        private float           endSlowRadius = 24.0f;   // soften final approach (pixels)        [SerializeField, ShowIf(nameof(isFollowDirect))]
        [SerializeField]
        private bool            smoothPath;
        [SerializeField, Range(1, 6), ShowIf(nameof(isSmoothPath)), Tooltip("Fraction of segment length for tangent control points")]
        private int             segmentsPerWaypoint= 3; 
        [SerializeField, Range(0.0f, 0.5f), ShowIf(nameof(isSmoothPath)), Tooltip("Fraction of segment length for tangent control points")]
        private float           tangentScale = 0.3f;
        [SerializeField, ShowIf(nameof(isFollowDirect))]
        private Vector2         followOffset;
        [SerializeField]
        private float           stoppingDistance = 2.0f;
        [SerializeField, Tooltip("If set, when the target is unreachable (e.g. a closed door) the agent moves to the closest reachable point instead of refusing to move.")]
        private bool            allowPartialPaths = true;


        bool isFollowDirect => followMode == PathFollowMode.FollowDirect;
        bool isFollowPursuit => followMode == PathFollowMode.FollowPursuit;
        bool isSmoothPath => smoothPath;


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

        // Manual (non-auto-traverse) link state. While traversing, path following is held until
        // TraversalComplete() is called; _pendingPath is the remainder resumed from the link far side.
        private bool                        _traversing;
        private List<NavMesh2d.PathNode>    _pendingPath;

        // The position the caller actually asked for (kept even when only a partial path was found, so
        // 'targetPosition' can hold the reachable end for replanning while gizmos show the real target).
        private Vector2                     _requestedTarget;

        public NavMeshLink2d    TraversalLink { get; private set; }
        public Vector3          TraversalStart { get; private set; }
        public Vector3          TraversalEnd { get; private set; }
        public bool             IsTraversing => _traversing;

        // State of the most recent plan: Full when the target was reached, Partial when only the
        // closest reachable point could be reached (e.g. behind a closed door).
        public NavMesh2d.PathState LastPathState { get; private set; } = NavMesh2d.PathState.NoPath;
        public bool             IsPartialPath => LastPathState == NavMesh2d.PathState.Partial;

        public void SetMaxSpeed(float maxSpeed)
        {
            this.speed = maxSpeed;
        }

        public bool isMoving => _isMoving;

        void Start()
        {
            navMesh = NavMesh2d.Get(agentType);
            if (navMesh == null)
            {
                Debug.LogWarning($"Navmesh not found for agent of type {agentType.name}!");
            }
            else
            {
                regionId = navMesh.GetRegion(transform.position);
            }

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
            // While a manual link traversal is in progress, navigation is held until TraversalComplete().
            if (_traversing) return;

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
            // Early outs
            if (path == null || path.Count < 2)
            {
                ForceStop();
                return;
            }

            Vector2 pos = transform.position;

            // -- 1) Find closest point on the current (center) path and advance past waypoints by projection
            int bestSeg = Mathf.Clamp(currentPathIndex - 1, 0, path.Count - 2);
            float bestT = 0f;
            float bestD2 = float.MaxValue;

            for (int i = bestSeg; i < path.Count - 1; i++)
            {
                Vector2 a = path[i].pos;
                Vector2 b = path[i + 1].pos;
                Vector2 ab = b - a;
                float ab2 = ab.sqrMagnitude; if (ab2 < 1e-8f) continue;

                float t = Mathf.Clamp01(Vector2.Dot(pos - a, ab) / ab2);
                Vector2 proj = a + ab * t;
                float d2 = (proj - pos).sqrMagnitude;
                if (d2 < bestD2) { bestD2 = d2; bestSeg = i; bestT = t; }
            }

            // Advance index monotonically so we never "orbit" a finished segment
            currentPathIndex = Mathf.Clamp(Mathf.Max(currentPathIndex, bestSeg + (bestT > 0.999f ? 1 : 0)), 1, path.Count - 1);

            // -- 2) Compute lookahead target along the path (time-headway + clamp)
            float L = Mathf.Clamp(minLookahead + lookaheadTime * velocity.magnitude, minLookahead, maxLookahead);
            int laSeg; float laT;
            Vector2 lookaheadCenter = AdvanceAlongPath(bestSeg, bestT, L, out laSeg, out laT);

            // Optional: clamp to last visible point on navmesh
            Vector2 target = lookaheadCenter;
            if (useNavmeshLoS && navMesh != null)
            {
                // The agent may have walked across an auto-traverse link into another region; keep the
                // region used for line-of-sight in sync with the current position.
                int curRegion = navMesh.GetRegion(pos);
                if (curRegion >= 0) regionId = curRegion;

                if (!navMesh.RaycastVector(pos, (lookaheadCenter - pos).normalized,
                                            Vector2.Distance(pos, lookaheadCenter),
                                            regionId, out Vector3 hit, out int _))
                {
                    // LoS blocked: RaycastVector returns false with 'hit' at last valid point
                    target = hit;
                }
            }

            // -- 3) Arrival speed control
            // 3a) Heading-based slow-down (large angle => slow)
            Vector2 pathTan;
            {
                // tangent at the lookahead segment
                Vector2 sa = path[laSeg].pos;
                Vector2 sb = path[Mathf.Min(laSeg + 1, path.Count - 1)].pos;
                pathTan = (sb - sa); if (pathTan.sqrMagnitude < 1e-8f) pathTan = (target - pos);
                pathTan = pathTan.normalized;
            }

            Vector2 toTarget = target - pos;
            float distToTarget = Mathf.Max(0.0f, toTarget.magnitude);
            Vector2 desiredDirNow = (distToTarget > 1e-6f) ? (toTarget / distToTarget) : pathTan;

            float angleErr = 0f;
            if (velocity.sqrMagnitude > 1e-6f)
                angleErr = Mathf.Abs(Vector2.SignedAngle(velocity.normalized, desiredDirNow));

            float angleFactor = Mathf.Clamp01(1.0f - angleErr / Mathf.Max(1.0f, slowAngleDeg * 2.0f));
            // 3b) Braking for end of path and close targets
            // approximate remaining distance along path from (bestSeg,bestT) to the end
            float remaining = 0f;
            {
                Vector2 acc = path[bestSeg].pos + (path[bestSeg + 1].pos - path[bestSeg].pos) * bestT;
                for (int i = bestSeg; i < path.Count - 1; i++)
                {
                    Vector2 a = (i == bestSeg) ? acc : path[i].pos;
                    Vector2 b = path[i + 1].pos;
                    remaining += Vector2.Distance(a, b);
                }
            }
            // soften near the very end regardless of braking distance
            float endFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining / Mathf.Max(1e-3f, endSlowRadius)));

            // Physics braking distance
            float brakeDist = (velocity.sqrMagnitude) / Mathf.Max(1e-3f, 2f * acceleration);
            float brakeFactor = Mathf.Clamp01(remaining / Mathf.Max(1e-3f, brakeDist));

            float desiredSpeedMag = speed * angleFactor * endFactor * brakeFactor;

            // -- 4) Turn-rate limited steering (your original idea kept)
            Vector2 desiredDir = desiredDirNow;
            if (velocity.sqrMagnitude > 1e-6f)
            {
                float ang = Vector2.SignedAngle(velocity.normalized, desiredDirNow);
                float maxTurn = angularSpeed * dt;
                ang = Mathf.Clamp(ang, -maxTurn, maxTurn);
                desiredDir = Quaternion.Euler(0, 0, ang) * velocity.normalized;
            }

            Vector2 desiredVel = desiredDir.normalized * desiredSpeedMag;
            velocity = Vector2.MoveTowards(velocity, desiredVel, acceleration * dt);

            if (useRigidbody && rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                rb.linearVelocity = velocity;
            else
                transform.position = (Vector2)transform.position + velocity * dt;

            // If we are within stopping distance of the final goal, finish
            Vector2 goal = path[path.Count - 1].pos;
            if ((goal - pos).sqrMagnitude <= stoppingDistance * stoppingDistance && remaining <= endSlowRadius)
            {
                ForceStop();
                OnReachedGoal();
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
                OnReachedGoal();
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

            // Resolve start and end without pinning to a single region: the destination may be in
            // another region reachable through a NavMeshLink2d.
            if (!navMesh.GetPointOnNavMesh(transform.position, out int startRegion, out int startPolyId, out Vector3 startPosOnNavmesh))
            {
                return false;
            }
            if (!navMesh.GetPointOnNavMesh(newTargetPosition, out int endRegion, out int endPolyId, out Vector3 endPosOnNavmesh))
            {
                return false;
            }

            var result = navMesh.PlanPathOnNavmesh(startPosOnNavmesh, startRegion, startPolyId, endPosOnNavmesh, endRegion, endPolyId, ref polyIds, ref newPath, agentType : agentType, agent : this, allowPartial : allowPartialPaths);
            LastPathState = result;
            if (result == NavMesh2d.PathState.NoPath || newPath.Count < 2)
            {
                _isMoving = false;
                return false;
            }

            regionId = startRegion;
            _requestedTarget = newTargetPosition;
            // On a partial path the goal was unreachable, so record the reachable end as the target.
            // This keeps the dedupe at the top of SetDestination from blocking a later replan toward the
            // real (still unreachable) target - letting the agent retry once the route opens up.
            targetPosition = (result == NavMesh2d.PathState.Partial) ? (Vector2)newPath[newPath.Count - 1].pos : newTargetPosition;
            lastDeltasSampleCount = 0;
            _traversing = false;
            _isMoving = true;
            SetActivePath(newPath);
            return true;
        }

        // Sets 'path' to the portion of 'full' up to (and including) the near side of the first
        // manual-traversal link; any remainder after the link is stashed in _pendingPath and resumed
        // by TraversalComplete(). Auto-traverse links stay inline and need no special handling.
        private void SetActivePath(List<NavMesh2d.PathNode> full)
        {
            List<NavMesh2d.PathNode> active = full;
            _pendingPath = null;

            for (int i = 1; i < full.Count; i++)
            {
                if (full[i].link != null && full[i].manualTraversal)
                {
                    active = full.GetRange(0, i + 1);
                    _pendingPath = full.GetRange(i + 1, full.Count - (i + 1));
                    break;
                }
            }

            // Smoothing rebuilds nodes from positions, so re-apply the near-side link tag afterwards.
            NavMesh2d.PathNode tail = active[active.Count - 1];
            if (isSmoothPath && active.Count >= 3)
            {
                active = SmoothPath(active);
                if (tail.link != null)
                    active[active.Count - 1] = new NavMesh2d.PathNode(active[active.Count - 1].pos, tail.link, tail.manualTraversal);
            }

            path = active;
            currentPathIndex = 1; // start pursuing the first target (index 1), since [0] is the current pos
            ResetStopDetection();
        }

        // Called when the follower reaches the end of the active path. If that end is the near side of
        // a manual link, start the traversal handshake; otherwise the destination has been reached.
        private void OnReachedGoal()
        {
            if (path != null && path.Count > 0)
            {
                var last = path[path.Count - 1];
                if (last.link != null && last.manualTraversal)
                {
                    BeginManualTraversal(last.link);
                    return;
                }
            }
            onComplete?.Invoke(this);
        }

        private void BeginManualTraversal(NavMeshLink2d link)
        {
            INavMeshLinkTraversal handler = null;
            foreach (var t in GetComponents<INavMeshLinkTraversal>())
            {
                if (t.CanHandleTraversal(this, link)) { handler = t; break; }
            }

            Vector3 farSide = (_pendingPath != null && _pendingPath.Count > 0)
                              ? _pendingPath[0].pos
                              : (Vector3)transform.position;

            if (handler == null)
            {
                Debug.LogWarning($"NavMeshAgent2d '{name}' reached manual link '{link.name}' but no INavMeshLinkTraversal on this GameObject can handle it.");
                _pendingPath = null;
                ForceStop();
                return;
            }

            _traversing = true;
            _isMoving = false;
            velocity = Vector2.zero;
            if (rb && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = Vector2.zero;

            TraversalLink = link;
            TraversalStart = transform.position;
            TraversalEnd = farSide;

            handler.BeginTraversal(this, link);
        }

        /// <summary>
        /// Signals that a manual link traversal (started via INavMeshLinkTraversal.BeginTraversal) has
        /// finished and the agent is at the link's far side. Path following then resumes.
        /// </summary>
        public void TraversalComplete()
        {
            if (!_traversing) return;
            _traversing = false;
            TraversalLink = null;

            var remaining = _pendingPath;
            _pendingPath = null;

            if (remaining != null && remaining.Count > 0)
            {
                // Make sure we are exactly at the planned far-side point before continuing.
                Vector3 farSide = remaining[0].pos;
                if (rb && rb.bodyType == RigidbodyType2D.Dynamic) rb.position = farSide;
                else transform.position = farSide;
                regionId = navMesh.GetRegion(farSide);
            }

            if (remaining != null && remaining.Count >= 2)
            {
                _isMoving = true;
                SetActivePath(remaining);
            }
            else
            {
                ForceStop();
                onComplete?.Invoke(this);
            }
        }

        // Conservative segment legality: walk the segment in short steps over the navmesh.
        // Returns true iff the whole [a,b] is inside the mesh (no boundary hits).
        private bool SegmentLegalChunked(Vector2 a, Vector2 b)
        {
            if (navMesh == null) return true; // fail-open for editor-time convenience

            Vector2 d = b - a;
            float dist = d.magnitude;
            if (dist <= 1e-5f) return true;

            // Choose a safe step length, agent radius seems like a good guess
            var steplen = agentType.agentRadius;

            Vector2 dir = d / dist;
            Vector3 cur = a;
            float remaining = dist;
            int guard = 0;

            while (remaining > 1e-4f && guard++ < 4096)
            {
                float step = Mathf.Min(steplen, remaining);

                Vector3 end;
                int poly;
                bool hitBoundary = navMesh.RaycastVector(cur, dir, step, regionId, out end, out poly);

                if (hitBoundary)
                    return false; // crossed a forbidden boundary in this chunk

                float advanced = ((Vector2)end - (Vector2)cur).magnitude;
                if (advanced < 1e-6f)
                    return false; // failed to advance -> treat as blocked

                cur = end;
                remaining -= advanced;
            }

            // Close enough to the intended endpoint b?
            return (((Vector2)cur) - b).sqrMagnitude <= 1.0f; // ~1 px tolerance
        }

        private List<NavMesh2d.PathNode> SmoothPath(List<NavMesh2d.PathNode> input)
        {
            int iters = Mathf.Clamp(segmentsPerWaypoint, 1, 6);
            float r = Mathf.Clamp(tangentScale, 0.05f, 0.49f);
            if (input == null || input.Count < 3 || iters <= 0) return input;

            // Work in Vector2
            var pts = new List<Vector2>(input.Count);
            for (int i = 0; i < input.Count; i++) pts.Add(input[i].pos);

            for (int pass = 0; pass < iters; pass++)
            {
                if (pts.Count < 3) break;

                var outPts = new List<Vector2>(pts.Count * 2);
                outPts.Add(pts[0]); // keep start

                // State used to possibly backtrack one corner if the next bridge is illegal
                int lastCornerStart = -1;   // index in outPts where we started writing the last corner (q)
                Vector2 lastCornerB = Vector2.zero;
                Vector2 lastCornerS = Vector2.zero;
                bool lastCornerAccepted = false;

                for (int j = 1; j <= pts.Count - 2; j++) // corners B = pts[j]
                {
                    Vector2 A = pts[j - 1];
                    Vector2 B = pts[j];
                    Vector2 C = pts[j + 1];

                    // Chaikin-style points around corner B:
                    // q on AB near B, s on BC near B
                    Vector2 q = Vector2.Lerp(A, B, 1f - r);
                    Vector2 s = Vector2.Lerp(B, C, r);

                    // Gentle fallback handles
                    Vector2 q2 = Vector2.Lerp(q, B, 0.5f);
                    Vector2 s2 = Vector2.Lerp(s, B, 0.5f);

                    // If we had a previous smoothed corner, validate the bridge [S_prev -> q]
                    if (lastCornerAccepted)
                    {
                        if (!SegmentLegalChunked(lastCornerS, q))
                        {
                            // Try gentler once
                            if (!SegmentLegalChunked(lastCornerS, q2))
                            {
                                // Bridge is illegal -> backtrack previous corner:
                                // remove its q,s and keep the raw corner instead
                                outPts.RemoveRange(lastCornerStart, outPts.Count - lastCornerStart);
                                outPts.Add(lastCornerB);
                                lastCornerAccepted = false;

                                // Now re-test this corner starting from the new last point (raw B)
                                if (!SegmentLegalChunked(outPts[outPts.Count - 1], q) ||
                                    !SegmentLegalChunked(q, s))
                                {
                                    // Try gentler once
                                    if (!SegmentLegalChunked(outPts[outPts.Count - 1], q2) ||
                                        !SegmentLegalChunked(q2, s2))
                                    {
                                        // Give up on smoothing this corner; keep raw B and continue
                                        outPts.Add(B);
                                        continue;
                                    }
                                    else
                                    {
                                        q = q2; s = s2;
                                    }
                                }

                                // Accept this corner with q,s
                                lastCornerStart = outPts.Count;
                                lastCornerB = B;
                                lastCornerS = s;
                                lastCornerAccepted = true;
                                outPts.Add(q);
                                outPts.Add(s);
                                continue;
                            }
                            else
                            {
                                q = q2; // gentler q works for the bridge; keep going
                            }
                        }
                    }

                    // Validate from current output tail -> q, and the local bend q->s
                    if (!SegmentLegalChunked(outPts[outPts.Count - 1], q) ||
                        !SegmentLegalChunked(q, s))
                    {
                        // Try gentler once
                        if (!SegmentLegalChunked(outPts[outPts.Count - 1], q2) ||
                            !SegmentLegalChunked(q2, s2))
                        {
                            // Raw corner only
                            outPts.Add(B);
                            lastCornerAccepted = false;
                            continue;
                        }
                        q = q2; s = s2;
                    }

                    // Tentatively accept this corner; we may still backtrack on the next iteration
                    lastCornerStart = outPts.Count;
                    lastCornerB = B;
                    lastCornerS = s;
                    lastCornerAccepted = true;
                    outPts.Add(q);
                    outPts.Add(s);
                }

                // No more corners. Done; append the last input point.
                outPts.Add(pts[pts.Count - 1]);

                // Replace for next iteration
                pts = outPts;
            }

            // Pack back to PathNode
            var result = new List<NavMesh2d.PathNode>(pts.Count);
            Vector2 prev = pts[0];
            result.Add(new NavMesh2d.PathNode(prev));
            for (int i = 1; i < pts.Count; i++)
            {
                if ((pts[i] - prev).sqrMagnitude > 1e-6f)
                {
                    result.Add(new NavMesh2d.PathNode(pts[i]));
                    prev = pts[i];
                }
            }
            return result;
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
                    bool partial = IsPartialPath;
                    Gizmos.color = partial ? new Color(1.0f, 0.5f, 0.0f) : Color.cyan;
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Gizmos.DrawLine(path[i].pos, path[i + 1].pos);
                    }

                    if (partial)
                    {
                        // The agent is heading to the closest reachable point; show the gap to the target.
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(path[path.Count - 1].pos, _requestedTarget);
                    }

                    if (currentPathIndex < path.Count)
                    {
                        var target = path[currentPathIndex].pos;
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere(target, 2.0f);
                    }
                }

                Gizmos.color = Color.cyan;
                DebugHelpers.DrawArrow(transform.position, desiredDir, 10.0f, 5.0f, 45.0f, desiredDir.Perpendicular());
                Gizmos.color = Color.yellow;
                DebugHelpers.DrawArrow(transform.position, velocity.normalized, 10.0f, 5.0f, 45.0f, desiredDir.Perpendicular());

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_requestedTarget, 3.0f);
            }

            if (_traversing)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(TraversalStart, TraversalEnd);
                Gizmos.DrawWireSphere(TraversalEnd, 3.0f);
            }
        }
    }
}
