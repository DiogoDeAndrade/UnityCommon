using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Hypertag", menuName = "Unity Common/Hypertag")]
public class Hypertag : ScriptableObject
{
    public static T FindObjectWithHypertag<T>(Hypertag tag) where T : Component
    {
        var objects = FindObjectsByType<HypertaggedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in objects)
        {
            if (obj.hypertag == tag)
            {
                var ret = obj.GetComponent<T>();

                return ret;
            }
        }

        return null;
    }

    public static List<T> FindObjectsWithHypertag<T>(Hypertag tag) where T : Component
    {
        List<T> ret = new List<T>();
        var objects = FindObjectsByType<HypertaggedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in objects)
        {
            if (obj.hypertag == tag)
            {
                var c = obj.GetComponent<T>();
                if (c)
                {
                    ret.Add(c);
                }
            }
        }

        return ret;
    }
}
