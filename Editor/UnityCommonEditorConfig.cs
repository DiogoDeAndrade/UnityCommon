using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "UnityCommonEditorConfig", menuName = "Unity Common/Editor Config")]
    public class UnityCommonEditorConfig : ScriptableObject
    {
        [SerializeField] private List<Texture2D> textures;

        private Dictionary<string, Texture2D> textureMap;

        private static UnityCommonEditorConfig _instance;

        public static UnityCommonEditorConfig Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_EDITOR
                    string[] guids = AssetDatabase.FindAssets("t:UnityCommonEditorConfig"); // Find all assets of this type

                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]); // Convert GUID to asset path
                        _instance = AssetDatabase.LoadAssetAtPath<UnityCommonEditorConfig>(path);
                    }

                    if (_instance == null)
                    {
                        Debug.LogError("UnityCommonEditorConfig instance not found. Ensure it's correctly created.");
                    }
#else
                Debug.LogError("UnityCommonEditorConfig should only be accessed in the Editor.");
#endif
                }
                return _instance;
            }
        }

        public Texture2D GetTexture(string name)
        {
            if (textureMap == null)
            {
                textureMap = new();
                if (textures != null)
                {
                    foreach (var t in textures)
                    {
                        textureMap.Add(t.name, t);
                    }
                }
            }

            if (textureMap.TryGetValue(name, out var texture))
            {
                return texture;
            }

            return null;
        }
    }
}