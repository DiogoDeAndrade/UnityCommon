using UnityEngine;

public class PointOfInterest : MonoBehaviour
{
    [SerializeField, Range(0, 2)]
    int _interestLevelCategory = 2;
    [SerializeField]
    float _priority = 0.0f;

    public int interestLevel => _interestLevelCategory;
    public float priority => _priority;
}
