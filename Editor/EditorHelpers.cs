#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace UC.Editor
{

    public static class EditorHelpers
    {
        public static void RepaintInspector(System.Type t)
        {
            var ed = Resources.FindObjectsOfTypeAll<UnityEditor.Editor>();
            for (int i = 0; i < ed.Length; i++)
            {
                if (ed[i].GetType() == t)
                {
                    ed[i].Repaint();
                    return;
                }
            }
        }
        public static void RepaintInspector<T>() where T : UnityEditor.Editor
        {
            var ed = Resources.FindObjectsOfTypeAll<UnityEditor.Editor>();
            for (int i = 0; i < ed.Length; i++)
            {
                if (ed[i] as T != null)
                {
                    ed[i].Repaint();
                    return;
                }
            }
        }
    }
}
#endif