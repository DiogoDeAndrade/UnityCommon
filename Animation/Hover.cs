using UnityEngine;

public class Hover : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3   direction = Vector3.up;
    [SerializeField] private float     offset;
    [SerializeField] private float     amplitude;
    [SerializeField] private float     frequency;
    [SerializeField] private float     baseOffset = 0.0f;

    Vector3 basePos;
    float   elapsedTime;

    void Start()
    {
        if (target == null) target = this.transform;

        basePos = target.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        elapsedTime += Time.deltaTime;

        target.localPosition =  basePos + direction * (offset + Mathf.Sin(elapsedTime * frequency * Mathf.Deg2Rad + baseOffset) * amplitude);
    }
}
