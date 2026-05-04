using TMPro;
using UC;
using UnityEngine;

public class UIListItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textElement;

    private int             _index;
    private BaseUIControl   control;
    private UIList          _ownerList;

    public int index
    {
        get => _index;
        set
        {
            _index = value;
        }
    }

    public UIList ownerList
    {
        get => _ownerList;
        set
        {
            _ownerList = value;
        }
    }

    private void Start()
    {
        // Register for selection
        control = GetComponent<BaseUIControl>();
        if (control)
        {
            control.onInteract += Control_onInteract;
        }
    }

    private void Control_onInteract(BaseUIControl control)
    {
        _ownerList.SelectItem(this);
    }

    public void SetText(string text)
    {
        textElement.text = text;
    }
}
