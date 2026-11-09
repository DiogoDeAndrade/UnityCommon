using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UC
{

    // Hand driven camera for an orthographic 2d view: pans with the cursor on the screen edges and/or a directional
    // input, zooms (orthographic size) with the scroll wheel and/or a zoom input, and keeps the view inside a box.
    [RequireComponent(typeof(Camera))]
    public class RTSCameraController2d : MonoBehaviour
    {
        [SerializeField, Tooltip("Top speed, in world units per second (at the reference zoom, if the speed scales with zoom). Diagonals are no faster than straight lines")]
        private float           cameraSpeed = 200.0f;
        [SerializeField, Tooltip("Ramp up to the top speed and glide to a stop, instead of moving at a fixed speed")]
        private bool            useAcceleration;
        [SerializeField, ShowIf(nameof(useAcceleration)), Tooltip("In world units per second squared")]
        private float           acceleration = 100.0f;
        [SerializeField, ShowIf(nameof(useAcceleration)), Range(0.0f, 1.0f), Tooltip("Fraction of the speed kept every 1/60th of a second while nothing pushes the camera that way (letting go, turning, reversing). Lower stops sooner, 1 never stops")]
        private float           damping = 0.9f;
        [SerializeField, Min(0.0f), Tooltip("0 disables zoom. Zoom is a factor, so it feels the same at any zoom level: a held zoom input multiplies the size by e (2.7) every second at 1, a wheel notch is worth a tenth of a second of that")]
        private float           zoomSpeed = 1.0f;
        [SerializeField, ShowIf(nameof(hasZoom)), Tooltip("Orthographic size range (half the view height, in world units). Zooming out also stops once the view fills the camera bounds")]
        private Vector2         minMaxZoom = new Vector2(50.0f, 400.0f);
        [SerializeField, ShowIf(nameof(hasZoom)), Tooltip("Move faster when zoomed out and slower when zoomed in, in proportion to the zoom, so the view crosses the screen at the same rate at any zoom")]
        private bool            scaleSpeedWithZoom;
        [SerializeField, ShowIf(EConditionOperator.And, nameof(hasZoom), nameof(scaleSpeedWithZoom)), Min(1.0f), Tooltip("Orthographic size at which the camera moves at exactly the camera speed (at twice this size it moves twice as fast)")]
        private float           speedReferenceZoom = 200.0f;
        [SerializeField, Tooltip("Pan with the cursor on the screen edges, zoom with the scroll wheel")]
        private bool            allowMouseControl;
        [SerializeField, ShowIf(nameof(allowMouseControl)), Range(0.0f, 0.5f), Tooltip("Size of the edge band that pans the camera, as a fraction of the screen width (left/right) and height (top/bottom)")]
        private float           screenMargin = 0.05f;
        [SerializeField, ShowIf(nameof(allowMouseControl)), Min(0.0f), Tooltip("Edge panning moves at the camera speed times this")]
        private float           mouseSpeedMultiplier = 1.0f;
        [SerializeField]
        private bool            allowButtonControl;
        [SerializeField, ShowIf(nameof(allowButtonControl))]
        private PlayerInput     playerInput;
        [SerializeField, ShowIf(nameof(allowButtonControl)), InputPlayer(nameof(playerInput)), AllowInput(AllowInput.NewInput | AllowInput.NoInput), Tooltip("A Vector2 action, e.g. a WASD/arrows 2D composite or a stick")]
        private InputControl    directionalInput;
        [SerializeField, ShowIf(nameof(allowButtonControl)), Min(0.0f), Tooltip("The directional input moves at the camera speed times this")]
        private float           buttonSpeedMultiplier = 1.0f;
        [SerializeField, ShowIf(EConditionOperator.And, nameof(allowButtonControl), nameof(hasZoom))]
        private bool            allowButtonZoomControl;
        [SerializeField, ShowIf(EConditionOperator.And, nameof(allowButtonControl), nameof(allowButtonZoomControl), nameof(hasZoom)), InputPlayer(nameof(playerInput)), Tooltip("Held zoom input, positive zooms in, e.g. a 1D axis on two keys")]
        private InputControl    scrollControl;
        [SerializeField, Tooltip("The whole view stays inside this box. Only its shape is used, so the collider can be disabled")]
        private BoxCollider2D   cameraBounds;
        [SerializeField, Tooltip("Move and zoom on unscaled time, so the camera can still be moved while the game is paused (timeScale 0)")]
        private bool            useUnscaledTime = true;
        [SerializeField, Tooltip("Snap the camera to the pixel grid, so sub pixel positions don't make the sprites shimmer")]
        private bool            pixelSnap;
        [SerializeField, ShowIf(nameof(pixelSnap)), Min(0.0f), Tooltip("Size of a pixel in world units (1 for sprites imported at 1 pixel per unit)")]
        private float           pixelSize = 1.0f;

        bool hasZoom => zoomSpeed > 0.0f;

        // A wheel notch counts as this many seconds of held zoom input
        const float WheelNotchTime = 0.1f;
        // How quickly the orthographic size catches up with the zoom target (exponential, per second)
        const float ZoomSmoothing = 12.0f;
        // A damped camera slower than this (world units per second) is stopped outright
        const float StopSpeed = 1.0f;

        Camera  cam;
        Vector2 position;           // Unsnapped, the transform gets the snapped version
        Vector2 writtenPosition;    // What was last written to the transform, to notice when something else moves it
        Vector2 velocity;
        float   targetSize;

        void Awake()
        {
            cam = GetComponent<Camera>();

            if (allowButtonControl)
            {
                directionalInput.playerInput = playerInput;
                scrollControl.playerInput = playerInput;
            }
        }

        void Start()
        {
            position = transform.position;

            targetSize = cam.orthographicSize;
            if (hasZoom)
            {
                targetSize = ClampZoom(targetSize);
                cam.orthographicSize = targetSize;
            }

            ApplyPosition();
        }

        void Update()
        {
            float dt = (useUnscaledTime) ? (Time.unscaledDeltaTime) : (Time.deltaTime);

            // Something else moved the camera (a cut, centring on something): carry on from there
            Vector2 current = transform.position;
            if (current != writtenPosition) position = current;

            UpdateZoom(dt);
            UpdateMovement(dt);
            ApplyPosition();
        }

        // --- Movement -----------------------------------------------------------

        void UpdateMovement(float dt)
        {
            // Each source is capped at length 1 on its own, so two edges or two keys are no faster on a diagonal than
            // on a straight line (an analog stick that's only partly pushed keeps its partial speed), then scaled by
            // its own multiplier
            Vector2 mouseInput = (allowMouseControl) ? (Vector2.ClampMagnitude(GetEdgeInput(), 1.0f) * mouseSpeedMultiplier) : (Vector2.zero);
            Vector2 buttonInput = (allowButtonControl) ? (Vector2.ClampMagnitude(directionalInput.GetAxis2(), 1.0f) * buttonSpeedMultiplier) : (Vector2.zero);

            // Both at once go no faster than the faster of the two alone
            Vector2 input = Vector2.ClampMagnitude(mouseInput + buttonInput, Mathf.Max(mouseInput.magnitude, buttonInput.magnitude));

            velocity = (useAcceleration) ? (Accelerate(velocity, input, dt)) : (input * cameraSpeed);

            // The velocity is kept as at the reference zoom and scaled only here, so the zoom scales the top speed and
            // the acceleration together (reaching full speed takes as long at any zoom)
            position += velocity * (GetZoomSpeedScale() * dt);
        }

        float GetZoomSpeedScale()
        {
            if ((!hasZoom) || (!scaleSpeedWithZoom)) return 1.0f;

            return cam.orthographicSize / Mathf.Max(speedReferenceZoom, 1.0f);
        }

        Vector2 Accelerate(Vector2 v, Vector2 input, float dt)
        {
            // Damping is the fraction kept per 1/60th of a second, made framerate independent
            float keep = Mathf.Pow(damping, dt * 60.0f);

            float inputMagnitude = input.magnitude;
            if (inputMagnitude < 1e-4f)
            {
                v *= keep;
                return (v.sqrMagnitude < StopSpeed * StopSpeed) ? (Vector2.zero) : (v);
            }

            // Split the velocity into the part going where the input points and the rest. The rest is damped instead of
            // left for the acceleration to undo, so turning doesn't drift sideways and reversing doesn't take ages
            Vector2 dir = input / inputMagnitude;
            float   along = Vector2.Dot(v, dir);
            Vector2 across = (v - dir * along) * keep;
            if (along < 0.0f) along *= keep;

            float targetSpeed = inputMagnitude * cameraSpeed;
            along = Mathf.MoveTowards(along, targetSpeed, acceleration * dt);

            // While the sideways part dies off, never faster than it was or than it's heading for (switching from a
            // faster source to a slower one eases down instead of cutting the speed)
            return Vector2.ClampMagnitude(dir * along + across, Mathf.Max(v.magnitude, targetSpeed));
        }

        // -1/0/+1 per axis, depending on which edge bands the cursor is in
        Vector2 GetEdgeInput()
        {
            // When the window loses focus (clicking the inspector, another app) the cursor position freezes where it
            // was, which is usually right inside an edge band, and the camera would keep panning on its own
            if (!Application.isFocused) return Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            // No mouse reads as (0,0), which is the bottom left corner
            if (Mouse.current == null) return Vector2.zero;
#endif

            Vector2 mouse = InputControl.GetMousePixelPosition();
            if ((mouse.x < 0.0f) || (mouse.y < 0.0f) || (mouse.x > Screen.width) || (mouse.y > Screen.height)) return Vector2.zero;

            float marginX = screenMargin * Screen.width;
            float marginY = screenMargin * Screen.height;

            Vector2 input = Vector2.zero;

            if (mouse.x <= marginX) input.x = -1.0f;
            else if (mouse.x >= Screen.width - marginX) input.x = 1.0f;

            if (mouse.y <= marginY) input.y = -1.0f;
            else if (mouse.y >= Screen.height - marginY) input.y = 1.0f;

            return input;
        }

        // --- Zoom ---------------------------------------------------------------

        void UpdateZoom(float dt)
        {
            if (!hasZoom) return;

            // Positive zooms in, in seconds of held input
            float zoomInput = 0.0f;
            if (allowMouseControl) zoomInput += InputControl.GetMouseScrollDelta().y * WheelNotchTime;
            if ((allowButtonControl) && (allowButtonZoomControl)) zoomInput += scrollControl.GetAxis() * dt;

            if (zoomInput != 0.0f) targetSize *= Mathf.Exp(-zoomInput * zoomSpeed);

            // Every frame, not just on input: the bounds limit how far out it can go
            targetSize = ClampZoom(targetSize);

            float size = Mathf.Lerp(cam.orthographicSize, targetSize, 1.0f - Mathf.Exp(-ZoomSmoothing * dt));
            if (Mathf.Abs(size - targetSize) < 0.01f) size = targetSize;
            cam.orthographicSize = size;
        }

        float ClampZoom(float size)
        {
            float maxSize = minMaxZoom.y;

            // No further out than the bounds can fill, or the view would show past them
            if (TryGetBounds(out Rect bounds))
            {
                maxSize = Mathf.Min(maxSize, bounds.height * 0.5f, bounds.width * 0.5f / cam.aspect);
            }

            return Mathf.Clamp(size, minMaxZoom.x, Mathf.Max(minMaxZoom.x, maxSize));
        }

        // --- Position and bounds ------------------------------------------------

        // Keeps the camera inside the bounds and writes it to the transform, snapped to the pixel grid if asked
        void ApplyPosition()
        {
            // Range the camera centre can be in so the whole view stays inside the bounds (unbounded without any)
            Vector2 min = Vector2.negativeInfinity;
            Vector2 max = Vector2.positiveInfinity;

            if (TryGetBounds(out Rect bounds))
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;

                min = new Vector2(bounds.xMin + halfWidth, bounds.yMin + halfHeight);
                max = new Vector2(bounds.xMax - halfWidth, bounds.yMax - halfHeight);

                float x = ClampAxis(position.x, min.x, max.x);
                float y = ClampAxis(position.y, min.y, max.y);

                // Nothing left pushing into the wall, so the camera comes off it as soon as the input says so, instead
                // of first burning off speed that wasn't moving it anywhere
                if (x != position.x) velocity.x = 0.0f;
                if (y != position.y) velocity.y = 0.0f;

                position = new Vector2(x, y);
            }

            Vector2 p = position;
            if ((pixelSnap) && (pixelSize > 0.0f))
            {
                p = new Vector2(SnapAxis(p.x, min.x, max.x), SnapAxis(p.y, min.y, max.y));
            }

            transform.position = new Vector3(p.x, p.y, transform.position.z);
            writtenPosition = p;
        }

        // Nearest pixel, but never past the edge of the range, or a sliver past the bounds would show
        float SnapAxis(float value, float min, float max)
        {
            float snapped = Mathf.Round(value / pixelSize) * pixelSize;

            if (min <= max)
            {
                if (snapped < min) snapped += pixelSize;
                else if (snapped > max) snapped -= pixelSize;
            }

            return snapped;
        }

        // On an axis where the view is bigger than the box (zoom disabled, or the minimum zoom is too far out), centre
        // on the box
        static float ClampAxis(float value, float min, float max)
        {
            return (min > max) ? ((min + max) * 0.5f) : (Mathf.Clamp(value, min, max));
        }

        // World space rect of the box, worked out from its shape instead of collider.bounds, which is empty while the
        // collider is disabled. A rotated box gives its axis aligned bounds
        bool TryGetBounds(out Rect rect)
        {
            if (cameraBounds == null)
            {
                rect = default;
                return false;
            }

            Transform t = cameraBounds.transform;
            Vector2   offset = cameraBounds.offset;
            Vector2   half = cameraBounds.size * 0.5f;

            Vector2 p0 = t.TransformPoint(offset + new Vector2(-half.x, -half.y));
            Vector2 p1 = t.TransformPoint(offset + new Vector2( half.x, -half.y));
            Vector2 p2 = t.TransformPoint(offset + new Vector2(-half.x,  half.y));
            Vector2 p3 = t.TransformPoint(offset + new Vector2( half.x,  half.y));

            Vector2 min = Vector2.Min(Vector2.Min(p0, p1), Vector2.Min(p2, p3));
            Vector2 max = Vector2.Max(Vector2.Max(p0, p1), Vector2.Max(p2, p3));

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }
    }
}
