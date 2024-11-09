using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UnityCommonEditorConfig", menuName = "Unity Common/Editor Config")]
public class UnityCommonEditorConfig : ScriptableObject
{
    [SerializeField] private List<Texture2D>  textures;

    private Dictionary<string, Texture2D> textureMap;

    private static UnityCommonEditorConfig _instance;

    public static UnityCommonEditorConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<UnityCommonEditorConfig>("UnityCommonEditorConfig");
                if (_instance == null)
                {
                    Debug.LogError("UnityCommonEditorConfig instance not found. Make sure it is located in a 'Resources' folder and named 'UnityCommonEditorConfig'.");
                }
            }
            return _instance;
        }
    }

    public Texture2D GetTexture(string name)
    {
        if (textureMap == null)
        {
            textureMap = new();
            foreach (var t in textures)
            {
                textureMap.Add(t.name, t);
            }
        }

        if (textureMap.TryGetValue(name, out var texture))
        {
            return texture;
        }

        return null;
    }
}
