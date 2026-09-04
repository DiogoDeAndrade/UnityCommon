using UnityEngine;
using NaughtyAttributes;

namespace UC
{

    public class RadialCameraInput : MonoBehaviour
    {
        public bool mouseControl;
        [ShowIf("mouseControl")]
        public int requiredMouseButton = -1;
        [ShowIf("mouseControl")]
        public float minHoldTime;
        [ShowIf("mouseControl")]
        public float mouseAxisX;
        [ShowIf("mouseControl")]
        public float mouseAxisY;
        [ShowIf("mouseControl")]
        public float mouseZoomSpeed;
        [ShowIf("needZoomLimits")]
        public Vector2 zoomLimits = new Vector2(0, 100);

        RadialCamera radialCamera;

        bool needZoomLimits => (mouseControl) && (Mathf.Abs(mouseZoomSpeed) > 0);
        float buttonDown;

        void Start()
        {
            radialCamera = GetComponent<RadialCamera>();
        }

        void Update()
        {
            if (radialCamera == null) return;

            bool dragging = false;
            if (requiredMouseButton == -1) dragging = true;
            else
            {
                if (InputControl.GetMouseButtonDown(requiredMouseButton)) buttonDown = Time.time;

                if ((requiredMouseButton >= 0) && (InputControl.GetMouseButton(requiredMouseButton)))
                {
                    if ((Time.time - buttonDown) >= minHoldTime)
                    {
                        dragging = true;
                    }
                }
            }

            if (dragging)
            {
                if (Mathf.Abs(mouseAxisY) > 0)
                {
                    float incY = -InputControl.GetAxis("Mouse X") * mouseAxisY;
                    radialCamera.angleY += incY; while (radialCamera.angleY > 360) radialCamera.angleY -= 360;
                }

                if (Mathf.Abs(mouseAxisX) > 0)
                {
                    float incX = InputControl.GetAxis("Mouse Y") * mouseAxisX;
                    radialCamera.angleX = Mathf.Clamp(radialCamera.angleX + incX, 0, 90);
                }
            }

            if (mouseZoomSpeed > 0)
            {
                float incZoom = -InputControl.GetMouseScrollDelta().y * mouseZoomSpeed;
                radialCamera.distance = Mathf.Clamp(radialCamera.distance + incZoom, zoomLimits.x, zoomLimits.y);
            }
        }
    }
}