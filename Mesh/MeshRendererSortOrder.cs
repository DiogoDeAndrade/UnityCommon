using NaughtyAttributes;
using UnityEngine;

public class MeshRendererSortOrder : MonoBehaviour
{
    [SerializeField, SortingLayer] private int sortingLayer;
    [SerializeField] private int orderInLayer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Set();
    }

    private void OnValidate()
    {
        Set();
    }

    [Button("Set")]
    void Set()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer)
        {
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = orderInLayer;
        }
    }
}
