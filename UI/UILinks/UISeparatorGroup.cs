using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
public class UISeparatorGroup : MonoBehaviour
{
    [SerializeField] private GameObject separatorObject;

    LayoutElement layoutElement;

    void Start()
    {
        layoutElement = GetComponent<LayoutElement>();
    }

    void Update()
    {
        int activeElements = 0;

        foreach (Transform child in transform)
        {
            if (child.gameObject == separatorObject) continue;

            if (!child.gameObject.activeSelf) continue;

            var gfx = child.GetComponent<Graphic>();
            if ((gfx) && (!gfx.enabled)) continue;

            var layoutElement = child.GetComponent<LayoutElement>();
            if ((layoutElement) && (layoutElement.ignoreLayout)) continue;

            activeElements++;
        }

        separatorObject.gameObject.SetActive(activeElements > 0);
        layoutElement.ignoreLayout = activeElements == 0;
    }
}
