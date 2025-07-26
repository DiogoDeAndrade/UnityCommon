using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    public class BlobShadow : MonoBehaviour
    {
        private enum Mode { MoveTransform };

        [SerializeField]
        private Mode mode;
        [SerializeField]
        private MeshRenderer meshRenderer;
        [SerializeField]
        private string shadowIntensityName = "_ShadowIntensity";
        [SerializeField]
        private bool raycast = false;
        [SerializeField, ShowIf(nameof(raycast))]
        private float offsetBeforeRaycast = 1.0f;
        [SerializeField, ShowIf(nameof(raycast))]
        private float maxDist = 2.0f;
        [SerializeField, ShowIf(nameof(raycast))]
        private LayerMask groundLayers;
        [SerializeField, ShowIf(nameof(raycast))]
        private bool rotateToGround;
        [SerializeField, ShowIf(nameof(raycast))]
        private float blobOffset = 0.1f;

        private Material material;
        private int shadowIntensityHash;

        void Start()
        {
            if ((meshRenderer.gameObject == gameObject) && (rotateToGround))
            {
                Debug.LogWarning("It is not advisable to have the meshRenderer on the same object as the BlobShadow controller, since the raycast is cast from it and with the rotation we'll move the position in unexpected ways which may make the shadow run away.");
            }

            if (meshRenderer)
            {
                material = new Material(meshRenderer.material);
                meshRenderer.material = material;

                shadowIntensityHash = Shader.PropertyToID(shadowIntensityName);

                if (material.shader.name != "Unity Common/Blob Shadow")
                {
                    Debug.LogWarning("Only Unity Common/Blob Shadow shader supported!");
                }
            }
        }

        void Update()
        {
            switch (mode)
            {
                case Mode.MoveTransform:
                    UpdateMoveTransform();
                    break;
                default:
                    break;
            }

            var vLight = VirtualLight.GetLight(transform.position);
            if (vLight == null)
            {
                // Disable intensity
                material.SetFloat(shadowIntensityHash, 0.0f);
            }
            else
            {
                material.SetFloat(shadowIntensityHash, vLight.GetIntensity(transform.position));
            }
        }

        private void UpdateMoveTransform()
        {
            if (raycast)
            {
                var up = (rotateToGround) ? (transform.up) : (Vector3.up);

                if (Physics.Raycast(transform.position + up * offsetBeforeRaycast, -up, out RaycastHit hit, maxDist, groundLayers))
                {
                    if (rotateToGround)
                    {
                        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;

                        if (forward.sqrMagnitude < 0.001f)
                            forward = Vector3.ProjectOnPlane(transform.right, hit.normal).normalized;

                        Quaternion rot = Quaternion.LookRotation(forward, hit.normal);
                        meshRenderer.transform.SetPositionAndRotation(hit.point + hit.normal * blobOffset, rot);
                    }
                    else
                    {
                        meshRenderer.transform.position = hit.point + Vector3.up * blobOffset;
                    }
                }
            }
        }
    }
}