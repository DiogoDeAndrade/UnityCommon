﻿using NaughtyAttributes;
using System.Linq;
using UnityEngine;

namespace UC
{

    public class CameraFollow : MonoBehaviour
    {
        public enum UpdateMode { Update, FixedUpdate, LateUpdate };
        public enum Mode { SimpleFeedbackLoop = 0, CameraTrap = 1, ExponentialDecay = 2 };
        public enum TagMode { Closest = 0, Furthest = 1, Average = 2 };

        [SerializeField] UpdateMode updateMode = UpdateMode.FixedUpdate;
        [SerializeField] Mode mode = Mode.SimpleFeedbackLoop;
        [SerializeField] Hypertag targetTag;
        [SerializeField] TagMode tagMode = TagMode.Closest;
        [SerializeField] bool allowZoom;
        [SerializeField, ShowIf(nameof(allowZoom))] float zoomMargin = 1.1f;
        [SerializeField, ShowIf(nameof(allowZoom))] float zoomSpeed = 1.0f;
        [SerializeField] Vector2 minMaxSize = new Vector2(180.0f, 360.0f);
        [SerializeField, ShowIf(nameof(needObject))] Transform targetObject;
        [SerializeField, ShowIf(nameof(needFollowSpeed))] float followSpeed = 0.9f;
        [SerializeField, ShowIf(nameof(needRect))] Rect rect = new Rect(-100.0f, -100.0f, 200.0f, 200.0f);
        [SerializeField] BoxCollider2D cameraLimits;
        [SerializeField, Header("Debug")]
        private bool displayTargetPos;
        [SerializeField, ShowIf(nameof(displayTargetPos))]
        private float displayTargetPosRadius = 0.5f;

        private Camera mainCamera;
        private Bounds allObjectsBound;

        bool needObject => targetTag == null;
        bool needFollowSpeed => (mode == Mode.SimpleFeedbackLoop) || (mode == Mode.ExponentialDecay);
        bool needRect => (mode == Mode.CameraTrap);

        void Start()
        {
            mainCamera = GetComponent<Camera>();

            if (mode == Mode.CameraTrap)
            {
                float currentZ = transform.position.z;
                Vector3 targetPos = GetTargetPos();
                transform.position = new Vector3(targetPos.x, targetPos.y, currentZ);

                CheckBounds();
            }
        }

        void Update()
        {
            if (updateMode == UpdateMode.Update) Run_Update();
        }
        void FixedUpdate()
        {
            if (updateMode == UpdateMode.FixedUpdate) Run_Update();
        }
        void LateUpdate()
        {
            if (updateMode == UpdateMode.LateUpdate) Run_Update();
        }

        void Run_Update()
        {
            switch (mode)
            {
                case Mode.SimpleFeedbackLoop:
                    FixedUpdate_SimpleFeedbackLoop();
                    break;
                case Mode.CameraTrap:
                    FixedUpdate_Box();
                    break;
                case Mode.ExponentialDecay:
                    FixedUpdate_ExponentialDecay();
                    break;
            }
        }

        void FixedUpdate_SimpleFeedbackLoop()
        {
            float currentZ = transform.position.z;

            Vector3 err = GetTargetPos() - transform.position;

            Vector3 newPos = transform.position + err * followSpeed;
            newPos.z = currentZ;

            transform.position = newPos;

            RunZoom();
            CheckBounds();
        }
        void FixedUpdate_ExponentialDecay()
        {
            // Nice explanation of this: https://www.youtube.com/watch?v=LSNQuFEDOyQ&ab_channel=FreyaHolm%C3%A9r
            Vector3 targetPos = GetTargetPos();

            float factor = Mathf.Clamp01(Mathf.Pow((1.0f - followSpeed), Time.fixedDeltaTime));
            Vector3 newPos = targetPos + (transform.position - targetPos) * factor;
            newPos.z = transform.position.z;

            transform.position = newPos;

            RunZoom();
            CheckBounds();
        }

        void FixedUpdate_Box()
        {
            float currentZ = transform.position.z;
            Vector3 targetPos = GetTargetPos();
            Vector2 delta = transform.position;
            Rect r = rect;
            r.position += delta;

            if (targetPos.x > r.xMax) r.position += new Vector2(targetPos.x - r.xMax, 0);
            if (targetPos.x < r.xMin) r.position += new Vector2(targetPos.x - r.xMin, 0);
            if (targetPos.y < r.yMin) r.position += new Vector2(0, targetPos.y - r.yMin);
            if (targetPos.y > r.yMax) r.position += new Vector2(0, targetPos.y - r.yMax);

            transform.position = new Vector3(r.center.x, r.center.y, currentZ);

            RunZoom();
            CheckBounds();
        }

        void RunZoom()
        {
            if ((targetTag != null) && (tagMode == TagMode.Average) && (allowZoom))
            {
                Vector2 zoomLimits = minMaxSize;
                if (cameraLimits != null)
                {
                    float lim1 = cameraLimits.size.y * 0.5f;
                    float lim2 = cameraLimits.size.x * 0.5f / mainCamera.aspect;
                    float lim = Mathf.Min(lim1, lim2);

                    zoomLimits.y = Mathf.Min(lim, zoomLimits.y);
                }

                float height1 = Mathf.Clamp(allObjectsBound.extents.y + zoomMargin, zoomLimits.x, zoomLimits.y);
                float height2 = Mathf.Clamp(allObjectsBound.extents.x + zoomMargin, mainCamera.aspect * zoomLimits.x, mainCamera.aspect * zoomLimits.y) / mainCamera.aspect;

                float oldHeight = mainCamera.orthographicSize;
                float targetHeight = Mathf.Max(height1, height2);

                float newHeight = targetHeight + (oldHeight - targetHeight) * Mathf.Pow((1.0f - zoomSpeed), Time.fixedDeltaTime);

                mainCamera.orthographicSize = newHeight;
            }
        }

