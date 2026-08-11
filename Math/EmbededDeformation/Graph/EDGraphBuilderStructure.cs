using System.Collections.Generic;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Takes the deformation graph straight from the skeleton: every structure segment endpoint
    /// becomes a node and every segment becomes an edge.
    ///
    /// Neither sampling nor a linking strategy applies here, which is why this builder exposes
    /// neither - the skeleton already answers both questions. What it adds instead is a volumetric
    /// deformation field, which is how geometry is deformed in this mode.
    ///
    /// It also exposes no binding settings. Bindings are still built, because the node transforms
    /// have to reach the navmesh vertices somehow, but nothing that produces a result reads them in
    /// this mode: geometry goes through the field, clearance measures through the field, and the
    /// slope, orientation and segment-length terms all work from node frames and node indices. A
    /// fixed nearest-node binding is therefore enough, and exposing a knob that cannot change a
    /// result would only invite tuning it.
    /// </summary>
    [CreateAssetMenu(fileName = "EDGraphBuilderStructure", menuName = "Unity Common/ED/Graph Builder/Structure Skeleton")]
    public class EDGraphBuilderStructure : EDGraphBuilder
    {
        [SerializeField, Min(0.0f), Tooltip("Deformation field voxel size, as a fraction of the bounding box.")]
        private float fieldVoxelDensity = 0.05f;
        [SerializeField, Min(1), Tooltip("How many nodes may influence a single field cell.")]
        private int fieldMaxWeights = 4;
        [SerializeField]
        private bool useTerminalLength = true;

        private static readonly EDBindingConfig fixedBinding = new EDBindingConfig();

        public override DeformationGraphSource graphSource => DeformationGraphSource.StructureOnly;
        public override EDBindingConfig binding => fixedBinding;
        public override float deformationFieldVoxelDensity => fieldVoxelDensity;
        public override int deformationFieldMaxWeights => fieldMaxWeights;

        public override Instance NewInstance(EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav) => new StructureInstance(this, deformation, structureSource, nav);

        public class StructureInstance : Instance
        {
            public StructureInstance(EDGraphBuilderStructure builder, EmbededDeformation deformation, IEDStructureSource structureSource, EDNavQueries nav)
                : base(builder, deformation, structureSource, nav)
            {
            }

            /// <summary>
            /// Two structure endpoints closer together than this are the same node. Not a sampling
            /// distance - the skeleton decides where nodes go - only a guard against a shared
            /// junction arriving as two coincident points from two segments.
            /// </summary>
            private const float structureNodeMergeDistanceSq = 1e-8f;

            public override void Build(List<int> forcedVertices)
            {
                var def = (EDGraphBuilderStructure)builder;

                TopologyStatic topology = ResolveTopology();
                if (topology == null) return;

                deformation.BeginGraphBuild(DeformationGraphSource.StructureOnly);

                deformation.BuildStructure(structureSource, def.maxSegmentLength, nav.tryGetSurfaceNormal);

                var structure = deformation.structure;

                if ((structure == null) || (structure.Count == 0))
                {
                    Debug.LogError("Structure graph build failed: structure is null or empty.");
                    return;
                }

                // -----------------------------------------------------------------
                // 1) Copy source navmesh into ED rest data.
                //    Even in StructureOnly mode, the mesh still needs to deform.
                // -----------------------------------------------------------------
                deformation.SetRestGeometry(topology);

                // -----------------------------------------------------------------
                // 2) Build ED graph directly from structure segment endpoints.
                // -----------------------------------------------------------------
                for (int i = 0; i < structure.Count; i++)
                {
                    var seg = structure[i];

                    int idx1 = deformation.AddGraphNode(seg.p1, structureNodeMergeDistanceSq);
                    int idx2 = deformation.AddGraphNode(seg.p2, structureNodeMergeDistanceSq);

                    seg.node1 = idx1;
                    seg.node2 = idx2;

                    deformation.LinkGraphNodes(idx1, idx2);
                }

                if (deformation.nodes.Count == 0)
                {
                    Debug.LogError("Structure graph build failed: no nodes were created.");
                    return;
                }

                // -----------------------------------------------------------------
                // 3) Bind navmesh vertices to the structure graph.
                // -----------------------------------------------------------------
                deformation.SetGraphBindings(topology, def.binding, def.sampleMinDistance);

                deformation.EndGraphBuild();

                Debug.Log($"ED structure-only graph built. " +
                          $"Vertices={topology.vertexCount}, " +
                          $"Triangles={topology.triangleCount}, " +
                          $"StructureSegments={structure.Count}, " +
                          $"Nodes={deformation.nodes.Count}, " +
                          $"Edges={deformation.graphEdgeCount}");

                // -----------------------------------------------------------------
                // 4) Everything that only means something on a skeleton graph.
                // -----------------------------------------------------------------
                deformation.BuildNodeRestFrames();
                deformation.BuildLinkAngleConstraints();

                BuildDeformationField(def);
            }

            /// <summary>
            /// The bar length to seed each node along, indexed by node. Zero everywhere except the
            /// terminal nodes a connector attaches to, which seed along their whole cross-section.
            ///
            /// A skeleton graph puts exactly one node at a connector, so a vertex on the connector
            /// cross-section is otherwise reached by geodesic distance from that single point - and
            /// a vertex out at the edge of the bar is measurably further from it than one at the
            /// centre, so it gets a blend where it should be controlled almost entirely by its
            /// terminal. Seeding the whole bar at distance zero removes the asymmetry without
            /// adding a variable or a per-vertex assignment.
            ///
            /// The sampled builders need none of this: every vertex the connector holds is already
            /// forced to be a node of its own and constrained directly, and they build no field.
            ///
            /// Terminals are matched by the same GetClosestLeafNodeIndex the terminal constraints
            /// use, so the node seeded along a bar is the node that bar goes on to drive.
            /// </summary>
            private float[] ResolveConnectorWidths()
            {
                float[] widths = new float[deformation.nodes.Count];

                var seeds = deformation.GetConnectorSeeds();

                if (seeds == null) return widths;

                for (int i = 0; i < seeds.Count; i++)
                {
                    EDConnectorSeed seed = seeds[i];

                    if (seed.width <= 0.0f) continue;

                    int nodeIndex = deformation.GetClosestLeafNodeIndex(seed.restPosition);

                    if ((nodeIndex < 0) || (nodeIndex >= widths.Length)) continue;

                    // Two connectors landing on one node means the skeleton has no terminal to tell
                    // them apart, and the terminal constraints are in the same trouble. Keep the
                    // wider bar rather than whichever came last, and say so - a silent overwrite
                    // here would show up only as a field that is subtly wrong at one connector.
                    if (widths[nodeIndex] > 0.0f)
                    {
                        Debug.LogWarning($"Two connectors both resolve to leaf node {nodeIndex}. Seeding it with the wider bar; the terminal constraints on it will also be in conflict.");

                        widths[nodeIndex] = Mathf.Max(widths[nodeIndex], seed.width);
                        continue;
                    }

                    widths[nodeIndex] = seed.width;
                }

                return widths;
            }

            /// <summary>
            /// Voxelizes the source geometry and seeds it with the graph nodes, so that a point
            /// anywhere in the solid is carried by geodesic distance through the volume rather than
            /// by straight-line distance through whatever wall happens to be between it and a node.
            /// </summary>
            private void BuildDeformationField(EDGraphBuilderStructure def)
            {
                DebugProfiler timer = new();
                timer.Mark();

                List<Mesh> sourceGeometry = deformation.GetSourceGeometry();

                if ((sourceGeometry == null) || (sourceGeometry.Count == 0))
                {
                    Debug.LogWarning("BuildDeformationField failed: no source geometry was provided.");
                    deformation.SetDeformationField(null);
                    return;
                }

                var nodes = deformation.nodes;

                if ((nodes == null) || (nodes.Count == 0))
                {
                    Debug.LogWarning("BuildDeformationField failed: no deformation graph nodes exist.");
                    deformation.SetDeformationField(null);
                    return;
                }

                // -------------------------------------------------------------
                // 1) Compute bounds from baked source geometry.
                //    Important: Bounds is a struct, so do not use bounds.Value.Encapsulate().
                // -------------------------------------------------------------
                bool hasBounds = false;
                Bounds bounds = default;

                for (int i = 0; i < sourceGeometry.Count; i++)
                {
                    Mesh mesh = sourceGeometry[i];
                    if (mesh == null)
                        continue;

                    if (!hasBounds)
                    {
                        bounds = mesh.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(mesh.bounds);
                    }
                }

                if (!hasBounds)
                {
                    Debug.LogWarning("BuildDeformationField failed: source geometry has no valid meshes.");
                    deformation.SetDeformationField(null);
                    return;
                }

                // Make sure deformation graph nodes are inside the field bounds.
                for (int i = 0; i < nodes.Count; i++)
                {
                    bounds.Encapsulate(nodes[i].restPosition.ToVector3());
                }

                float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

                if (maxSize <= 1e-6f)
                {
                    Debug.LogWarning("BuildDeformationField failed: source geometry bounds are degenerate.");
                    deformation.SetDeformationField(null);
                    return;
                }

                float safeDensity = Mathf.Max(def.fieldVoxelDensity, 1e-5f);
                float voxelSize = maxSize * safeDensity;

                int safeMaxWeights = Mathf.Clamp(def.fieldMaxWeights, 1, nodes.Count);

                // -------------------------------------------------------------
                // 2) Create deformation field.
                // -------------------------------------------------------------
                FullDeformationField field = new FullDeformationField(voxelSize, safeMaxWeights);

                // -------------------------------------------------------------
                // 3) Fill the field using source geometry.
                //
                //    sourceGeometry is already baked to world space in SetSourceGeometry(),
                //    so the voxelizer should receive identity matrices here.
                // -------------------------------------------------------------
                List<Matrix4x4> identityMatrices = new();

                for (int i = 0; i < sourceGeometry.Count; i++)
                {
                    identityMatrices.Add(Matrix4x4.identity);
                }

                field.FillWithMesh(sourceGeometry, identityMatrices);

                // -------------------------------------------------------------
                // 4) Add deformation graph nodes as volumetric/geodesic seeds.
                //
                //    Add them in ED node order so the deformation field node id matches the ED node index.
                // -------------------------------------------------------------
                float[] connectorWidths = ResolveConnectorWidths();

                for (int i = 0; i < nodes.Count; i++)
                {
                    EDNode node = nodes[i];

                    float connectorWidth = (def.useTerminalLength) ? (connectorWidths[i]) : (0.0f);
                    field.AddDeformationNode(node.restPosition.ToVector3(), node.restRight.ToVector3(), node.restUp.ToVector3(), node.restForward.ToVector3(), connectorWidth);
                }

                // -------------------------------------------------------------
                // 5) Extend the influence field outside occupied cells.
                //
                //    The occupied volume gets geodesic distances from AddDeformationNode(). GrowInfluence() lets nearby empty cells also query valid weights.
                // -------------------------------------------------------------
                field.GrowInfluence();

                // -------------------------------------------------------------
                // 6) Convert distances into normalized weights.
                // -------------------------------------------------------------
                field.ComputeWeights(safeMaxWeights);
                field.BuildTrilinearRegions();

                deformation.SetDeformationField(field);

                timer.Mark();

                Debug.Log(
                    $"Deformation field built:\n" +
                    $"  Meshes={sourceGeometry.Count}\n " +
                    $"  Nodes={nodes.Count}\n" +
                    $"  VoxelSize={voxelSize:F4}\n" +
                    $"  MaxWeights={safeMaxWeights}\n" +
                    $"  Bounds={bounds.size}\n" +
                    $"  Grid Size={field.gridSize}\n" +
                    $"  Time={timer.accumulatedTimeMS:F6} ms"
                );
            }
        }
    }
}
#endif
