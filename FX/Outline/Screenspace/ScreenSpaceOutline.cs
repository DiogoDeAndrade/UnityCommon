using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    /// <summary>
    /// Outlines every mesh below this object, screen-space, through <c>ScreenSpaceOutlineFeature</c>.
    /// Enable/disable the component to turn the outline on and off.
    /// </summary>
    /// <remarks>
    /// Nothing here touches the object's materials - the outline is a separate pass over a silhouette mask -
    /// so an object can be outlined regardless of what it's made of: opaque, transparent, skinned, or a mix
    /// of all three in one hierarchy. That also means there is nothing to restore when the outline goes away.
    ///
    /// This is a convenience wrapper: it owns an <see cref="OutlineTarget"/> and keeps it registered while
    /// the component is enabled. Code that already has a renderer list can skip the component entirely and
    /// register a target itself.
    /// </remarks>
    /// <remarks>
    /// ExecuteAlways because the outline is worth seeing while authoring: without it Unity never calls
    /// OnEnable/OnDisable outside play mode, so nothing would register and the scene view would show nothing.
    /// The render feature already runs for scene view cameras.
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("Unity Common/3d/Screen Space Outline")]
    public class ScreenSpaceOutline : MonoBehaviour
    {
        [SerializeField]
        private Color               color = Color.white;
        [SerializeField, Tooltip("Thickness in screen pixels. Clamped by the render feature's Max Width, which is what bounds the resolve kernel.")]
        private float               width = 3.0f;
        [SerializeField]
        private OutlineOcclusion    occlusion = OutlineOcclusion.VisibleOnly;
        [SerializeField, Tooltip("Renderers to outline. Leave empty to use every mesh/skinned renderer below this object.")]
        private Renderer[]          targetRenderers;
        [SerializeField, Tooltip("Renderers on these layers are never outlined - collision proxies, inverted-hull outline meshes, and anything else that isn't part of the silhouette.")]
        private LayerMask           excludeLayers = 0;

        readonly OutlineTarget target = new();

        public Color outlineColor
        {
            get => color;
            set { color = value; target.color = value; }
        }

        public float outlineWidth
        {
            get => width;
            set { width = value; target.width = value; }
        }

        public OutlineOcclusion outlineOcclusion
        {
            get => occlusion;
            set { occlusion = value; target.occlusion = value; }
        }

        void OnEnable()
        {
            Refresh();
            OutlineRegistry.Register(target);
        }

        void OnDisable()
        {
            OutlineRegistry.Unregister(target);
        }

        /// <summary>Re-collects the renderers and pushes the current settings. Call it after adding or
        /// removing geometry below this object - runtime-instanced children are the usual reason.</summary>
        [Button("Refresh")]
        public void Refresh()
        {
            target.color     = color;
            target.width     = width;
            target.occlusion = occlusion;
            target.renderers = CollectRenderers();
        }

        void OnValidate()
        {
            // Inspector tweaks should show up without a re-enable; the feature re-reads the target every frame.
            target.color     = color;
            target.width     = width;
            target.occlusion = occlusion;

            // Editing the renderer list or the layer filter has to re-collect, and a component added at edit
            // time gets its OnValidate before anything else - Register is idempotent, so this is also what
            // makes a freshly added component show its outline immediately.
            if (isActiveAndEnabled)
            {
                target.renderers = CollectRenderers();
                OutlineRegistry.Register(target);
            }
        }

        Renderer[] CollectRenderers()
        {
            var source = ((targetRenderers != null) && (targetRenderers.Length > 0))
                ? targetRenderers
                : GetComponentsInChildren<Renderer>(true);

            List<Renderer> result = new();

            foreach (var renderer in source)
            {
                if (!renderer) continue;
                // The mask draws with our own material and a plain mesh vertex layout, so only these two
                // renderer types make sense - particles, trails and lines would be drawn wrong, not just ugly.
                if ((renderer is not MeshRenderer) && (renderer is not SkinnedMeshRenderer)) continue;
                if ((excludeLayers.value & (1 << renderer.gameObject.layer)) != 0) continue;

                result.Add(renderer);
            }

            return result.ToArray();
        }
    }
}
