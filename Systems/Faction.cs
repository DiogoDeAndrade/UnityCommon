using NaughtyAttributes;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

namespace UC
{

    [CreateAssetMenu(fileName = "Hypertag", menuName = "Unity Common/Faction")]
    public class Faction : ScriptableObject
    {
        [SerializeField] private List<Faction> hostileFaction;

        public bool IsHostile(Faction faction)
        {
            if (hostileFaction == null) return false;

            foreach (var f in hostileFaction)
            {
                if (f == faction) return true;
            }

            return false;
        }

        public void SetHostile(Faction faction)
        {
            if (IsHostile(faction)) return;

            if (hostileFaction == null) hostileFaction = new();

            for (int i = 0; i < hostileFaction.Count; i++)
            {
                if (hostileFaction[i] == null)
                {
                    hostileFaction[i] = faction;
                    return;
                }
            }

            hostileFaction.Add(faction);
        }

        [Button("Reciprocate Hostile")]
        void CopyToOthers()
        {
            if (hostileFaction != null)
            {
                foreach (var faction in hostileFaction)
                {
                    if (faction == null) continue;
                    if (!faction.IsHostile(this))
                    {
                        faction?.SetHostile(this);
#if UNITY_EDITOR
                        EditorUtility.SetDirty(faction);
#endif
                    }
                }
            }

        }
    }
}