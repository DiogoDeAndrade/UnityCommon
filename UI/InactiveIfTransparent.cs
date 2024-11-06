using UnityEngine;

public class InactiveIfTransparent : MonoBehaviour
{
    CanvasGroup canvasGroup;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    // Update is called once per frame
    void Update()
    {
        canvasGroup.interactable = (canvasGroup.alpha >= 1.0f);
    }
}
