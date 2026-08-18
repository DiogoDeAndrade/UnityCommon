# Screen space outlines

Outlines drawn as a render pass, the way the editor's selection outline works: the outlined objects are
rendered into a silhouette mask, the mask is dilated and edge-detected, and the result is blended over the
scene. Nothing touches the objects' materials.

Because of that:

- **Transparent, opaque and skinned meshes all work**, and can be mixed inside one outlined object - the mask
  records coverage, not blending.
- **One ring per selection.** Multi-material objects show no seams between submeshes, and objects outlined
  together share a single outline where they touch.
- **Nothing to restore.** No material cloning, no saving and putting back emissive/outline parameters, and an
  object's own authored outline (inverted hull or otherwise) is left completely alone.

## Setup

1. Add the **Screen Space Outline Feature** to the URP Renderer asset (Add Renderer Feature).
2. Add a **Screen Space Outline** component (Unity Common > 3d) to the top of any object you want outlined,
   or register an `OutlineTarget` from code.
3. Enable/disable the component to turn the outline on and off.

The shaders live in `FX/Outline/Screenspace/Resources`, so `Shader.Find` reaches them in builds; nothing needs assigning
by hand.

## On the render feature

| | |
|---|---|
| `Max Width` | Largest width in pixels any object may use. Bounds the resolve kernel, which costs `(2*width+1)^2` taps per pixel - the practical range is small, 1-6; 16 is a hard ceiling, not a recommendation. |
| `Width Mode` | `ScreenPixels` (constant on screen) or `WorldUnits` (shrinks with distance, like geometry). World widths are still clamped by Max Width. |
| `Composite At` | After post-processing keeps the outline's exact color; before it lets bloom smear it into a glow. |
| `Occlude Outline` | Hides the outline where nearer geometry covers it. On by default - see below. |
| `Alpha Clip` | Makes silhouettes follow alpha-clipped materials' texture alpha (fences, foliage). Off by default; costs one material copy per alpha-clipped source material. |
| `Depth Bias` | Tolerance in world units for both depth tests. |

### Why `Occlude Outline` exists

The ring is drawn *outside* the silhouette, so its pixels belong to other geometry and it has no depth of its
own - a crate standing in front of an outlined object would still get painted over. The resolve compares the
scene depth at each ring pixel against the scene depth at the mask pixel the ring came from, and drops the
ring where the scene is nearer. That needs no per-object setting: a hidden object's own pixels already read
its occluder's depth, so a through-wall (`Always`) outline still draws across the wall that hides it.

Turn it off for outlines that must be visible over absolutely everything.

## Per object, on the component or the target

| | |
|---|---|
| `color` | Outline color. Alpha is ignored - see limitations. |
| `width` | Thickness, in whatever the feature's **Width Mode** says (screen pixels or world units), clamped by **Max Width**. 0 disables the outline. |
| `occlusion` | `VisibleOnly` outlines only what the camera can see; `Always` outlines the whole silhouette, through walls. |
| `targetRenderers` | Explicit renderer list. Empty means every mesh/skinned renderer below the object. |
| `excludeLayers` | Renderers on these layers never contribute - collision proxies, hull-outline meshes, anything that isn't part of the silhouette. |

All of these can change every frame; the feature re-reads them, so animating color or width needs no
re-registration.

## From code, without a component

```csharp
var target = new OutlineTarget { renderers = myRenderers, color = Color.white, width = 3.0f };
OutlineRegistry.Register(target);       // outline on
...
OutlineRegistry.Unregister(target);     // outline off
```

Useful when the renderer list already exists somewhere else (an entity that keeps a filtered list, a pooled
object) - the component is only a convenience wrapper around exactly this.

## How the parameters survive the pass

The mask is a single RGBA8 target holding, per pixel, `RGB` = the object's outline color and
`A` = its width / **Max Width**. `A > 0` is what "covered" means, so width 0 is automatically no outline. The
resolve pass then asks, for each uncovered pixel, which covered pixel is nearest *among those whose stored
width reaches this far*, and takes that pixel's color.

