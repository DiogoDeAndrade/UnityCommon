using UnityEngine;

public class AutoSetCursor : MonoBehaviour
{
    [SerializeField] private bool visibleOnStart;

    void Start()
    {
        Cursor.visible = visibleOnStart;
        Destroy(this);
    }
}
