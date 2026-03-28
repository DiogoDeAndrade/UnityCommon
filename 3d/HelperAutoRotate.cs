using UnityEngine;

public class HelperAutoRotate : MonoBehaviour
{
    [SerializeField] private float      speed = 360.0f;
    [SerializeField] private Vector3    axis = Vector3.up;
    [SerializeField] private float      startAngle = 0.0f;
    [SerializeField] private bool       startWithCurrentRotation;

    float       angle = 0.0f;
    Quaternion  rotationOffset = Quaternion.identity;

    void Start()
    {
        angle = startAngle;
        if (startWithCurrentRotation) rotationOffset = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        angle += Time.deltaTime * speed;
        if (angle > 360.0f) angle -= 360.0f;
        else if (angle < 0.0f) angle += 360.0f;

        transform.rotation = Quaternion.AngleAxis(angle, axis) * rotationOffset;
    }
}
