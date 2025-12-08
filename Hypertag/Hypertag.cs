using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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