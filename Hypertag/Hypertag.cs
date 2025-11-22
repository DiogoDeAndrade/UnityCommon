using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Hypertag", menuName = "Unity Common/Hypertag")]
    public class Hypertag : ScriptableObject
    {
        public T FindFirst<T>() where T: Component
        {
            return FindFirstObjectWithHypertag<T>(this);
        }

        public static T FindFirstObjectWithHypertag<T>(Hypertag tag) where T : Component
        {
            List<T> ret = new List<T>();

            var objects = HypertaggedObject.Get(tag);
            foreach (var obj in objects)
            {
                var c = obj.GetComponent<T>();
                if (c)
                {
                    return c;
                }
            }

            return null;
        }

        public static List<T> FindObjectsWithHypertag<T>(Hypertag tag) where T : Component
        {
            List<T> ret = new List<T>();

            var objects = HypertaggedObject.Get(tag);
            foreach (var obj in objects)
            {
                var c = obj.GetComponent<T>();
                if (c)
                {
                    ret.Add(c);
                }
            }

            return ret;
        }

        public static List<T> FindObjectsWithHypertag<T>(Hypertag[] tags) where T : Component
        {
            List<T> ret = new List<T>();

            var objects = HypertaggedObject.Get(tags);
            foreach (var obj in objects)
            {
                var c = obj.GetComponent<T>();
                if (c)
                {
                    ret.Add(c);
                }
            }

            return ret;
        }
    }
}