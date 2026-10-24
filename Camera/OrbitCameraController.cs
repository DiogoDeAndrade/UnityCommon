using NaughtyAttributes;
using UnityEngine;

namespace UC
{
    // Camera at fixed angles and distance from a target (found by hypertag), following it with smoothing.
    // Optionally limited: in Position mode the camera position is clamped to a box.
    public class OrbitCameraController : MonoBehaviour
    {
        public enum UpdateMode { Update = 0, FixedUpdate = 1, LateUpdate = 2 };
        public enum FollowMode { Exponential = 0, Linear = 1 };
        public enum LimitMode { None = 0, Position = 1 };

        [SerializeField]
        private Hypertag targetTag;
        [SerializeField]
        private Vector3 targetOffset = Vector3.zero;
        [SerializeField]
        private float distance = 10.0f;
        [SerializeField]
        private float angleX = 45.0f;
        [SerializeField]
        private float angleY = 0.0f;
        [SerializeField]
        private UpdateMode updateMode = UpdateMode.LateUpdate;
        [SerializeField]
        private FollowMode followMode = FollowMode.Exponential;
        [SerializeField]
        private float followFactor = 0.1f;
        [SerializeField]
        private float followSpeed = 10.0f;
        [SerializeField]
        private bool snapOnStart = true;
        [SerializeField]
        private bool displayFocusPoint = false;
        [SerializeField]
        private float displayFocusPointRadius = 0.25f;
        [SerializeField, Tooltip("How fast the zoom factor eases towards its target (higher = snappier)")]
        private float zoomSpeed = 4.0f;
        [SerializeField, Tooltip("None: the camera goes wherever the target takes it. Position: the camera position is clamped to the limits box")]
        private LimitMode limitMode = LimitMode.None;
        [SerializeField, ShowIf(nameof(hasLimits)), Tooltip("Box the camera is kept inside. Only its shape is used, so the collider can be disabled (and should be, or be a trigger, so nothing bumps into it)")]
        private BoxCollider cameraLimits;

        public Vector3 focusPoint { get; private set; }

        private Transform target;
        private float targetZoom = 1.0f;    // Requested zoom factor (1 = the configured distance)
        private float currentZoom = 1.0f;   // Smoothed zoom factor actually applied

        bool hasLimits => limitMode != LimitMode.None;

        // Base distance, before zoom
        public float GetDistance() => distance;
        public void SetDistance(float v) { distance = v; }

        // Zoom factor multiplies the base distance; smoothed over time, so setting it repeatedly is harmless
        public float zoomFactor
        {
            get => targetZoom;
            set => targetZoom = Mathf.Max(0.01f, value);
        }
        public float currentZoomFactor => currentZoom;
        public float GetCurrentDistance() => distance * currentZoom;

        public Vector2 GetAngles() => new Vector2(angleX, angleY);
        public void SetAngles(float angleX, float angleY) { this.angleX = angleX; this.angleY = angleY; }

        public void SetLimits(LimitMode mode, BoxCollider limits) { limitMode = mode; cameraLimits = limits; }

        void Start()
        {
            FetchTarget();

            if (snapOnStart)
            {
                Snap();
            }
            else
            {
                focusPoint = transform.position + transform.forward * distance;
            }
        }

        void Update()
        {
            if (updateMode == UpdateMode.Update) Run_Update(Time.deltaTime);
        }
        void FixedUpdate()
        {
            if (updateMode == UpdateMode.FixedUpdate) Run_Update(Time.fixedDeltaTime);
        }
        void LateUpdate()
        {
            if (updateMode == UpdateMode.LateUpdate) Run_Update(Time.deltaTime);
        }

        void Run_Update(float deltaTime)
        {
            if (target == null)
            {
                FetchTarget();
                if (target == null) return;
            }

            Vector3 targetPos = GetTargetPos();

            // Ease the zoom factor towards the requested one (frame-rate independent exponential)
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, 1.0f - Mathf.Exp(-zoomSpeed * deltaTime));

            // The focus point is what gets smoothed, the camera is always rigidly attached to it
            switch (followMode)
            {
                case FollowMode.Exponential:
                    {
                        Vector3 err = targetPos - focusPoint;
                        focusPoint = focusPoint + err * followFactor;
                    }
                    break;
                case FollowMode.Linear:
                    focusPoint = Vector3.MoveTowards(focusPoint, targetPos, followSpeed * deltaTime);
                    break;
                default:
                    break;
            }

            PlaceCamera();
        }

        void PlaceCamera()
        {
            Quaternion rotation = Quaternion.Euler(angleX, angleY, 0.0f);

            transform.position = ApplyLimits(focusPoint - rotation * Vector3.forward * (distance * currentZoom));
            transform.rotation = rotation;
        }

        // Clamps a camera position to the limits box. Done in the box's own space, so it can be rotated or scaled.
        Vector3 ApplyLimits(Vector3 position)
        {
            if ((limitMode != LimitMode.Position) || (cameraLimits == null)) return position;

            Transform limitsTransform = cameraLimits.transform;
            Vector3 local = limitsTransform.InverseTransformPoint(position);
            Vector3 min = cameraLimits.center - cameraLimits.size * 0.5f;
            Vector3 max = cameraLimits.center + cameraLimits.size * 0.5f;

            local.x = Mathf.Clamp(local.x, min.x, max.x);
            local.y = Mathf.Clamp(local.y, min.y, max.y);
            local.z = Mathf.Clamp(local.z, min.z, max.z);

            return limitsTransform.TransformPoint(local);
        }

        // Moves the camera to the final position immediately, no smoothing
        public void Snap()
        {
            if (target == null) FetchTarget();
            if (target == null) return;

            focusPoint = GetTargetPos();
            currentZoom = targetZoom;
            PlaceCamera();
        }

        void FetchTarget()
        {
            if (targetTag == null) return;

            target = Hypertag.FindFirstObjectWithHypertag<Transform>(targetTag);
        }

        Vector3 GetTargetPos()
        {
            return target.position + targetOffset;
        }

        private void OnDrawGizmosSelected()
        {
            if (displayFocusPoint)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(focusPoint, displayFocusPointRadius);
                Gizmos.DrawLine(transform.position, focusPoint);
            }
            if ((hasLimits) && (cameraLimits != null))
            {
                Gizmos.color = Color.cyan;
                Gizmos.matrix = cameraLimits.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(cameraLimits.center, cameraLimits.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
