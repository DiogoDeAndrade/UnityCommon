using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
public class UIDisableIfItemDisabled : MonoBehaviour
{
    [SerializeField] private GameObject srcObject;
    
    LayoutElement   layoutElement;
    Graphic         gfx;

    void Start()
    {
        layoutElement = GetComponent<LayoutElement>();
        gfx = GetComponent<Graphic>();
    }

    void Update()
    {
        if (layoutElement.ignoreLayout == srcObject.activeSelf)
        {
            if (layoutElement) layoutElement.ignoreLayout = !srcObject.activeSelf;
            if (gfx) gfx.enabled = srcObject.activeSelf;
            if (transform.parent) LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
        }
    }
}
