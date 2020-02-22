using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public enum Mode { SimpleFeedbackLoop };

    public Mode      mode = Mode.SimpleFeedbackLoop;
    public Transform targetObject;
    public float     followSpeed = 0.9f;

    void Start()
    {
    }

    void FixedUpdate()
    {
        if (targetObject)
        {
            switch (mode)
            {
                case Mode.SimpleFeedbackLoop:
                    FixedUpdate_SimpleFeedbackLoop();
                    break;
            }
        }
    }

    void FixedUpdate_SimpleFeedbackLoop()
    { 
        float currentZ = transform.position.z;

        Vector3 err = targetObject.transform.position - transform.position;

        Vector3 newPos = transform.position + err * followSpeed;
        newPos.z = currentZ;

        transform.position = newPos;
    }
}
