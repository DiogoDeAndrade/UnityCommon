using System.Collections.Generic;
using UC;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{
    public class NavMeshAgent2d : MonoBehaviour
    {
        [SerializeField] private AgentType agentType;
        [SerializeField] private float maxSpeed;
        [SerializeField] private float stopDistance = 2.0f;

        protected bool          navMesh = false;
        protected bool          _isMoving = false;
        protected Vector2       targetPosition;
        protected List<Vector2> currentPath;
        protected Vector2[]     lastDeltas;
        protected int           lastDeltasIndex = 0;
        protected Vector2       lastDeltaPos;
        protected Vector2       moveDir;
        protected Rigidbody2D   rb;
        protected Vector2       requestedMovePoint;

        public void SetMaxSpeed(float maxSpeed)
        {
            this.maxSpeed = maxSpeed;
        }

        public bool isMoving => _isMoving;

        void Start()
        {
            lastDeltas = new Vector2[16];
            lastDeltaPos = transform.position;
            rb = GetComponent<Rigidbody2D>();

        }

        void Update()
        {
            // Update delta movement (to check if we're stuck for 16 frames)
            Vector3 delta = transform.position.xy() - lastDeltaPos;
            lastDeltas[lastDeltasIndex] = delta;
            lastDeltasIndex = (lastDeltasIndex + 1) % lastDeltas.Length;
            lastDeltaPos = transform.position;
        }

        private void FixedUpdate()
        {
            UpdateMovement();
        }

        public bool MoveTo(Vector2 newTargetPosition)
        {
            // If it's already moving, check if the target position has changed enough for
            // replanning
            if ((_isMoving) && (currentPath != null))
            {
                if (Vector3.Distance(requestedMovePoint, newTargetPosition) < stopDistance)
                {
                    // No need to replan
                    return true;
                }
            }

            // Check if this position is already my position
            if (Vector3.Distance(transform.position, newTargetPosition) < stopDistance)
            {
                // Already there
                return false;
            }

            // Plan a path - for now, just a straight line from where we are to the target
            List<Vector2> newPath = new();
            newPath.Add(newTargetPosition);

            currentPath = newPath;
            _isMoving = true;
            requestedMovePoint = newTargetPosition;

            return true;
        }

        bool UpdateMovement()
        {
            if (currentPath == null) return true;

            moveDir = Vector2.zero;
            float maxDisp = maxSpeed * Time.fixedDeltaTime;

            while (currentPath.Count > 0)
            {
                Vector3 moveDir = (currentPath[0] - transform.position.xy());
                if (moveDir.magnitude < maxDisp)
                {
                    transform.position = currentPath[0];
                    currentPath.PopFirst();
                }
                else
                {
                    moveDir.Normalize();

                    rb.linearVelocity = moveDir * maxSpeed;

                    return false;
                }
            }

            rb.linearVelocity = Vector2.zero;
            currentPath = null;
            _isMoving = false;

            return true;
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (currentPath == null || currentPath.Count == 0)
                return;

            Handles.color = Color.yellow;

            Vector3 prev = transform.position;
            foreach (var point in currentPath)
            {
                Handles.DrawDottedLine(prev, point, 5f); // 4 = dash spacing
                prev = point;
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(prev, requestedMovePoint);
            Gizmos.DrawSphere(requestedMovePoint, 4.0f);
#endif
        }
    }
}