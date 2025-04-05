using UnityEngine;

namespace UC
{

    public class RadialCamera : MonoBehaviour
    {
        public Transform dynamicTarget;
        public Vector3 targetPos;
        public float angleX = 45.0f;
        public float angleY = 0.0f;
        public float distance = 20.0f;

        void Start()
        {

        }

        void Update()
        {
            if (dynamicTarget)
            {
                targetPos = dynamicTarget.position;
            }

            float cy = Mathf.Cos(Mathf.Deg2Rad * (angleY + 90));
            float sy = Mathf.Sin(Mathf.Deg2Rad * (angleY + 90));
            float cx = Mathf.Cos(-Mathf.Deg2Rad * angleX);
            float sx = Mathf.Sin(-Mathf.Deg2Rad * angleX);

            Vector3 cameraDir = new Vector3(cy * cx, sx, sy * cx);
            Vector3 cameraPos = targetPos - distance * cameraDir;

            transform.position = cameraPos;
            transform.rotation = Quaternion.LookRotation(cameraDir.normalized, Vector3.up);
        }
    }
}