So per-object color and width cost nothing extra: one mask, one fullscreen resolve, no matter how many
objects are outlined at once.

## Limitations, and where the room is

- **No per-object opacity.** The mask's four channels are full (three colour + width). A fifth value needs
  either a second mask target or a narrower colour encoding (16-bit colour would free 8 bits) - that is also
  what a "dimmer where hidden" mode would need.
- **`VisibleOnly` outlines the clipped edge.** Where a wall cuts across the object, the visible part gets an
  outline along the wall edge too. The editor's selection outline behaves the same way.
- **Both depth tests are only as good as `_CameraDepthTexture`.** They fail open - outline always drawn - when
  the scene isn't in it, and there is a trap here: if anything requests a *DepthNormals* prepass (URP's SSAO
  with Source = Depth Normals, for one), that prepass replaces the depth copy, and it only renders shaders
  that have a `DepthNormals` pass. Custom shaders with just a `DepthOnly` pass silently vanish from the depth
  texture, and then only URP-Lit objects occlude anything. Check the frame debugger for a CopyDepth pass, or a
  DrawDepthNormalPrepass with suspiciously few events.
- **`VisibleOnly` tests against the depth copy**, which URP fills after opaques by default, so transparent
  geometry does not occlude an outline. Setting the pipeline's depth texture mode to *after transparents*
  changes that.
- **Width in `ScreenPixels` mode is pixels at render resolution**, so an outline is relatively thinner at 4K
  than at 1080p. `WorldUnits` mode has the opposite property by design.
- **Cost is `(2*radius+1)^2` 8-bit loads per uncovered pixel.** Fine at 1080p with a small **Max Width**.
  The two ways out are below.

## Making it cheaper, or thicker

Two separate upgrades, in the order they're worth doing.

### 1. Restrict the resolve to the objects' screen bounds

The kernel runs over the whole screen even though the outline only touches a small part of it. Project each
outlined renderer's world bounds to screen space, union them, pad by the radius, and `SetViewport` that rect
before the fullscreen triangle. The shader needs no change at all - it derives its coordinates from
`SV_POSITION`, which stays absolute under a viewport.

The trap: the rect's Y may need flipping depending on the render target
(`cameraData.IsCameraProjectionMatrixFlipped()`), and getting it wrong means a silently missing outline rather
than an obvious error. Also bail out to the full screen when any bounds corner is behind the camera, since the
projection math breaks there.

This is the bigger win for a normal game, where one or two objects are outlined at a time.

### 2. Jump flood, if outlines need to be thick

The square kernel is `O(radius^2)` per pixel, so it stops being sane somewhere around 6-8 px - which is what
**Max Width**'s 16 px ceiling is really about. Jump flood (JFA) replaces it with `log2(radius)` fullscreen
passes: seed each covered pixel with its own coordinate, then for step sizes `radius/2, radius/4, ... 1`, each
pixel takes the nearest seed among its 8 neighbours at that step. The result is a nearest-seed coordinate plus
distance for every pixel.

It fits this design well: **read the color and width from the mask at the winning seed coordinate**, and the
per-object parameters keep working exactly as they do now, with the same "does that seed's width reach this
far" test.

What it costs:

- **More passes, not fewer taps.** 7-8 fullscreen passes for a 128 px reach, ~9 taps each. Below ~6-8 px the
  square kernel is *faster* - JFA only pays off for thick outlines.
- **An extra ping-pong buffer** holding seed coordinates (`RG16_UInt`, or `RG32_SFloat` above 2048 px), on top
  of the mask. More memory and bandwidth than the single RGBA8 today.
- **Distances are approximate.** JFA can be a pixel or two wrong in pathological seed layouts. Invisible on an
  outline; it would matter if the field were used for anything measured.
- **It fights upgrade 1.** Every JFA pass is fullscreen by nature, so the bounds restriction is easier to keep
  with the current kernel.
- It changes nothing else: no help with the depth-texture issues above, and no effect on the interior test.

So: do 1 first, and only reach for 2 if the design actually calls for fat outlines.
