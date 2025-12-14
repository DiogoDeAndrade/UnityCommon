using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
public class UIDisableIfItemDisabled : MonoBehaviour
{
    [SerializeField] private GameObject srcObject;
    LayoutElement layoutElement;

    void Start()
    {
        layoutElement = GetComponent<LayoutElement>();
    }

    void Update()
    {
        if (layoutElement.ignoreLayout == srcObject.activeSelf)
        {
            layoutElement.ignoreLayout = !srcObject.activeSelf;
            if (transform.parent) LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
        }
    }
}
