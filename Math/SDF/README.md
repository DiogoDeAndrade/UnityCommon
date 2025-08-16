# SDF mesher with marching cubes

- To do:
  - PathXYZ (Curve) => Will be needed for generic lathes and extrusions (for profiles and the path)
  - SDF Curve => The actual SDF based on a path and a profile
  - SDF Noise
    - Different method -> Add noise / Warp noise
    - Different types of noise - Perlin, Simplex, Worley, Celular, fBM    
  - Improve blends over surfaces (instead of min/max)
  - Lattice noise with different grid size (allows to preserve resolution but have less noisy distortion)
