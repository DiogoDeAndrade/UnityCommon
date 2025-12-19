using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{

    [CreateAssetMenu(fileName = "Archetype", menuName = "Unity Common/RPG/Archetype")]
    public class Archetype : ModularScriptableObject
    {
        [Header("Text")]
        public string   displayName;
        [SerializeField, ResizableTextArea]
        private string  _description;

        public string description
        {
            get
            {
                if (string.IsNullOrEmpty(_description))
                {
                    foreach (var p in parents)
                    {
                        var tmp = (p as Archetype)?.description ?? string.Empty;
                        if (!string.IsNullOrEmpty(tmp)) return tmp;
                    }
                }

                return _description;
            }
        }
    }
}
