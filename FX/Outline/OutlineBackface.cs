using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class OutlineBackface : MonoBehaviour
    {
        public enum Channel { UV0 = 0, UV1 = 1, UV2 = 2, UV3 = 3, UV4 = 4, UV5 = 5, UV6 = 6, UV7 = 7, Normal = 8 };
        private static readonly string[] kDirKeywords =
        {
        "_DIRSOURCE_UV0","_DIRSOURCE_UV1","_DIRSOURCE_UV2","_DIRSOURCE_UV3",
        "_DIRSOURCE_UV4","_DIRSOURCE_UV5","_DIRSOURCE_UV6","_DIRSOURCE_UV7",
        "_DIRSOURCE_NORMAL"
    };

        [SerializeField] private Color color = Color.black;
        [SerializeField] private float width = 0.02f;
        [SerializeField] private Channel channel = Channel.UV7;
        [SerializeField] private bool worldSpace = true;
        [SerializeField] private bool continuousUpdate;

        Dictionary<Renderer, Material> rendererOutlineMaterial = new();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            var renderers = GetComponentsInChildren<Renderer>();

            ToggleOutline(true, renderers);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;

            ToggleOutline(false, null);
        }

        private void ToggleOutline(bool enable, Renderer[] renderers)
        {
            if (enable)
            {
                foreach (var renderer in renderers)
                {
                    if (!rendererOutlineMaterial.ContainsKey(renderer))
                    {
                        // Create material
                        Material material = new Material(Shader.Find("Unity Common/Effects/Outline (Backface Extrude)"));
                        material.SetColor("_OutlineColor", color);
                        material.SetFloat("_OutlineWidth", width);
                        material.SetFloat("_Mode", (worldSpace) ? (0.0f) : (1.0f));
                        material.SetInt("_DirSource", (int)channel);

                        for (int i = 0; i < kDirKeywords.Length; i++)
                            material.DisableKeyword(kDirKeywords[i]);
                        material.EnableKeyword(kDirKeywords[(int)channel]);

                        rendererOutlineMaterial.Add(renderer, material);
                    }
                    SetMaterial(renderer, rendererOutlineMaterial[renderer]);
                }
            }
            else
            {
                foreach (var renderer in rendererOutlineMaterial)
                {
                    UnsetMaterial(renderer.Key, renderer.Value);
                }
            }
        }

        private void SetMaterial(Renderer renderer, Material material)
        {
            var materials = renderer.materials;
            foreach (var mat in materials)
            {
                if (mat == material) return; // Already set
            }

            renderer.SetMaterials(new List<Material>(materials) { material });
        }

        private void UnsetMaterial(Renderer renderer, Material material)
        {
            var materials = new List<Material>(renderer.materials);
            materials.Remove(material);
            renderer.SetMaterials(materials);
        }

        [Button("Update Materials")]
        public void UpdateMaterials()
        {
            foreach (var renderer in rendererOutlineMaterial)
            {
                var material = renderer.Value;
                material.SetColor("_OutlineColor", color);
                material.SetFloat("_OutlineWidth", width);
                material.SetFloat("_Mode", (worldSpace) ? (0.0f) : (1.0f));
            }
        }

        private void Update()
        {
            if (continuousUpdate)
            {
                UpdateMaterials();
            }
        }
    }
}