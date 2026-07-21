using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UC
{

    public static partial class MeshTools
    {
        public static Mesh SimplifyMesh(Mesh sourceMesh, float quality)
        {
            // Create our mesh simplifier and setup our entire mesh in it
            var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
            meshSimplifier.Initialize(sourceMesh);

            // This is where the magic happens, lets simplify!
            meshSimplifier.SimplifyMesh(quality);

            // Create our final mesh and apply it back to our mesh filter
            return meshSimplifier.ToMesh();
        }

        public static Mesh SimplifyMeshImproved(Mesh sourceMesh, float quality)
        {
            if (sourceMesh == null) throw new ArgumentNullException(nameof(sourceMesh));

            if (!sourceMesh.isReadable)
            {
                Debug.LogError($"Cannot simplify mesh '{sourceMesh.name}': the mesh is not readable.");

                return null;
            }

            var options = UnityMeshSimplifier.SimplificationOptions.Default;

            // Protect the mesh silhouette and open connector boundaries.
            options.PreserveBorderEdges = true;

            // Avoid collapsing across texture discontinuities.
            options.PreserveUVSeamEdges = true;
            options.PreserveUVFoldoverEdges = true;

            // Particularly relevant after deformation: curved areas should carry a higher collapse cost than locally planar areas.
            options.PreserveSurfaceCurvature = true;

            // Treat coincident imported vertices as one geometric location while retaining their separate normals, UVs and other attributes.
            options.EnableSmartLink = true;

            // Start by linking only effectively identical positions.
            options.VertexLinkDistance = double.Epsilon;

            // These control whether the algorithm manages to reach the target, rather than directly defining the desired result.
            options.MaxIterationCount = 100;
            options.Agressiveness = 7.0;

            var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier
            {
                SimplificationOptions = options
            };

            // Options must be assigned before initialization because smart linking and UV handling are configured during initialization.
            meshSimplifier.Initialize(sourceMesh);
            meshSimplifier.SimplifyMesh(Mathf.Clamp01(quality));

            Mesh result = meshSimplifier.ToMesh();

            result.name = (sourceMesh.name.Contains("Simplified")) ? (sourceMesh.name) : ($"{sourceMesh.name} Simplified");

            return result;
        }
    }
}