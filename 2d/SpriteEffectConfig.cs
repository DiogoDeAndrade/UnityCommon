using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    [CreateAssetMenu(fileName = "SpriteEffectConfig", menuName = "Unity Common/Sprite Effect Config")]
    public class SpriteEffectConfig : ScriptableObject
    {
        [System.Serializable]
        struct MaterialToMaterial
        {
            public Material srcMaterial;
            public Material destMaterial;
        }

        [SerializeField] private List<MaterialToMaterial> shaderToMaterial;

        Dictionary<Shader, Material> shaderMap = new Dictionary<Shader, Material>();

        private static SpriteEffectConfig _instance;

        public static SpriteEffectConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<SpriteEffectConfig>("SpriteEffectConfig");
                    if (_instance == null)
                    {
                        Debug.LogError("SpriteEffectConfig instance not found. Make sure it is located in a 'Resources' folder and named 'SpriteEffectConfig'.");
                    }
                }
                return _instance;
            }
        }

        void BuildMap()
        {
            if ((shaderMap == null) || (shaderMap.Count == 0))
            {
                shaderMap = new();
                foreach (var m in shaderToMaterial)
                {
                    shaderMap.Add(m.srcMaterial.shader, m.destMaterial);
                }
            }
        }

        public Material GetMaterial(Shader shader)
        {
            BuildMap();

            if (shaderMap.TryGetValue(shader, out var material))
            {
                return material;
            }

            return null;
        }

        public Shader GetShader(Shader shader)
        {
            BuildMap();

            if (shaderMap.TryGetValue(shader, out var material))
            {
                return material.shader;
            }

            return shader;
        }
    }
}