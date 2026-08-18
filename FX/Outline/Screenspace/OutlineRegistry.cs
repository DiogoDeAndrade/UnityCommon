using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    /// <summary>What happens to the outline where the object is behind other geometry.</summary>
    public enum OutlineOcclusion
    {
        /// <summary>Only outline the parts of the object that are actually visible (tested against scene depth).</summary>
        VisibleOnly,
        /// <summary>Outline the whole silhouette, walls included.</summary>
        Always
    }

    /// <summary>
    /// One thing to outline: a set of renderers plus the parameters it is drawn with. Registered with
    /// <see cref="OutlineRegistry"/>, which is what <c>ScreenSpaceOutlineFeature</c> reads while rendering.
    /// </summary>
    /// <remarks>
    /// Use <see cref="ScreenSpaceOutline"/> if a component on the object is convenient; create a target
    /// directly when the renderer list comes from somewhere else (an entity that already keeps a filtered
    /// list, a pooled object, code that outlines things it doesn't own). Fields can be changed at any time -
    /// they're read fresh every frame, so animating color or width needs no re-registration.
    /// </remarks>
    public class OutlineTarget
    {
        /// <summary>Renderers that make up the silhouette. Only MeshRenderer/SkinnedMeshRenderer are drawn;
        /// nulls and disabled renderers are skipped, so a destroyed object is harmless.</summary>
        public Renderer[]       renderers;
        /// <summary>Outline color. Alpha is ignored (see Outline.md - the mask has no channel left for it).</summary>
        public Color            color = Color.white;
        /// <summary>Thickness in screen pixels, clamped by the feature's Max Width. 0 means no outline.</summary>
        public float            width = 3.0f;
        public OutlineOcclusion occlusion = OutlineOcclusion.VisibleOnly;

        /// <summary>The color as the shader needs it. Colors authored in the inspector are sRGB, and the
        /// mask is written straight into a render target, so linear projects have to convert here - nothing
        /// down the line will do it for a value passed as a raw vector.</summary>
        public Color shaderColor => (QualitySettings.activeColorSpace == ColorSpace.Linear) ? color.linear : color;
    }

    /// <summary>
    /// The list of things currently outlined. Registration is explicit (no scene scanning), so the render
    /// feature costs nothing at all when nothing is registered.
    /// </summary>
    public static class OutlineRegistry
    {
        static readonly List<OutlineTarget> targets = new();

        public static int count => targets.Count;

        /// <summary>The live list, for the render feature. Not a copy - never mutate it while rendering.</summary>
        internal static List<OutlineTarget> allTargets => targets;

        public static void Register(OutlineTarget target)
        {
            if (target == null) return;
            if (targets.Contains(target)) return;

            targets.Add(target);
        }

        public static void Unregister(OutlineTarget target)
        {
            if (target == null) return;

            targets.Remove(target);
        }

        public static void Clear()
        {
            targets.Clear();
        }

        /// <summary>Static state survives leaving play mode when domain reload is disabled, and a target
        /// registered by a scene object would then point at a dead renderer. Clear on subsystem
        /// registration, which runs before any scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            targets.Clear();
        }
    }
}
