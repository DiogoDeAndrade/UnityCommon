using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIRaycastDebugger : MonoBehaviour
{
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

        _eventData.position = Input.mousePosition;
        _results.Clear();
        EventSystem.current.RaycastAll(_eventData, _results);

        if (_results.Count > 0)
        {
            // Topmost hit
            Debug.Log("Top UI under cursor: " + _results[0].gameObject.name);

            // Uncomment if you want the full stack:
            // foreach (var r in _results)
            //     Debug.Log(" - " + r.gameObject.name);
        }
    }
}
