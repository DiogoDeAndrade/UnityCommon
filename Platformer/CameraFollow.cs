using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class CameraFollow : MonoBehaviour
{
    public enum Mode { SimpleFeedbackLoop, Box };

    public Mode         mode = Mode.SimpleFeedbackLoop;
    public Transform    targetObject;
    [ShowIf("NeedFollowSpeed")]
    public float        followSpeed = 0.9f;
    [ShowIf("NeedBox")]
    public Rect         rect = new Rect(-100.0f, -100.0f, 200.0f, 200.0f);

    bool NeedFollowSpeed() { return mode == Mode.SimpleFeedbackLoop; }
    bool NeedBox() { return mode == Mode.Box; }

    void Start()
    {
        if (mode == Mode.Box)
        {
            float currentZ = transform.position.z;
            Vector3 targetPos = targetObject.transform.position;
            transform.position = new Vector3(targetPos.x, targetPos.y, currentZ);
        }
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
                case Mode.Box:
                    FixedUpdate_Box();
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

    void FixedUpdate_Box()
    {
        float   currentZ = transform.position.z;
        Vector3 targetPos = targetObject.transform.position;
        Rect    r = rect;
        r.position += transform.position.xy();

        if (targetPos.x > r.xMax) r.position += new Vector2(targetPos.x - r.xMax, 0);
        if (targetPos.x < r.xMin) r.position += new Vector2(targetPos.x - r.xMin, 0);
        if (targetPos.y < r.yMin) r.position += new Vector2(0, targetPos.y - r.yMin);
        if (targetPos.y > r.yMax) r.position += new Vector2(0, targetPos.y - r.yMax);

        transform.position = new Vector3(r.center.x, r.center.y, currentZ);
    }

    public Vector3 GetTargetPos()
    {
        if (targetObject == null) return transform.position;

        return targetObject.position;
    }

    private void OnDrawGizmos()
    {
        if (targetObject)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(targetObject.position, 2.0f);
        }

        if (mode == Mode.Box)
        {
            Rect r = rect;
            r.position += transform.position.xy();

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin));
            Gizmos.DrawLine(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax));
            Gizmos.DrawLine(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax));
            Gizmos.DrawLine(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin));
        }
    }
}
