using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace UC
{
    /// <summary>
    /// Screen-space outlines around whatever is registered with <see cref="OutlineRegistry"/> - the same
    /// silhouette-and-dilate idea the editor's selection outline uses.
    /// </summary>
    /// <remarks>
    /// Two passes:
    ///
    /// 1. Mask. Every registered renderer is drawn into a small RGBA8 target: RGB = that object's outline
    ///    color, A = its width in pixels divided by Max Width. A > 0 is what "covered" means, so a width of 0
    ///    is automatically no outline. Because the mask carries the parameters, per-object color and width
    ///    cost nothing extra - one mask and one resolve, however many objects are outlined.
    /// 2. Resolve. One fullscreen pass finds, for each uncovered pixel, the nearest covered pixel whose
    ///    stored width reaches it, and blends that pixel's stored color on top of the scene.
    ///
    /// Consequences worth knowing: the outline is one ring around the union of the renderers, so
    /// multi-material objects don't show seams between submeshes and touching objects share an outline;
    /// transparency doesn't matter, because the mask records coverage rather than blending; and no material
    /// on the object is touched, cloned or restored.
    ///
    /// The mask pass runs after transparents (where scene depth is still around for the occlusion test) and
    /// the resolve after post-processing by default, so bloom can't turn the outline into a glow.
    /// </remarks>
    public class ScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        /// <summary>Where the outline is composited. Both of these are after the mask pass.</summary>
        public enum InjectionPoint
        {
            /// <summary>Post-processing sees the outline - bloom will smear it into a glow.</summary>
            BeforeRenderingPostProcessing = RenderPassEvent.BeforeRenderingPostProcessing,
            /// <summary>Post-processing has already run: the outline keeps exactly the color it was given.</summary>
            AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing
        }

        /// <summary>What an outline's width is measured in.</summary>
        public enum WidthMode
        {
            /// <summary>Screen pixels: the outline reads the same whatever the object's distance.</summary>
            ScreenPixels,
            /// <summary>World units: the outline shrinks with distance, like geometry. Still clamped by Max
            /// Width, since that is what bounds the resolve kernel.</summary>
            WorldUnits
        }

        [SerializeField, Range(1.0f, 16.0f), Tooltip("Largest outline width, in pixels, any object is allowed. This bounds the resolve kernel, which costs (2*width+1)^2 taps per pixel - keep it as low as the look allows.")]
        private float           maxWidth = 4.0f;
        [SerializeField, Tooltip("Whether object widths are in screen pixels or world units.")]
        private WidthMode       widthMode = WidthMode.ScreenPixels;
        [SerializeField, Tooltip("Where the outline is composited over the scene.")]
        private InjectionPoint  compositeAt = InjectionPoint.AfterRenderingPostProcessing;
        [SerializeField, Tooltip("Hide the outline where geometry nearer than the outlined object covers it. The ring falls on pixels belonging to other objects and has no depth of its own, so without this it draws over things standing in front.")]
        private bool            occludeOutline = true;
        [SerializeField, Tooltip("Make silhouettes follow alpha-clipped materials' texture alpha, so a fence outlines its bars and not its quad. Costs a material copy per alpha-clipped source material; materials that don't have alpha clipping enabled are unaffected.")]
        private bool            alphaClip = false;
        [SerializeField, Tooltip("Tolerance (world units) for both depth tests. The mask draws the object's own geometry, so its depth equals the scene depth it is compared against - without a little slack, precision noise punches holes in the mask.")]
        private float           depthBias = 0.02f;

        const string kMaskShaderName    = "Unity Common/Effects/Screen Space Outline Mask";
        const string kResolveShaderName = "Unity Common/Effects/Screen Space Outline Resolve";

        Material            maskMaterial;
        Material            resolveMaterial;
        MaskMaterials       maskMaterials;
        OutlineMaskPass     maskPass;
        OutlineResolvePass  resolvePass;

        public override void Create()
        {
            maskMaterials = new MaskMaterials();
            maskPass      = new OutlineMaskPass();
            resolvePass   = new OutlineResolvePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            // Material previews and reflection probes render the world too; they have no business drawing
            // a selection outline. Scene view cameras deliberately DO, so authoring shows the real thing.
            if ((cameraType == CameraType.Preview) || (cameraType == CameraType.Reflection)) return;

            if (OutlineRegistry.count == 0) return;
            if (!EnsureMaterials()) return;

            maskMaterials.Setup(maskMaterial, alphaClip, OutlineRegistry.allTargets);

            maskPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            maskPass.Setup(maskMaterials, maxWidth, widthMode, depthBias);
            // Only ask URP for the depth copy when something actually tests against it - requesting it
            // unconditionally can add a copy pass for nothing.
            maskPass.ConfigureInput(NeedsSceneDepth() ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);

            resolvePass.renderPassEvent = (RenderPassEvent)compositeAt;
            resolvePass.Setup(resolveMaterial, maxWidth, occludeOutline, depthBias);
            resolvePass.ConfigureInput(occludeOutline ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);

            renderer.EnqueuePass(maskPass);
            renderer.EnqueuePass(resolvePass);
        }

        protected override void Dispose(bool disposing)
        {
            maskMaterials?.Dispose();

            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(resolveMaterial);

            maskMaterial    = null;
            resolveMaterial = null;
        }

        static bool NeedsSceneDepth()
        {
            foreach (var target in OutlineRegistry.allTargets)
            {
                if (target == null) continue;
                if (target.occlusion == OutlineOcclusion.VisibleOnly) return true;
            }

            return false;
        }

        bool EnsureMaterials()
        {
            // The shaders live in a Resources folder next to this file, so Shader.Find works in builds too
            // (Resources content is always included) - same arrangement as the rest of UnityCommon.
            if (!maskMaterial)
            {
                var shader = Shader.Find(kMaskShaderName);
                if (!shader)
                {
                    Debug.LogWarning($"Can't find shader \"{kMaskShaderName}\" - screen space outlines are disabled.");
                    return false;
                }
                maskMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            if (!resolveMaterial)
            {
                var shader = Shader.Find(kResolveShaderName);
                if (!shader)
                {
                    Debug.LogWarning($"Can't find shader \"{kResolveShaderName}\" - screen space outlines are disabled.");
                    return false;
                }
                resolveMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            return true;
        }

        static class ShaderIDs
        {
            public static readonly int outlineColor  = Shader.PropertyToID("_OutlineColor");
            public static readonly int outlineParams = Shader.PropertyToID("_OutlineParams");
            public static readonly int depthBias     = Shader.PropertyToID("_OutlineDepthBias");
            public static readonly int mask          = Shader.PropertyToID("_OutlineMask");
            public static readonly int resolveParams = Shader.PropertyToID("_OutlineResolveParams");
            public static readonly int baseMap       = Shader.PropertyToID("_BaseMap");
            public static readonly int baseMapST     = Shader.PropertyToID("_BaseMap_ST");
            public static readonly int mainTex       = Shader.PropertyToID("_MainTex");
            public static readonly int mainTexST     = Shader.PropertyToID("_MainTex_ST");
            public static readonly int cutoff        = Shader.PropertyToID("_Cutoff");
            public static readonly int alphaClip     = Shader.PropertyToID("_AlphaClip");
        }

        /// <summary>
        /// Picks the material each submesh is drawn into the mask with.
        /// </summary>
        /// <remarks>
        /// Everything shares one material, except alpha-clipped submeshes: those need their own texture in the
        /// shader, and DrawRenderer takes no property block, so a per-source-material copy is the only way to
        /// vary it per draw. Only materials that actually have alpha clipping ENABLED get one - which is what
        /// keeps a transparent glass submesh (low alpha, no clipping) in the silhouette where it belongs.
        /// </remarks>
        class MaskMaterials
        {
            readonly Dictionary<Material, Material> variants = new();
            readonly List<Material>                 sourceMaterials = new();

            Material    baseMaterial;
            bool        alphaClip;

            public void Setup(Material baseMaterial, bool alphaClip, List<OutlineTarget> targets)
            {
                if ((this.baseMaterial != baseMaterial) || (this.alphaClip && !alphaClip)) DestroyVariants();

                this.baseMaterial = baseMaterial;
                this.alphaClip    = alphaClip;

                // Variants are built here, on the CPU side of the frame, rather than lazily while the draws
                // are being recorded: creating a material triggers shader variant compilation the first time,
                // which is not something to do from inside a render pass.
                if (alphaClip) Prewarm(targets);
            }

            void Prewarm(List<OutlineTarget> targets)
            {
                foreach (var target in targets)
                {
                    if (target?.renderers == null) continue;

                    foreach (var renderer in target.renderers)
                    {
                        if (!renderer) continue;

                        renderer.GetSharedMaterials(sourceMaterials);
                        foreach (var source in sourceMaterials) EnsureVariant(source);
                    }
                }
            }

            Material EnsureVariant(Material source)
            {
                if (!UsesAlphaClip(source)) return null;
                if (variants.TryGetValue(source, out var variant) && variant) return variant;

                variant = new Material(baseMaterial) { name = $"Outline Mask (alpha clip: {source.name})" };
                variant.EnableKeyword("_OUTLINE_ALPHATEST");
                variant.SetTexture(ShaderIDs.baseMap, GetAlphaTexture(source));
                variant.SetVector(ShaderIDs.baseMapST, GetTextureST(source));
                variant.SetFloat(ShaderIDs.cutoff, source.HasProperty(ShaderIDs.cutoff) ? source.GetFloat(ShaderIDs.cutoff) : 0.5f);

                variants[source] = variant;

                return variant;
            }

            /// <summary>Called once per renderer, before its submeshes are drawn.</summary>
            public void BeginRenderer(Renderer renderer)
            {
                sourceMaterials.Clear();
                if (!alphaClip) return;   // nothing needs the source materials

                // The list overload doesn't allocate, unlike sharedMaterials.
                renderer.GetSharedMaterials(sourceMaterials);
            }

            /// <summary>Lookup only - anything not prewarmed just draws with the shared material.</summary>
            public Material GetForSubMesh(int subMesh)
            {
                if (!alphaClip) return baseMaterial;
                if ((subMesh < 0) || (subMesh >= sourceMaterials.Count)) return baseMaterial;

                var source = sourceMaterials[subMesh];
                if (!source) return baseMaterial;

                return (variants.TryGetValue(source, out var variant) && variant) ? variant : baseMaterial;
            }

            public void Dispose()
            {
                DestroyVariants();
                baseMaterial = null;
            }

            void DestroyVariants()
            {
                foreach (var variant in variants.Values) CoreUtils.Destroy(variant);

                variants.Clear();
            }

            static bool UsesAlphaClip(Material material)
            {
                if (!material) return false;
                if (material.IsKeywordEnabled("_ALPHATEST_ON")) return true;

                // Custom shaders that expose the toggle without URP's keyword.
                return material.HasProperty(ShaderIDs.alphaClip) && (material.GetFloat(ShaderIDs.alphaClip) > 0.5f);
            }

            /// <remarks>The explicit property names come first on purpose: reading a property a shader
            /// doesn't have logs an error, and mainTexture/mainTextureScale go through whatever the shader
            /// tagged (or _MainTex if it tagged nothing), which isn't guaranteed to exist at all.</remarks>
            static Texture GetAlphaTexture(Material material)
            {
                if (material.HasProperty(ShaderIDs.baseMap)) return material.GetTexture(ShaderIDs.baseMap);
                if (material.HasProperty(ShaderIDs.mainTex)) return material.GetTexture(ShaderIDs.mainTex);

                return material.mainTexture;
            }

            static Vector4 GetTextureST(Material material)
            {
                if (material.HasProperty(ShaderIDs.baseMapST)) return material.GetVector(ShaderIDs.baseMapST);
                if (material.HasProperty(ShaderIDs.mainTexST)) return material.GetVector(ShaderIDs.mainTexST);

                return new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
            }
        }

        /// <summary>Carries the mask from the mask pass to the resolve pass, which are separate passes at
        /// separate injection points. This is what ContextItem is for - the handle is only valid for the
        /// frame, so it must not be cached in a field.</summary>
        class OutlineFrameData : ContextItem
        {
            public TextureHandle mask;

            public override void Reset()
            {
                mask = TextureHandle.nullHandle;
            }
        }

        class OutlineMaskPass : ScriptableRenderPass
        {
            class PassData
            {
                public MaskMaterials        materials;
                public List<OutlineTarget>  targets;
                public float                maxWidth;
                public bool                 worldSpaceWidth;
                public float                depthBias;
            }

            MaskMaterials   materials;
            float           maxWidth;
            WidthMode       widthMode;
            float           depthBias;

            public OutlineMaskPass()
            {
                profilingSampler = new ProfilingSampler("Outline Mask");
            }

            public void Setup(MaskMaterials materials, float maxWidth, WidthMode widthMode, float depthBias)
            {
                this.materials  = materials;
                this.maxWidth   = maxWidth;
                this.widthMode  = widthMode;
                this.depthBias  = depthBias;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var targets = OutlineRegistry.allTargets;
                if (targets.Count == 0) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.activeColorTexture.IsValid()) return;

                // Same size (and dynamic scaling behaviour) as the camera target, but a plain 8-bit color
                // texture: no MSAA, because the mask is sampled rather than resolved, and point filtering
                // because the resolve loads exact pixels.
                var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                desc.name               = "_OutlineMask";
                desc.format             = GraphicsFormat.R8G8B8A8_UNorm;
                desc.msaaSamples        = MSAASamples.None;
                desc.bindTextureMS      = false;
                desc.useMipMap          = false;
                desc.autoGenerateMips   = false;
                desc.filterMode         = FilterMode.Point;
                desc.wrapMode           = TextureWrapMode.Clamp;
                desc.clearBuffer        = true;
                desc.clearColor         = Color.clear;

                var mask = renderGraph.CreateTexture(desc);
                frameData.GetOrCreate<OutlineFrameData>().mask = mask;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline Mask", out var passData, profilingSampler))
                {
                    passData.materials       = materials;
                    passData.targets         = targets;
                    passData.maxWidth        = maxWidth;
                    passData.worldSpaceWidth = (widthMode == WidthMode.WorldUnits);
                    passData.depthBias       = depthBias;

                    builder.SetRenderAttachment(mask, 0);

                    // The mask shader reads _CameraDepthTexture, which URP publishes as a render graph
                    // global. Declaring the globals is what makes the graph keep it alive for this pass.
                    builder.UseAllGlobalTextures(true);

                    // Per-object color and width are set as globals between draws (DrawRenderer takes no
                    // property block), which counts as modifying global state.
                    builder.AllowGlobalStateModification(true);
                    // Nothing samples the mask inside this pass, so the graph can't see that it's needed.
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => Execute(data, context));
                }
            }

            static void Execute(PassData data, RasterGraphContext context)
            {
                var cmd = context.cmd;

                cmd.SetGlobalFloat(ShaderIDs.depthBias, data.depthBias);

                foreach (var target in data.targets)
                {
                    if (target == null) continue;

                    var renderers = target.renderers;
                    if ((renderers == null) || (renderers.Length == 0)) continue;
                    if (target.width <= 0.0f) continue;

                    cmd.SetGlobalVector(ShaderIDs.outlineColor, target.shaderColor);
                    // The width stays in its authored unit - the mask shader converts and normalizes it,
                    // because a world-space width depends on the fragment's distance from the camera.
                    cmd.SetGlobalVector(ShaderIDs.outlineParams, new Vector4(
                        target.width,
                        (target.occlusion == OutlineOcclusion.VisibleOnly) ? 1.0f : 0.0f,
                        data.worldSpaceWidth ? 1.0f : 0.0f,
                        data.maxWidth));

                    foreach (var renderer in renderers)
                    {
                        // Renderers can die under us - an outlined object whose scene got unloaded, a pooled
                        // object put away while highlighted.
                        if (!renderer) continue;
                        if (!renderer.enabled) continue;
                        if (!renderer.gameObject.activeInHierarchy) continue;

                        int subMeshCount = GetSubMeshCount(renderer);
                        if (subMeshCount <= 0) continue;

                        data.materials.BeginRenderer(renderer);

                        for (int i = 0; i < subMeshCount; i++)
                            cmd.DrawRenderer(renderer, data.materials.GetForSubMesh(i), i, 0);
                    }
                }
            }

            /// <summary>DrawRenderer draws one submesh, so a multi-material mesh needs one call each.
            /// Counting materials instead would be wrong: anything that appends an extra material (an
            /// inverted-hull outline, for one) would make us redraw submesh 0.</summary>
            static int GetSubMeshCount(Renderer renderer)
            {
                if (renderer is SkinnedMeshRenderer skinned)
                    return (skinned.sharedMesh != null) ? skinned.sharedMesh.subMeshCount : 0;

                var filter = renderer.GetComponent<MeshFilter>();
                if ((filter != null) && (filter.sharedMesh != null)) return filter.sharedMesh.subMeshCount;

                return 1;
            }
        }

        class OutlineResolvePass : ScriptableRenderPass
        {
            class PassData
            {
                public Material         material;
                public TextureHandle    mask;
                public float            maxWidth;
                public bool             occlude;
                public float            depthBias;
            }

            static readonly MaterialPropertyBlock propertyBlock = new();

            Material    material;
            float       maxWidth;
            bool        occlude;
            float       depthBias;

            public OutlineResolvePass()
            {
                profilingSampler = new ProfilingSampler("Outline Resolve");
            }

            public void Setup(Material material, float maxWidth, bool occlude, float depthBias)
            {
                this.material   = material;
                this.maxWidth   = maxWidth;
                this.occlude    = occlude;
                this.depthBias  = depthBias;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var mask = frameData.GetOrCreate<OutlineFrameData>().mask;
                if (!mask.IsValid()) return;   // mask pass bailed out this frame

                var resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.activeColorTexture.IsValid()) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline Resolve", out var passData, profilingSampler))
                {
                    passData.material   = material;
                    passData.mask       = mask;
                    passData.maxWidth   = maxWidth;
                    passData.occlude    = occlude;
                    passData.depthBias  = depthBias;

                    builder.UseTexture(mask, AccessFlags.Read);
                    // Scene depth, for giving the ring a depth of its own (see the shader).
                    builder.UseAllGlobalTextures(true);
                    // The outline is hardware-blended straight onto the camera color, so the scene never
                    // has to be copied into a second target.
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => Execute(data, context));
                }
            }

            static void Execute(PassData data, RasterGraphContext context)
            {
                propertyBlock.Clear();
                propertyBlock.SetTexture(ShaderIDs.mask, data.mask);
                propertyBlock.SetVector(ShaderIDs.resolveParams, new Vector4(
                    data.maxWidth,
                    Mathf.Ceil(data.maxWidth),
                    data.depthBias,
                    data.occlude ? 1.0f : 0.0f));

                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, propertyBlock);
            }
        }
    }
}
