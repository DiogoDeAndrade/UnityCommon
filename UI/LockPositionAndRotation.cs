using UnityEngine;
using NaughtyAttributes;

public class LockPositionAndRotation : MonoBehaviour
{
    public bool respectRotation = false;
    public bool preserveRelativePositon = false;
    [ShowIf("preserveRelativePositon")]
    public Transform relativeTransform;

    // Keep vars
    Quaternion initialRotation;
    Vector3 deltaPos;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initialRotation = transform.rotation;

        if (preserveRelativePositon)
        {
            if (relativeTransform)
            {
                deltaPos = transform.position - relativeTransform.transform.position;
            }
            else preserveRelativePositon = false;
        }

    }

    private void LateUpdate()
    {
        if (!respectRotation)
        {
            transform.rotation = initialRotation;
        }
        if (preserveRelativePositon)
        {
            transform.position = relativeTransform.position + deltaPos;
        }
    }
}
