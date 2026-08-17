using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// How far the navigable region reaches either side of a point, along one direction across it.
    ///
    /// Kept as two independent extents rather than a single width because the two questions separate:
    /// the deformation field can only seed a symmetric bar today, so it asks for
    /// <see cref="symmetricWidth"/>, while a corridor-aware seed would want the halves apart. Measuring
    /// both from the start costs nothing and means the asymmetric version needs no second entry point.
    /// </summary>
    public struct EDCorridorExtent
    {
        /// <summary>
        /// Distance from the probe origin to the first boundary crossing along +across.
        ///
        /// MaxValue means the probe crossed nothing on that side - unbounded, which is emphatically
        /// not the same as zero. Zero means a wall passes through the origin.
        /// </summary>
        public double positive;
        public double negative;

        public bool hasPositive => positive != double.MaxValue;
        public bool hasNegative => negative != double.MaxValue;

        /// <summary>False only when neither side found anything at all, which is a failed measurement.</summary>
        public bool isMeasured => (hasPositive) || (hasNegative);

        /// <summary>
        /// The longest bar centred on the probe origin that stays inside the corridor: twice the
        /// nearer wall.
        ///
        /// Symmetric because that is all FullDeformationField.AddDeformationNode can express - it
        /// seeds along a bar centred on the frame position - and taking the nearer wall rather than
        /// the mean so the bar cannot reach past a wall on the narrow side. An unbounded side simply
        /// loses the Min, which is the answer rather than a special case: finding nothing on one side
        /// is exactly what stops it being the nearer wall.
        /// </summary>
        public double symmetricWidth => (isMeasured) ? (2.0 * Math.Min(positive, negative)) : (0.0);

        public static EDCorridorExtent unmeasured => new EDCorridorExtent
        {
            positive = double.MaxValue,
            negative = double.MaxValue
        };
    }

    public partial class EmbededDeformation
    {
        /// <summary>
        /// Numerical slack on the plane test, in world units. Only there so a crossing sitting exactly
        /// on the probe origin is not rejected by its own rounding - the cone below opens far faster
        /// than this at any distance that matters, so this constant does not decide anything.
        /// </summary>
        private const double corridorPlaneEpsilon = 1e-3;

        /// <summary>
        /// Nothing steeper than this may be asked of the cone, whatever maxSlope is set to. tan blows
        /// up at 90 degrees and a vertical limit means "accept every height", which is not a band.
        /// </summary>
        private const double corridorMaxConeAngle = 89.0;

        /// <summary>
        /// How far the navigable region reaches either side of <paramref name="center"/>, measured
        /// along <paramref name="across"/> against the navmesh boundary.
        ///
        /// This is the same measurement <see cref="GetClearance"/> makes - distance from a point to
        /// the walls that are not openings - resolved per side and along a stated direction instead of
        /// minimised over everything. That is deliberate: it is the entry point a corridor-aware
        /// clearance would also use.
        ///
        /// **The direction is a parameter rather than read off the node.** A node with more than two
        /// neighbours has no meaningful "across", and the intended answer there is an omnidirectional
        /// width - several probes at this one, minimised. Taking the direction in is what makes that a
        /// loop at the call site rather than a second function.
        ///
        /// **The corridor is measured on the navmesh, and the navmesh is already inset by the agent
        /// radius.** So this returns the *navigable* width, narrower than the visible corridor by an
        /// agent radius on each side. That is the same convention clearance uses and it is the one
        /// wanted here; it is not a shortfall to be corrected.
        ///
        /// <paramref name="state"/> null measures the rest geometry, which is what the field build
        /// needs: it runs inside Build, before BuildNavigationData, so there are no bindings to deform
        /// through yet and the rest vertices are the rest vertices. Pass a state to measure the
        /// deformed navmesh, with nodeFrames when the field is doing the deforming.
        /// </summary>
        internal bool TryMeasureCorridor(DVector3 center, DVector3 across, DVector3 up,
                                         EDStateView? state, List<FullDeformationField.Frame> nodeFrames,
                                         out EDCorridorExtent extent)
        {
            extent = EDCorridorExtent.unmeasured;

            // Not isNavConfigured: that additionally requires the per-segment bindings, which do not
            // exist while the field is being built - and this is the one caller that matters.
            if ((navMeshTopology == null) || (navMeshTopology.edgeCount == 0)) return false;
            if ((restVertices == null) || (restVertices.Length == 0)) return false;

            // The rest vertices have to be the navmesh's own, since the edge indices below index into
            // them. They are for every builder whose topologySource is the navmesh, which is every
            // builder that has any business asking this - but a geometry-sampled graph would index
            // one mesh's edges into another mesh's vertices, and that is an out-of-range crash on a
            // good day and a silent wrong answer on a bad one.
            if (restVertices.Length < navMeshTopology.vertexCount)
            {
                Debug.LogError($"TryMeasureCorridor: the rest geometry has {restVertices.Length} vertices but the navmesh topology has {navMeshTopology.vertexCount}. The corridor is measured against the navmesh, so it can only be measured on a graph built over it.");
                return false;
            }

            // Deforming needs one of the two routes to a deformed vertex. Without either, a caller
            // asking for a deformed measurement would silently get rest positions back.
            if ((state != null) && (nodeFrames == null) && (bindings == null))
            {
                Debug.LogError("TryMeasureCorridor was asked for a deformed measurement with neither node frames nor bindings. Build the navigation data first.");
                return false;
            }

            if (!BuildProbeBasis(across, up, out DVector3 r, out DVector3 f, out DVector3 n)) return false;

            // The band the crossing's height off the tangent plane must fall inside, as a cone rather
            // than a slab: tight at the origin, where a wrong crossing would do real damage, and
            // opening at the surface's own steepest permitted rate further out. A slab of any fixed
            // thickness rejects the far wall of a ramp, which is a wall this must find.
            double coneSlope = Math.Tan(Math.Min(maxSlope, corridorMaxConeAngle) * Mathf.Deg2Rad);

            double bestPositive = double.MaxValue;
            double bestNegative = double.MaxValue;

            foreach (var edge in navMeshTopology.edges)
            {
                if (!edge.isBoundary) continue;

                // An opening is a way out of the piece, not a wall across it - the same exclusion
                // clearance makes, for the same reason.
                if (IsOpeningEdge(edge)) continue;

                DVector3 a = CorridorEdgePoint(edge.vertices.i1, state, nodeFrames) - center;
                DVector3 b = CorridorEdgePoint(edge.vertices.i2, state, nodeFrames) - center;

                double va = DVector3.Dot(a, f);
                double vb = DVector3.Dot(b, f);

                // Half-open straddle test: a vertex sitting exactly on the probe line counts as
                // negative. That is what stops two edges meeting at such a vertex both reporting a
                // crossing - exactly one of them does.
                if ((va > 0.0) == (vb > 0.0)) continue;

                // Safe without a guard: only opposite signs, or one exact zero against a nonzero,
                // reach here, and both give a nonzero denominator.
                double t = va / (va - vb);

                double u = DVector3.Dot(a, r) + t * (DVector3.Dot(b, r) - DVector3.Dot(a, r));
                double h = DVector3.Dot(a, n) + t * (DVector3.Dot(b, n) - DVector3.Dot(a, n));

                if (Math.Abs(h) > (corridorPlaneEpsilon + Math.Abs(u) * coneSlope)) continue;

                if (u >= 0.0)
                {
                    if (u < bestPositive) bestPositive = u;
                }
                else
                {
                    if (-u < bestNegative) bestNegative = -u;
                }
            }

            extent.positive = bestPositive;
            extent.negative = bestNegative;

            return extent.isMeasured;
        }

        /// <summary>
        /// An orthonormal probe frame built from the two directions the caller has an opinion about.
        ///
        /// Built here rather than taken as three axes because the crossing test is only a crossing
        /// test if the basis is orthogonal: u and v come from projecting onto r and f, and a caller
        /// handing in a node frame whose axes have drifted out of square would move the probe line
        /// off the direction it asked to measure along, silently.
        /// </summary>
        private static bool BuildProbeBasis(DVector3 across, DVector3 up, out DVector3 r, out DVector3 f, out DVector3 n)
        {
            const double epsilon = 1e-12;

            r = DVector3.right;
            f = DVector3.forward;
            n = DVector3.up;

            if ((across.sqrMagnitude < epsilon) || (up.sqrMagnitude < epsilon)) return false;

            n = up.normalized;

            // The surface normal is authoritative, as it is everywhere else a frame is built here, so
            // the probe direction is the one that gets projected.
            DVector3 projected = across - DVector3.Dot(across, n) * n;

            if (projected.sqrMagnitude < epsilon) return false;

            r = projected.normalized;
            f = DVector3.Cross(n, r);

            return (f.sqrMagnitude >= epsilon);
        }

        private DVector3 CorridorEdgePoint(int vertexIndex, EDStateView? state, List<FullDeformationField.Frame> nodeFrames)
        {
            DVector3 rest = restVertices[vertexIndex];

            if (state == null) return rest;

            // A default binding is only ever reached with nodeFrames present, where
            // DeformClearancePoint goes through the field and ignores the binding entirely - the
            // entry guard rejects the combination that would actually need it.
            EDVertexBinding binding = (bindings != null) ? (bindings[vertexIndex]) : (default);

            return DeformClearancePoint(rest, binding, state.Value, nodeFrames);
        }
    }
}
#endif
