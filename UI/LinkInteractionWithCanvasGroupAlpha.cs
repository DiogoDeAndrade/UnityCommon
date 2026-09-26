using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class LinkInteractionWithCanvasGroupAlpha : MonoBehaviour
{
    [SerializeField] private float disableThreshould = 0.25f;
    
    private CanvasGroup cg;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        if (cg == null)
        {
            Debug.LogWarning("No canvas group on object with LinkInteractionWithCanvasGroupAlpha!", this);
            enabled = false;
            return;
        }
        bool invisible = cg.alpha <= disableThreshould;
        cg.interactable = !invisible;
        cg.blocksRaycasts = !invisible;
    }
}
