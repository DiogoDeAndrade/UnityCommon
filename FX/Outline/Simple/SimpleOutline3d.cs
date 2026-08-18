using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class SimpleOutline3d : MonoBehaviour
    {
        public enum Channel { UV0 = 0, UV1 = 1, UV2 = 2, UV3 = 3, UV4 = 4, UV5 = 5, UV6 = 6, UV7 = 7, Normal = 8 };
        private static readonly string[] kDirKeywords =
        {
            "_DIRSOURCE_UV0","_DIRSOURCE_UV1","_DIRSOURCE_UV2","_DIRSOURCE_UV3",
            "_DIRSOURCE_UV4","_DIRSOURCE_UV5","_DIRSOURCE_UV6","_DIRSOURCE_UV7",
            "_DIRSOURCE_NORMAL"
        };
        
        public enum Space { Object, World, Clip }
        static readonly string[] kSpaceKeywords =
        {
          "_EXTRUDESPACE_OBJECT","_EXTRUDESPACE_WORLD","_EXTRUDESPACE_CLIP"
        };

        [SerializeField] private Color      color = Color.black;
        [SerializeField] private float      width = 0.02f;
        [SerializeField] private Channel    channel = Channel.UV7;
        [SerializeField] private Space      space = Space.World;
        [SerializeField] private bool       continuousUpdate;
        [SerializeField] private Renderer[] targetRenderers;

        Dictionary<Renderer, Material> rendererOutlineMaterial = new();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            if ((targetRenderers != null) && (targetRenderers.Length > 0))
                ToggleOutline(true, targetRenderers);
            else
                ToggleOutline(true, GetComponentsInChildren<Renderer>());
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;

            ToggleOutline(false, null);
        }

        private void OnDestroy()
        {
            DestroyOutlineMaterials();
        }

        private void DestroyOutlineMaterials()
        {
            foreach (var kv in rendererOutlineMaterial)
            {
                var renderer = kv.Key;
                var material = kv.Value;

                if (renderer && material)
                    UnsetMaterial(renderer, material);

                if (material)
                    material.Delete();
            }

            rendererOutlineMaterial.Clear();
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
                        var shader = GetOutlineShader();
                        if (!shader)
                        {
                            Debug.LogWarning("Outline shader not found.");
                            return;
                        }

                        Material material = new Material(shader);
                        material.SetColor("_OutlineColor", color);
                        material.SetFloat("_OutlineWidth", width);
                        material.SetInt("_DirSource", (int)channel);
                        material.SetInt("_ExtrudeSpace", (int)space);

                        for (int i = 0; i < kDirKeywords.Length; i++)
                            material.DisableKeyword(kDirKeywords[i]);
                        material.EnableKeyword(kDirKeywords[(int)channel]);

                        for (int i = 0; i < kSpaceKeywords.Length; i++)
                            material.DisableKeyword(kSpaceKeywords[i]);
                        material.EnableKeyword(kSpaceKeywords[(int)space]);

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
            var materials = new List<Material>(renderer.sharedMaterials);

            foreach (var mat in materials)
            {
                if (mat == material)
                    return; // Already set
            }

            materials.Add(material);
            renderer.SetSharedMaterials(materials);
        }

        private void UnsetMaterial(Renderer renderer, Material material)
        {
            var materials = new List<Material>(renderer.sharedMaterials);

            if (materials.Remove(material))
            {
                renderer.SetSharedMaterials(materials);
            }
        }

        [Button("Update Materials")]
        public void UpdateMaterials()
        {
            foreach (var renderer in rendererOutlineMaterial)
            {
                var material = renderer.Value;
                material.SetColor("_OutlineColor", color);
                material.SetFloat("_OutlineWidth", width);
            }
        }

        private void Update()
        {
            if (continuousUpdate)
            {
                UpdateMaterials();
            }
        }

        private static Shader outlineShader;

        private static Shader GetOutlineShader()
        {
            if (!outlineShader)
                outlineShader = Shader.Find("Unity Common/Effects/Outline (Backface Extrude)");

            return outlineShader;
        }
    }
}