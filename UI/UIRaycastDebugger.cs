using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UIRaycastDebugger : MonoBehaviour
{
    [SerializeField] private bool useNewInputSystem = true;
    [SerializeField] private bool fullStack = false;

    List<RaycastResult> _results = new List<RaycastResult>();
    PointerEventData _eventData;

    void Awake()
    {
        if (EventSystem.current != null)
            _eventData = new PointerEventData(EventSystem.current);
    }

    void Update()
    {
        if (EventSystem.current == null || _eventData == null)
            return;

        if (useNewInputSystem)
            _eventData.position = Mouse.current.position.ReadValue();
        else
            _eventData.position = Input.mousePosition;
        _results.Clear();
        EventSystem.current.RaycastAll(_eventData, _results);

        if (_results.Count > 0)
        {
            // Topmost hit
            Debug.Log("Top UI under cursor: " + _results[0].gameObject.name);

            if (fullStack)
            {
                foreach (var r in _results)
                    Debug.Log(" - " + r.gameObject.name);
            }
        }
    }
}
