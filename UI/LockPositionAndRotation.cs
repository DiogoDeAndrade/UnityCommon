using UnityEngine;
using NaughtyAttributes;

public class LockPositionAndRotation : MonoBehaviour
{
    public enum RotationBehaviour { None, PreserveInitial, Zero };
    public enum PositionBehaviour { None, RelativeTo, ParentPosition };

    public RotationBehaviour    rotationBehaviour = RotationBehaviour.PreserveInitial;
    public PositionBehaviour    positionBehaviour = PositionBehaviour.None;

    [ShowIf(nameof(needRelative))]
    public Transform    relativeTransform;
    [ShowIf(nameof(needOffset))]
    public Vector3      offset;

    bool needRelative => positionBehaviour == PositionBehaviour.RelativeTo;
    bool needOffset => positionBehaviour != PositionBehaviour.None;

    // Keep vars
    Quaternion initialRotation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        initialRotation = transform.rotation;

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
