using UnityEngine;
using NaughtyAttributes;

namespace UC
{

    public class LockPositionAndRotation : MonoBehaviour
    {
        public enum RotationBehaviour { None, PreserveInitial, Zero, FaceCamera, FaceCameraDir };
        public enum PositionBehaviour { None, RelativeTo, ParentPosition };

        public RotationBehaviour rotationBehaviour = RotationBehaviour.PreserveInitial;
        public PositionBehaviour positionBehaviour = PositionBehaviour.None;
        
        [SerializeField, ShowIf(nameof(needCamera))]
        private Hypertag cameraTag;

        [ShowIf(nameof(needRelative))]
        public Transform relativeTransform;
        [ShowIf(nameof(needOffset))]
        public Vector3 offset;

        bool needRelative => positionBehaviour == PositionBehaviour.RelativeTo;
        bool needOffset => positionBehaviour != PositionBehaviour.None;
        bool needCamera => (rotationBehaviour == RotationBehaviour.FaceCamera) || (rotationBehaviour == RotationBehaviour.FaceCameraDir);

        // Keep vars
        Quaternion  initialRotation;
        Camera      targetCamera;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            initialRotation = transform.rotation;

            if (needCamera)
            {
                targetCamera = cameraTag.FindFirst<Camera>();
            }

            RunRotationBehaviour();
        }

        private void LateUpdate()
        {
            RunRotationBehaviour();
            RunPositionBehaviour();
        }

        private void RunRotationBehaviour()
        {
            switch (rotationBehaviour)
            {
                case RotationBehaviour.None:
                    break;
                case RotationBehaviour.PreserveInitial:
                    transform.rotation = initialRotation;
                    break;
                case RotationBehaviour.Zero:
                    transform.rotation = Quaternion.identity;
                    break;
                case RotationBehaviour.FaceCamera:
                    if (targetCamera == null)
                        targetCamera = Camera.main;

                    if (targetCamera != null)
                    {
                        Vector3 toCamera = targetCamera.transform.position - transform.position;

                        if (toCamera.sqrMagnitude > 0.0001f)
                        {
                            transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
                        }
                    }
                    break;
                case RotationBehaviour.FaceCameraDir:
                    if (targetCamera == null)
                        targetCamera = Camera.main;

                    if (targetCamera != null)
                    {
                        transform.rotation = targetCamera.transform.rotation;
                    }
                    break;
                default:
                    break;
            }
        }

        void RunPositionBehaviour()
        {
            switch (positionBehaviour)
            {
                case PositionBehaviour.None:
                    break;
                case PositionBehaviour.RelativeTo:
                    transform.position = relativeTransform.position + offset;
                    break;
                case PositionBehaviour.ParentPosition:
                    transform.position = transform.parent.position + offset;
                    break;
                default:
                    break;
            }
        }
    }
}