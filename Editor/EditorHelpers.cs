#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace UC
{

    public static class EditorHelpers
    {
        public static void RepaintInspector(System.Type t)
        {
            Editor[] ed = (Editor[])Resources.FindObjectsOfTypeAll<Editor>();
            for (int i = 0; i < ed.Length; i++)
            {
                if (ed[i].GetType() == t)
                {
                    ed[i].Repaint();
                    return;
                }
            }
        }
        public static void RepaintInspector<T>() where T : Editor
        {
            Editor[] ed = (Editor[])Resources.FindObjectsOfTypeAll<Editor>();
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