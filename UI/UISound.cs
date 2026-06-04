using UC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISound : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [SerializeField] private SoundDef clickSound;
    [SerializeField] private SoundDef hoverSound;

    Selectable selectable;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!selectable.interactable) return;
        clickSound?.Play();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!selectable.interactable) return;
        hoverSound?.Play();
    }
}
