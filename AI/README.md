# NavMesh2d

* This is work in progress
  * Currently it builds a "voxel" field from 2d static colliders in the scene.
  * It then extract the boundaries and holes
  * These boundary/holes can be simplified either by using a greedy vertex decimater, or RDP (Ramer-Douglas-Peucker algorithm)
  * [MadWorldNL's Earcut](https://github.com/MadWorldNL/EarCut) is then used to convert these to a triangle mesh
  * This mesh can then be merged into convex polygons, using Hertel-Mehlhorn algorithm
  * Can query the navmesh for point in navmesh - it uses a quadtree for efficient querying
    * It can search within a region, or just any region
  * It can query the navmesh for the polygon path between two points in a region
  * Can do pathfinding queries, with or without funneling, and with or without biasing
  * NavMesh2d can now do LoS queries (raycast)
  * There's two follow modes: pursuit (which is more physical, based on acceleration and velocity, and has some parameters to slow down in tight turns, etc), and direct (in which we follow the path, with a possible offset)
  * Paths can be smoothed using cubic beziers at corners, with checks for LoS to stop the curve from going inside the walls.
  * Pathfinding can now account for costs, using class NavMeshModifier2d, you can condition the cost of the parts of the environment - this is now done through the use of NavMeshTerrainType2d, which can be used to override costs by agent
    * Imagine an agent that moves faster on water - this way it's easier to implement costs in a hierarchical fashion
  * Agent types can be hierarchical, since it's mostly required to guarantee that the right NavMesh can be found for an agent.
* Still to do:
  * Pathfinding
    * Offmesh links - Doors (conditionals)
    * Offmesh links - Holes (link fields)
    * Obstacle avoidance
    * Multithreaded generation and queries
      * Concept of dynamic path - a path that can be changed over time based on changing situations on the map
    * Dynamic meshes (carving)
  * I'm not happy with the convex polygons generated, there's usually a lot of small, thin triangles.
    * It might be that I'm asking too much, Recast has thin triangles sometimes and it works fine - so far, it doesn't seem to be a problem, although I believe it can reduce the quality of the pathfinding solution, especially with costs, etc.
    * Options
      * Instead of doing triangulation->merge, I can try doing the same as Recast: rasterize the boundary at a certain resolution, and each strip/scanline try to merge with the previous one, guaranteeing convexity. Not a fan of this approach, I see a lot of thinkering, and the thin triangles are also present there
      * I can try to implement Bayazit's algorithm - there's no C# code available, and I'm even having difficulty finding in other languages - this generates convex polygons from the boundaries, so there's no triangle step; this doesn't support holes directly, but I can support them by using a system similar to what we do with EarCut to bridge the holes. The problem on this one is that there's no reference implementation and I'll have to do everything before I can actually check if this fixes my issues or if it stays the same.
      * Use Delauney triangulation, which is quite a challenge - Delauney allows to specify parameters to ensure max-area/min-angle triangulation. I can at least try to see if this solves my issues by using Triangle.NET, and then find out if I can actually use Triangle.NET (there might be gray areas on the licenses)
  * Probably the best approach would be to try Triangle.NET and/or get the pathfinding to work so I can see if this is actually a problem or not, it might not be.