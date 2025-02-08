using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        foreach (var tag in tags)
        {
            foreach (var o in Get(tag))
            {
                yield return o;
            }
            Get(tag);
        }
    }
    public static IEnumerable<HypertaggedObject> Get(Hypertag[] tags)
    {
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
            Get(tag);
        }
    }
    public static IEnumerable<T> Get<T>(Hypertag[] tags) where T : Component
    {
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
}
