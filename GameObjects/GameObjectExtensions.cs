using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameObjectExtensions
{
    public static void DeleteAllChildren(this GameObject go)
    {
        List<Transform> toDestroy = new List<Transform>();

        foreach (Transform t in go.transform)
        {
            toDestroy.Add(t);
        }

        foreach (var t in toDestroy)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                GameObject.Destroy(t.gameObject);
            }
            else
            {
                GameObject.DestroyImmediate(t.gameObject);
            }
#else
            GameObject.Destroy(t.gameObject);
#endif
        }
    }
}

