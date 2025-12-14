using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
public class UIDisableIfChildrenDisabled : MonoBehaviour
{
    LayoutElement layoutElement;

    void Start()
    {
        layoutElement = GetComponent<LayoutElement>();
    }

    void Update()
    {
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                layoutElement.ignoreLayout = false;
                return;
            }
        }

        layoutElement.ignoreLayout = true;
    }
}
