using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The surface a graph builder constructs through.
    ///
    /// The deformation still owns everything - nodes, bindings, structure, the field, the solver
    /// state. What moved out is the *construction*: a builder queries what it needs from here and
    /// pushes the result back through Add/Set calls, instead of the deformation taking one
    /// seventeen-parameter call whose signature was the union of every builder's needs and whose
    /// unused arguments each builder had to invent a value for.
    ///
    /// The build is bracketed by BeginGraphBuild/EndGraphBuild so the ordering that used to be
    /// implicit in one method body is stated: clear, then fill, then allocate solver state.
    /// </summary>
    public partial class EmbededDeformation
    {
        // The navmesh lives on navMeshTopology in the main partial because the nav-aware energies
        // read it directly. This is the other one: present only when the graph is built over the
        // source geometry rather than over the navmesh, which is config 2c and nothing else.
        [SerializeField, HideInInspector]
        private TopologyStatic geometryTopology;

        [SerializeField, HideInInspector]
        private float storedAgentRadius = 0.5f;

        /// <summary>
        /// Whether the per-segment probe bindings exist. This is the gate the nav-aware energies
        /// gate their row counts on, and it has to stay an explicit statement about the navigation
        /// data rather than a test on the topology: the topology is now supplied to every run so a
        /// builder can sample it, so "a navmesh is present" no longer distinguishes a navigation
        /// run from a plain-ED one. Config 3 exists precisely to catch nav energies leaking into a
        /// plain-ED run, and it would stop catching it.
        /// </summary>
        [SerializeField, HideInInspector]
        private bool navigationDataBuilt;

        /// <summary>
        /// The agent this piece is navigable for. Data rather than configuration, which is why it
        /// is set once by the owner and queried by whoever needs it, rather than travelling as an
        /// argument through the construction.
        /// </summary>
        public float agentRadius => storedAgentRadius;

        public void SetAgentParameters(float agentRadius, Vector3 upVector)
        {
            storedAgentRadius = agentRadius;

            // Matches what the construction used to do with the fallback up: a degenerate vector
            // means "no opinion", not "up is zero".
            this.upVector = (upVector.sqrMagnitude > 1e-8f) ? (upVector) : (Vector3.up);
        }

        /// <summary>
        /// Hands the deformation the meshes it may be built over. Both are supplied by the owner
        /// before the build, and the builder asks for the one matching its own topologySource - so
        /// the graph, the bindings and the vertex constraints cannot end up disagreeing about which
        /// geometry they are on.
        /// </summary>
        public void SetTopology(EDTopologySource source, TopologyStatic topology)
        {
            switch (source)
            {
                case EDTopologySource.SourceGeometry:
                    geometryTopology = topology;
                    break;

                default:
                    navMeshTopology = topology;
                    break;
            }
        }

        /// <summary>
        /// Null when that topology was never supplied. Tests the vertex data rather than the
        /// reference: TopologyStatic is [Serializable] on a [SerializeField] member, so one that
        /// was null when the scene was written comes back as a live but empty object.
        /// </summary>
        public TopologyStatic GetTopology(EDTopologySource source)
        {
            TopologyStatic ret = (source == EDTopologySource.SourceGeometry) ? (geometryTopology) : (navMeshTopology);

            return ((ret != null) && (ret.vertexCount > 0)) ? (ret) : (null);
        }

        internal List<Mesh> GetSourceGeometry() => sourceGeometry;

        // Deliberately not serialized. It is build input, read once while the field is being made
        // and meaningless afterwards, so a stale copy surviving a domain reload could only mislead.
        private List<EDConnectorSeed> connectorSeeds;

        /// <summary>
        /// Where this piece's connectors are and how wide they are, set by the owner before the
        /// build. Only the structure builder reads them, to seed the deformation field along each
        /// connector's bar rather than at a single point.
        /// </summary>
        public void SetConnectorSeeds(List<EDConnectorSeed> seeds)
        {
            connectorSeeds = (seeds != null) ? (new List<EDConnectorSeed>(seeds)) : (new List<EDConnectorSeed>());
        }

        public IReadOnlyList<EDConnectorSeed> GetConnectorSeeds() => connectorSeeds;

        // ---------------------------------------------------------------------------------------
        // Build bracket
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Discards whatever the last build produced and records how this one is being made.
        ///
        /// vertexConstraints is deliberately not cleared: it is rebuilt from the handles by
        /// UpdateConstraints, which the owner drives on its own schedule, and clearing it here
        /// would leave a run unconstrained between the build and the next handle update.
        /// </summary>
        public void BeginGraphBuild(DeformationGraphSource source)
        {
            deformationGraphSource = source;

            nodes.Clear();
            bindings = null;

            handleConstraints.Clear();
            clearanceOpenings.Clear();
            terminalConstraints.Clear();

            // Only the structure path produces one.
            deformationField = null;
            structureTree = null;

            navigationDataBuilt = false;
        }

        /// <summary>
        /// Copies the mesh being deformed into the rest data. Every mode needs this, including the
        /// ones whose graph does not come from this mesh - the mesh still has to deform.
        /// </summary>
        public void SetRestGeometry(TopologyStatic topology)
        {
            var v = topology.GetVertexPositions();

            restVertices = new DVector3[v.Count];
            for (int i = 0; i < v.Count; i++)
                restVertices[i] = v[i].ToDVector3();

            triangles = topology.GetTriangleIndices().ToArray();
        }

        /// <summary>
        /// Allocates the solver state the built graph implies. Called once the node set is final,
        /// because both states are sized by it.
        /// </summary>
        public void EndGraphBuild()
        {
            currentState = new EDState(nodes.Count);
            restState = new EDState(nodes.Count);
        }

        // ---------------------------------------------------------------------------------------
        // Nodes and links
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Adds a node, or returns the existing one within mergeToleranceSq. A tolerance of zero
        /// always adds, which is how forced vertices get in regardless of spacing.
        /// </summary>
        public int AddGraphNode(DVector3 position, float mergeToleranceSq)
        {
            if (mergeToleranceSq > 0.0f)
            {
                int index = GetSampledVertexIndex(position, mergeToleranceSq);
                if (index != -1) return index;
            }

            nodes.Add(new EDNode
            {
                restPosition = position,
                neighbors = new List<int>()
            });

            return nodes.Count - 1;
        }

        public int AddGraphNode(int vertexId, TopologyStatic topology, float mergeToleranceSq)
            => AddGraphNode(topology.GetVertexPosition(vertexId).ToDVector3(), mergeToleranceSq);

        public void LinkGraphNodes(int a, int b) => AddUndirectedNeighbor(a, b);

        public void ClearGraphLinks()
        {
            for (int i = 0; i < nodes.Count; i++)
                nodes[i].neighbors.Clear();
        }

        // ---------------------------------------------------------------------------------------
        // Bindings
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Binds a single point under a builder's binding configuration.
        ///
        /// Binding evaluation stays here rather than moving out with the rest of the construction,
        /// because it is not only a build-time operation: the scene probe and DeformMesh bind
        /// arbitrary points long after any builder instance has been discarded. What the builder
        /// owns is the configuration, which is why that arrives as an argument.
        /// </summary>
        public EDVertexBinding BindPoint(DVector3 p, EDBindingConfig config, float sampleMinDistance)
        {
            return GetBinding(p, config.selectionMode, config.weightMode, config.nearestK,
                              config.attenuationPower, config.ResolveSigma(sampleMinDistance));
        }

        /// <summary>
        /// Binds every vertex of the given topology and adopts the result.
        ///
        /// The configuration is remembered as well as applied, so that points which are not
        /// vertices of this mesh can be bound the same way later. A point bound under different
        /// settings is not the point this deformation acts on.
        /// </summary>
        public void SetGraphBindings(TopologyStatic topology, EDBindingConfig config, float sampleMinDistance)
        {
            if (topology == null)
            {
                Debug.LogError("SetGraphBindings failed: topology is null.");
                return;
            }

            if ((nodes == null) || (nodes.Count == 0))
            {
                Debug.LogError("SetGraphBindings failed: no ED nodes exist.");
                return;
            }

            RememberBindingSettings(config.selectionMode, config.weightMode, config.nearestK,
                                    config.attenuationPower, config.ResolveSigma(sampleMinDistance));

            int vertexCount = topology.vertexCount;

            bindings = new EDVertexBinding[vertexCount];

            for (int vId = 0; vId < vertexCount; vId++)
                bindings[vId] = BindPoint(topology.GetVertexPosition(vId).ToDVector3(), config, sampleMinDistance);
        }

        // ---------------------------------------------------------------------------------------
        // Navigation data
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Builds the per-segment bindings and clearance probes the nav-aware energies measure
        /// through, and computes the rest clearances they measure against.
        ///
        /// This is what SetNavEDParameters used to do, minus everything that was not actually
        /// navigation data. The topology and the agent radius arrive through their own setters
        /// before the build; the limits each travel with the energy that reads them; the binding
        /// configuration belongs to the builder and is handed in by it. What is left is this.
        ///
        /// Calling it is what makes isNavConfigured true, so it must only be called for a run that
        /// really is navigation-aware.
        /// </summary>
        public void BuildNavigationData(EDBindingConfig config, float sampleMinDistance)
        {
            if (structure == null)
            {
                Debug.LogError("BuildNavigationData failed: no structure was built.");
                return;
            }

            for (int i = 0; i < structure.Count; i++)
            {
                var seg = structure[i];

                seg.bind1 = BindPoint(seg.p1, config, sampleMinDistance);
                seg.bind2 = BindPoint(seg.p2, config, sampleMinDistance);

                // Build tangent space
                var dir = (seg.p2 - seg.p1).normalized;
                var t = DVector3.ProjectOnPlane(dir, seg.normal).normalized;
                var b = DVector3.Cross(seg.normal, t).normalized;

                float probeDistance = storedAgentRadius * 0.5f;
                seg.probeT = seg.center + probeDistance * t;
                seg.probeB = seg.center + probeDistance * b;

                seg.cBind = BindPoint(seg.center, config, sampleMinDistance);
                seg.tBind = BindPoint(seg.probeT, config, sampleMinDistance);
                seg.bBind = BindPoint(seg.probeB, config, sampleMinDistance);
            }

            navigationDataBuilt = true;

            ComputeClearance(currentState);
            ComputeClearance(restState);

            LogClearance("Original clearance:", restState, restState);
        }

        // ---------------------------------------------------------------------------------------
        // Deformation field
        // ---------------------------------------------------------------------------------------

        public void SetDeformationField(FullDeformationField field)
        {
            deformationField = field;

            // Re-arm, so the next reload warns again rather than staying quiet on the strength of a
            // build that a reload has since discarded.
            warnedFieldMissing = false;
        }
    }
}
#endif
