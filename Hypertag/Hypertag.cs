using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "Hypertag", menuName = "Unity Common/Hypertag")]
    public class Hypertag : ScriptableObject
    {
        [SerializeField] private string _displayName = String.Empty;

        public string displayName
        {
            get => string.IsNullOrEmpty(_displayName) ? name : _displayName;
            set
            {
                _displayName = value;
            }
        }

        public T FindFirst<T>() where T : Component
        {
            return FindFirstObjectWithHypertag<T>(this);
        }

        public GameObject FindIn(GameObject baseObject) 
        {
            if (baseObject.HasHypertag(this))
            {
                return baseObject;
            }

            foreach (Transform t in baseObject.transform)
            {
                var ret = FindIn(t.gameObject);
                if (ret) return ret;
            }

            return null;
        }

        public T FindIn<T>(GameObject baseObject) where T : Component
        {
            if (baseObject.HasHypertag(this))
            {
                return baseObject.GetComponent<T>();
            }

            foreach (Transform t in baseObject.transform)
            {
                var ret = FindIn<T>(t.gameObject);
                if (ret) return ret;
            }

            return null;
        }

        public List<GameObject> FindAllIn(GameObject baseObject, List<GameObject> outputList = null)
        {
            var ret = outputList ?? new List<GameObject>();

            if (baseObject.HasHypertag(this))
            {
                ret.Add(baseObject);
            }

            foreach (Transform t in baseObject.transform)
            {
                FindAllIn(t.gameObject, ret);
            }

            return ret;
        }

        public List<T> FindAllIn<T>(GameObject baseObject, List<T> outputList = null) where T : Component
        {
            var ret = outputList ?? new List<T>();

            if (baseObject.HasHypertag(this))
            {
                ret.AddRange(baseObject.GetComponents<T>());
            }

            foreach (Transform t in baseObject.transform)
            {
                FindAllIn<T>(t.gameObject, ret);
            }

            return ret;
        }

        public GameObject FindFirstGameObject()
        {
            var objects = HypertaggedObject.Get(this);
            foreach (var obj in objects)
            {
                return obj.gameObject;
            }

            return null;
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