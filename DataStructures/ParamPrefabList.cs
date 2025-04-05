using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    [System.Serializable]
    public class NamedParamPrefab<T> where T : UnityEngine.Object
    {
        public string name;
        public ParamPrefab<T> prefab = new ParamPrefab<T>();
    }


    [System.Serializable]
    public class ParamPrefabListBase { }


    [Serializable]
    public class ParamPrefabList<T> : ParamPrefabListBase, IEnumerable<NamedParamPrefab<T>> where T : UnityEngine.Object
    {
        [SerializeField]
        public List<NamedParamPrefab<T>> items = new();

        public IEnumerator<NamedParamPrefab<T>> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count => items.Count;

        public NamedParamPrefab<T> this[int index] => items[index];
    }
}