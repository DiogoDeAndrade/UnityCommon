using System.Collections.Generic;
using UC;
using UnityEngine;
using System.IO;
using UnityEditor.PackageManager.UI;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{
    public class NavMeshAgent2d : MonoBehaviour
    {
        public delegate void OnComplete(NavMeshAgent2d agent);
        public event OnComplete onComplete;
        public delegate bool OnStopped(NavMeshAgent2d agent);
        public OnStopped onStopped;

        [SerializeField] private AgentType agentType;
        [SerializeField] private float speed;
        [SerializeField] private float angularSpeed;
        [SerializeField] private float acceleration;
        [SerializeField] private float stoppingDistance = 2.0f;


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
                TickAgent(Time.deltaTime, useRigidbody: false);
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
                TickAgent(Time.fixedDeltaTime, useRigidbody: true);
            }
        }

        private void TickAgent(float dt, bool useRigidbody)
        {
            if (!_isMoving || path == null || currentPathIndex >= path.Count)
            {
                ForceStop();
                return;
            }

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

        void ForceStop()
        {
            if (_isMoving)
                _isMoving = false;
            velocity = Vector2.zero;
            if ((rb) && (rb.bodyType == RigidbodyType2D.Dynamic))
            {
                rb.linearVelocity = Vector2.zero;
            }
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

            targetPosition = newTargetPosition;
            path = newPath;
            currentPathIndex = 1; // start pursuing the first target (index 1), since [0] is usually the current pos
            _isMoving = true;
            return true;
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