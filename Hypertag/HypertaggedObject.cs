using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class HypertaggedObject : MonoBehaviour
{
    public Hypertag hypertag;

    void Awake()
    {
        AddToHypertagList(this);
    }

    private void OnDestroy()
    {
        RemoveFromHypertagList(this);
    }

    public bool HasAnyHypertag(List<Hypertag> hypertags)
    {
        return hypertags.IndexOf(hypertag) != -1;
    }

    public bool HasAnyHypertag(Hypertag[] hypertags)
    {
        foreach (var t in hypertags)
            if (hypertag == t) return true;

        return false;
    }

    static Dictionary<Hypertag, List<HypertaggedObject>> allHypertagObjects = new();

    static void AddToHypertagList(HypertaggedObject obj)
    {
        if (allHypertagObjects.TryGetValue(obj.hypertag, out var l))
        {
            l.Add(obj);
            return;
        }

        allHypertagObjects[obj.hypertag] = l = new();
        l.Add(obj);
    }

    static void RemoveFromHypertagList(HypertaggedObject obj)
    {
        if (allHypertagObjects.TryGetValue(obj.hypertag, out var l))
        {
            l.Remove(obj);
            return;
        }
    }

    public static IEnumerable<HypertaggedObject> Get(Hypertag tag)
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
            foreach (var obj in editorL)
            {
                if (!obj.HasHypertag(tag)) continue;

                yield return obj;
            }
            yield break;            
        }
#endif

        if (allHypertagObjects.TryGetValue(tag, out var l))
        {
            foreach (var obj in l)
            {
                yield return obj;
            }
        }       
    }

    public static IEnumerable<HypertaggedObject> Get(List<Hypertag> tags)
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
            foreach (var obj in editorL)
            {
                if (!obj.HasAnyHypertag(tags)) continue;

                yield return obj;
            }
            yield break;
        }
#endif

        foreach (var tag in tags)
        {
            if (allHypertagObjects.TryGetValue(tag, out var l))
            {
                foreach (var obj in l)
                {
                    yield return obj;
                }
            }
        }
    }

    public static IEnumerable<HypertaggedObject> Get(Hypertag[] tags)
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
            foreach (var obj in editorL)
            {
                if (!obj.HasAnyHypertag(tags)) continue;

                yield return obj;
            }
            yield break;
        }
#endif

        foreach (var tag in tags)
        {
            if (allHypertagObjects.TryGetValue(tag, out var l))
            {
                foreach (var obj in l)
                {
                    yield return obj;
                }
            }
        }
    }

    public static IEnumerable<T> Get<T>(Hypertag tag) where T : Component
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
            foreach (var obj in editorL)
            {
                if (!obj.HasHypertag(tag)) continue;

                var c = obj.GetComponent<T>();
                if (c) yield return c;
            }
            yield break;
        }
#endif

        if (allHypertagObjects.TryGetValue(tag, out var l))
        {
            foreach (var obj in l)
            {
                var c = obj.GetComponent<T>();
                if (c) yield return c;
            }
        }
    }

    public static IEnumerable<T> Get<T>(List<Hypertag> tags) where T : Component
    {
        foreach (var tag in tags)
        {
            foreach (var obj in Get(tag))
            {
                var c = obj.GetComponent<T>();
                if (c) yield return c;
            }
        }
    }
    public static IEnumerable<T> Get<T>(Hypertag[] tags) where T : Component
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
            foreach (var obj in editorL)
            {
                if (!obj.HasAnyHypertag(tags)) continue;

                var c = obj.GetComponent<T>();
                if (c) yield return c;
            }
            yield break;
        }
#endif

        foreach (var tag in tags)
        {
            if (allHypertagObjects.TryGetValue(tag, out var l))
            {
                foreach (var obj in l)
                {
                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
            }
        }
    }

    public static IEnumerable<T> GetInRadius<T>(Hypertag tag, Vector3 position, float radius) where T : Component
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
            foreach (var obj in editorL)
            {
                if (!obj.HasHypertag(tag)) continue;
                if (Vector3.Distance(position, obj.transform.position) < radius)
                {
                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
            }
        }
#endif

        if (allHypertagObjects.TryGetValue(tag, out var l))
        {
            foreach (var obj in l)
            {
                if (Vector3.Distance(position, obj.transform.position) < radius)
                {
                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
            }
        }
    }

    public static IEnumerable<T> GetInRadius<T>(Hypertag tag, Vector2 position, float radius) where T : Component
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            var editorL = FindObjectsByType<HypertaggedObject>(FindObjectsSortMode.None);
            foreach (var obj in editorL)
            {
                if (!obj.HasHypertag(tag)) continue;
                if (Vector2.Distance(position, obj.transform.position) < radius)
                {
                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
            }
        }
#endif
        if (allHypertagObjects.TryGetValue(tag, out var l))
        {
            foreach (var obj in l)
            {
                if (Vector2.Distance(position, obj.transform.position) < radius)
                {
                    var c = obj.GetComponent<T>();
                    if (c) yield return c;
                }
            }
        }
    }
}