        void CheckBounds()
        {
            if (cameraLimits == null) return;

            Bounds r = cameraLimits.bounds;

            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = mainCamera.aspect * halfHeight;

            float xMin = transform.position.x - halfWidth;
            float xMax = transform.position.x + halfWidth;
            float yMin = transform.position.y - halfHeight;
            float yMax = transform.position.y + halfHeight;

            Vector3 position = transform.position;

            if (xMin <= r.min.x) position.x = r.min.x + halfWidth;
            else if (xMax >= r.max.x) position.x = r.max.x - halfWidth;
            if (yMin <= r.min.y) position.y = r.min.y + halfHeight;
            else if (yMax >= r.max.y) position.y = r.max.y - halfHeight;

            transform.position = position;
        }

        public Vector3 GetTargetPos()
        {
            CameraFollowTarget cft = null;

            if (targetObject != null)
            {
                cft = targetObject.GetComponent<CameraFollowTarget>();
                if (cft) return cft.followPos;

                return targetObject.transform.position;
            }
            else if (targetTag)
            {
                Vector3 selectedPosition = transform.position;

                var potentialTransforms = HypertaggedObject.Get<Transform>(targetTag).ToList();
                if (tagMode == TagMode.Closest)
                {
                    var minDist = float.MaxValue;
                    foreach (var obj in potentialTransforms)
                    {
                        var d = Vector3.Distance(obj.position, transform.position);
                        if (d < minDist)
                        {
                            minDist = d;
                            cft = obj.GetComponent<CameraFollowTarget>();
                            if (cft) selectedPosition = cft.followPos;
                            else selectedPosition = obj.position;
                        }
                    }
                }
                else if (tagMode == TagMode.Furthest)
                {
                    var maxDist = 0.0f;
                    foreach (var obj in potentialTransforms)
                    {
                        var d = Vector3.Distance(obj.position, transform.position);
                        if (d > maxDist)
                        {
                            maxDist = d;
                            cft = obj.GetComponent<CameraFollowTarget>();
                            if (cft) selectedPosition = cft.followPos;
                            else selectedPosition = obj.position;
                        }
                    }
                }
                else if (tagMode == TagMode.Average)
                {
                    if (potentialTransforms.Count > 0)
                    {
                        cft = potentialTransforms[0].GetComponent<CameraFollowTarget>();
                        if (cft) allObjectsBound = new Bounds(cft.followPos, Vector3.zero);
                        else allObjectsBound = new Bounds(potentialTransforms[0].position, Vector3.zero);
                        selectedPosition = Vector3.zero;
                        foreach (var obj in potentialTransforms)
                        {
                            var d = Vector3.Distance(obj.position, transform.position);
                            cft = obj.GetComponent<CameraFollowTarget>();
                            if (cft) selectedPosition += cft.followPos;
                            else selectedPosition += obj.position;
                            allObjectsBound.Encapsulate(obj.position);
                        }
                        selectedPosition /= potentialTransforms.Count;
                    }
                }

                return selectedPosition;
            }

            cft = GetComponent<CameraFollowTarget>();
            if (cft) return cft.followPos;

            return transform.position;
        }

        private void OnDrawGizmos()
        {
            if (displayTargetPos)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(GetTargetPos(), displayTargetPosRadius);
            }

            if (mode == Mode.CameraTrap)
            {
                Vector2 delta = transform.position;
                Rect r = rect;
                r.position += delta;

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin));
                Gizmos.DrawLine(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax));
                Gizmos.DrawLine(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax));
                Gizmos.DrawLine(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin));
            }

            if ((allowZoom) && (allObjectsBound.size.magnitude > 0))
            {
                Bounds extraBounds = allObjectsBound;
                extraBounds.extents *= zoomMargin;
                // Force the correct aspect ratio now
                float desiredAspectXOverY = (mainCamera) ? (mainCamera.aspect) : (16.0f / 9.0f);
                float desiredAspectYOverX = 1.0f / desiredAspectXOverY;
                float aspect = extraBounds.size.x / extraBounds.size.y;
                if (aspect > 1)
                {
                    extraBounds.size = new Vector3(extraBounds.size.x, extraBounds.size.x * desiredAspectYOverX, extraBounds.size.z);
                }
                else
                {
                    extraBounds.size = new Vector3(extraBounds.size.y * desiredAspectXOverY, extraBounds.size.y, extraBounds.size.z);
                }

                Gizmos.color = Color.yellow;
                DebugHelpers.DrawBox(extraBounds);
            }

            if (cameraLimits)
            {
                Bounds r = cameraLimits.bounds;

                Gizmos.color = Color.green;
                Gizmos.DrawLine(new Vector2(r.min.x, r.min.y), new Vector2(r.max.x, r.min.y));
                Gizmos.DrawLine(new Vector2(r.max.x, r.min.y), new Vector2(r.max.x, r.max.y));
                Gizmos.DrawLine(new Vector2(r.max.x, r.max.y), new Vector2(r.min.x, r.max.y));
                Gizmos.DrawLine(new Vector2(r.min.x, r.max.y), new Vector2(r.min.x, r.min.y));
            }
        }

    }
}