# Using this distortion effect

- Create layer for Distortion Sprites
- Create an empty object below the camera that should be affected
- Add a DistortionCamera component on it
  - Setup the culling layer and the resolution
- On any renderer object that should drive the drive the distortion, set it to the distortion layer
  - Set a material that can actually draw on that layer (usually unlit shader or something similar)
  - I recomment the DistortionSprites shader, it has a strength parameter that works better to fade the normals than messing with the alpha
  - Load textures/sprites as linear (not sRGB), results are slightly better