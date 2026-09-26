using System;
using System.Collections.Generic;
using System.Globalization;
using NaughtyAttributes;
using UnityEngine;
using UC.DoubleMath;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// How the corridor floor between the nodes is sampled - the settings the floor slope term and
    /// the floor slope measure share, so the two build the same triangles and cannot disagree.
    ///
    /// Always nested - inside the term, drawn by the term drawer, or inside the measure - so every
    /// field carries [AllowNesting], without which NaughtyAttributes evaluates none of its own
    /// attributes here and a ShowIf silently draws unconditionally. And the one field a ShowIf
    /// hides carries no [Range]: a built-in drawer paints its row whatever the ShowIf decided about
    /// the height, so the two together paint over the next field (the debug settings' note); the
    /// clamp is in the accessor instead.
    /// </summary>
    [Serializable]
    public class EDFloorRibbonSettings
    {
        public enum Form
        {
            BarToNeighbour,
            Strip
        }

        public enum WidthSource
        {
            Probe,
            SeedBars
        }

        public enum Carrier
        {
            ThroughField,
            LinkBlend
        }

        [SerializeField, AllowNesting, Tooltip("What the triangles are. BarToNeighbour: each bar's triangles go to the centre point of the neighbouring sample, forward and back - at one segment per link this is exactly the bar-and-neighbour construction, the bar at each node with a triangle to each neighbouring node, every point carried the same way; tessellated, a chain of the same along the link. Strip: consecutive bars joined edge to edge into a quad strip, so a triangle spans the corridor width at both of its samples.")]
        private Form form = Form.BarToNeighbour;

        [SerializeField, Min(1), AllowNesting, Tooltip("Segments per link along the skeleton. One is the bar-and-neighbour construction: a bar at each end of the link and the triangles between them. More puts a bar at each sample along the rest link, each the corridor width measured there, so the floor between two nodes is read at that resolution.")]
        private int alongSubdivisions = 1;

        [SerializeField, Min(1), AllowNesting, Tooltip("Strips per side across the corridor, between the centreline and each edge. One is a single strip per side.")]
        private int acrossSubdivisions = 1;

        [SerializeField, AllowNesting, Tooltip("Where each bar's width comes from. Probe: the corridor probe fired at the sample along the link's across axis at rest, the measurement the field's seed bars are made from; a side the probe finds unbounded is interpolated from the bounded samples on that side. SeedBars: the two end nodes' seed bar half-lengths interpolated along the link (a junction has none), which needs a deformation field.")]
        private WidthSource widthSource = WidthSource.Probe;

        [SerializeField, ShowIf(nameof(usesProbe)), AllowNesting, Tooltip("The probe's height band when measuring the widths, in degrees (0 to 89) - a filter on which boundary crossings count as walls, as on the corridor clearance term.")]
        private float coneAngleDegrees = 30.0f;

        [SerializeField, AllowNesting, Tooltip("Cut the rest ribbon at the deformation field's cell-centre planes, so every triangle lies inside one trilinear region of the field and its normal is exact there - the output mesh's grid cut applied to the ribbon. Needs a field and is ignored without one. The summary reports the rest area before and after the cut, which must agree.")]
        private bool cutAtCellPlanes = false;

        [SerializeField, AllowNesting, Tooltip("How a rest vertex of the ribbon is carried to the state. ThroughField: through the deformation field, the way the navmesh walls and the output mesh are carried, a blend of several nodes at every point. LinkBlend: the two end nodes' own maps blended linearly by the vertex's position along the link, which is what the along-skeleton metric would make of the link; the difference between the two carriers is the current field's handoff. Falls back to LinkBlend when there is no field.")]
        private Carrier carrier = Carrier.ThroughField;

        [SerializeField, Range(0.0f, 90.0f), AllowNesting, Tooltip("The slope, in degrees, above which a triangle is over the limit. Per triangle the limit is the larger of this and the triangle's slope at rest, so a floor the piece was modelled with is never counted or charged - only what the deformation added to it.")]
        private float slopeLimit = 45.0f;

        private bool usesProbe => widthSource == WidthSource.Probe;

        public Form         ribbonForm => form;
        public int          along => Math.Max(1, alongSubdivisions);
        public int          across => Math.Max(1, acrossSubdivisions);
        public WidthSource  widths => widthSource;
        public float        coneAngle => Mathf.Clamp(coneAngleDegrees, 0.0f, 89.0f);
        public bool         cut => cutAtCellPlanes;
        public Carrier      carrierMode => carrier;
        public float        limit => Mathf.Clamp(slopeLimit, 0.0f, 90.0f);

        /// <summary>An independent copy - every field is a value, so a member-wise copy is a full one.</summary>
        public EDFloorRibbonSettings Clone() => (EDFloorRibbonSettings)MemberwiseClone();

        /// <summary>
        /// A copy with the form and the tessellation replaced and every other setting kept - what
        /// the schedule runner hands the floor slope term for a run that varies only how the floor
        /// is sampled (queue §26d).
        /// </summary>
        public EDFloorRibbonSettings WithTessellation(Form newForm, int newAlong, int newAcross)
        {
            var copy = Clone();

            copy.form = newForm;
            copy.alongSubdivisions = Math.Max(1, newAlong);
            copy.acrossSubdivisions = Math.Max(1, newAcross);

            return copy;
        }
    }

    /// <summary>
    /// The corridor floor between the nodes as a ribbon swept along every link of the graph, and
    /// the slope of every triangle of it at a state. Designed with Diogo on 2026-09-25 as the
    /// slope item's instrument and then as its term's rows: the structure slope term scores each
    /// node's frame tilt, so a rise carried by translation with level frames costs it nothing and
    /// the floor between two nodes at different heights is a wall nothing prices. This reads that
    /// floor, the way a game reads it - the angle between a floor patch's normal and up, whatever
    /// direction a walker crosses the patch. The angle is signed by the patch's upward face: each
    /// triangle's winding is oriented at rest so its normal points up, and a patch that has folded
    /// over at a state reads past vertical rather than as the gentle slope its underside makes with
    /// up. Until 2026-09-26 the cosine was taken absolute, which let a fold at the top of the ramp
    /// read as a satisfied floor and put a corner at 90 degrees with the wrong sign beyond it.
    ///
    /// Diogo's construction generalised: at one segment per link, a bar across the corridor at
    /// each end of the link and the triangles from each bar to the other end's centre point;
    /// tessellated, a strip of bars at samples along the rest link, each the corridor width
    /// measured there by the same probe the field seeds its bars with, each side of the centreline
    /// its own strip. Every vertex is a rest point carried to the state like the geometry is, so no
    /// triangle depends on one node's transform alone and the node point itself - the one place a
    /// single transform is applied exactly - is never a vertex. The optional cut at the field's
    /// cell-centre planes puts every triangle inside one trilinear region, the arrangement the
    /// grid cut gives the output mesh, so the reading is exact per region rather than a function
    /// of a count.
    ///
    /// Built once against a deformation and read at any state. Shared by the term, whose rows it
    /// is, and by the measure, which hosts a weight-zero term - the same triangles either way.
    /// </summary>
    public sealed class EDFloorRibbon
    {
        /// <summary>One ribbon triangle at a state, for drawing: its deformed corners and what it read.</summary>
        public struct Triangle
        {
            public Vector3  a, b, c;
            public float    slope;          // degrees, at the state
            public float    restSlope;      // degrees, at rest
            public float    limit;          // degrees, this triangle's own
            public bool     overLimit;
            public int      side;           // +1 / -1
            public int      nodeA;          // the link's nodes
            public int      nodeB;
        }

        public struct Reading
        {
            public int      triangles;
            public double   maxSlope;
            public int      maxSlopeLink;
            public int      maxSlopeSide;
            public double   meanSlope;
            public double   maxExcess;
            public int      maxExcessLink;
            public int      overLimit;
            public double   areaOverLimit;
        }

        public static readonly string[] columns =
        {
            "linksMeasured", "trianglesMeasured", "maxSlope", "maxSlopeNodeA", "maxSlopeNodeB", "maxSlopeSide", "meanSlope",
            "maxExcess", "maxExcessNodeA", "maxExcessNodeB", "trianglesOverLimit", "areaOverLimit", "restMaxSlope",
        };

        private readonly EmbededDeformation         deformation;
        private readonly EDFloorRibbonSettings      settings;

        // The links, every one once, lower index first.
        private readonly int[]                      linkA = Array.Empty<int>();
        private readonly int[]                      linkB = Array.Empty<int>();
        private readonly DVector3[]                 linkDir = Array.Empty<DVector3>();      // rest, unit
        private readonly double[]                   linkLength = Array.Empty<double>();     // rest

        // The ribbon at rest: three corners per triangle, and what each triangle is.
        private readonly DVector3[]                 rest = Array.Empty<DVector3>();
        private readonly int[]                      triLink = Array.Empty<int>();
        private readonly int[]                      triSide = Array.Empty<int>();
        private readonly double[]                   restArea = Array.Empty<double>();
        private readonly double[]                   restSlope = Array.Empty<double>();      // degrees
        private readonly double[]                   limit = Array.Empty<double>();          // degrees, max(setting, rest)
        private readonly int[][]                    columnNodes = Array.Empty<int[]>();     // per triangle, the nodes its carry depends on; null = every node

        private readonly int                        linksMeasured;
        private readonly int                        linksUnmeasured;
        private readonly int                        probesUnbounded;
        private readonly int                        linksOneSided;
        private readonly int                        trianglesBeforeCut;
        private readonly double                     restAreaBeforeCut;
        private readonly double                     restAreaTotal;
        private readonly double                     restMaxSlope;
        private readonly int                        restMaxLink = -1;
        private readonly bool                       hasField;
        private readonly bool                       throughField;
        private readonly bool                       cutApplied;

        /// <summary>Why nothing could be built, or null when the ribbon exists.</summary>
        public string missingReason { get; }

        public int triangleCount => triLink.Length;
        public int linkCount => linksMeasured;
        public bool carriesThroughField => throughField;
        public double restMaximumSlope => restMaxSlope;
        public double settingLimit => settings.limit;

        public EDFloorRibbon(EmbededDeformation deformation, EDFloorRibbonSettings settings)
        {
            this.deformation = deformation;
            this.settings = settings;

            var nodes = deformation.nodes;

            if ((nodes == null) || (nodes.Count == 0))
            {
                missingReason = "no graph on this configuration";
                return;
            }

            var field = deformation.GetDeformationField();

            hasField = (field != null) && (deformation.UseDeformationFieldForClearance);
            throughField = (hasField) && (settings.carrierMode == EDFloorRibbonSettings.Carrier.ThroughField);

            // Every link once.
            var la = new List<int>();
            var lb = new List<int>();

            for (int i = 0; i < nodes.Count; i++)
            {
                var neighbours = nodes[i].neighbors;

                if (neighbours == null) continue;

                foreach (int j in neighbours)
                    if ((j > i) && (j < nodes.Count)) { la.Add(i); lb.Add(j); }
            }

            if (la.Count == 0)
            {
                missingReason = "the graph has no links";
                return;
            }

            linkA = la.ToArray();
            linkB = lb.ToArray();
            linkDir = new DVector3[linkA.Length];
            linkLength = new double[linkA.Length];

            int m = settings.along;
            int n = settings.across;
            double scale = deformation.effectiveCorridorWidthScale;
            DVector3 worldUp = deformation.upVectorD;

            var corners = new List<DVector3>();
            var triLinkOut = new List<int>();
            var triSideOut = new List<int>();

            var centre = new DVector3[m + 1];
            var across = new DVector3[m + 1];
            var halfPositive = new double[m + 1];
            var halfNegative = new double[m + 1];

            for (int k = 0; k < linkA.Length; k++)
            {
                int a = linkA[k], b = linkB[k];
                DVector3 pa = nodes[a].restPosition, pb = nodes[b].restPosition;
                DVector3 d = pb - pa;
                double length = d.magnitude;

                if (length < 1e-9)
                {
                    linksUnmeasured++;
                    continue;
                }

                DVector3 dir = d / length;

                linkDir[k] = dir;
                linkLength[k] = length;

                // The seed bars' half-lengths at the two ends, for the SeedBars source and as the
                // fallback when the probe finds no wall anywhere along a link.
                double seedA = SeedHalf(field, a, scale), seedB = SeedHalf(field, b, scale);

                for (int s = 0; s <= m; s++)
                {
                    double t = (double)s / m;

                    DVector3 c = pa + d * t;
                    DVector3 u = nodes[a].restUp * (1.0 - t) + nodes[b].restUp * t;

                    if (u.sqrMagnitude < 1e-12) u = worldUp;

                    // The link's own frame: across perpendicular to the link and to the floor,
                    // up re-closed so the probe's height band is square to both.
                    DVector3 ac = DVector3.Cross(u, dir);

                    if (ac.sqrMagnitude < 1e-12) ac = DVector3.Cross(worldUp, dir);
                    if (ac.sqrMagnitude < 1e-12) ac = DVector3.Cross(new DVector3(1.0, 0.0, 0.0), dir);

                    ac = ac.normalized;

                    DVector3 uo = DVector3.Cross(dir, ac).normalized;

                    centre[s] = c;
                    across[s] = ac;

                    double seed = Lerp(seedA, seedB, t);

                    if (settings.widths == EDFloorRibbonSettings.WidthSource.Probe)
                    {
                        if (deformation.TryMeasureCorridor(c, ac, uo, null, null, settings.coneAngle, out EDCorridorExtent extent))
                        {
                            halfPositive[s] = extent.positive;
                            halfNegative[s] = extent.negative;
                        }
                        else
                        {
                            halfPositive[s] = double.MaxValue;
                            halfNegative[s] = double.MaxValue;
                        }

                        if (halfPositive[s] == double.MaxValue) probesUnbounded++;
                        if (halfNegative[s] == double.MaxValue) probesUnbounded++;
                    }
                    else
                    {
                        halfPositive[s] = seed;
                        halfNegative[s] = seed;
                    }
                }

                // Unbounded samples take the bounded ones' widths along the link; a side bounded
                // nowhere takes the other side's; nothing bounded at all falls back to the seed
                // bars, and a link with no width anywhere is not measured.
                bool positiveAny = FillUnbounded(halfPositive);
                bool negativeAny = FillUnbounded(halfNegative);

                if ((!positiveAny) && (!negativeAny))
                {
                    if ((seedA == double.MaxValue) && (seedB == double.MaxValue))
                    {
                        linksUnmeasured++;
                        continue;
                    }

                    for (int s = 0; s <= m; s++)
                    {
                        halfPositive[s] = Lerp(seedA, seedB, (double)s / m);
                        halfNegative[s] = halfPositive[s];
                    }
                }
                else if (!positiveAny)
                {
                    Array.Copy(halfNegative, halfPositive, m + 1);
                    linksOneSided++;
                }
                else if (!negativeAny)
                {
                    Array.Copy(halfPositive, halfNegative, m + 1);
                    linksOneSided++;
                }

                if (settings.ribbonForm == EDFloorRibbonSettings.Form.Strip)
                {
                    // The quads between consecutive bars, each side its own strips, two triangles each.
                    for (int s = 0; s < m; s++)
                    {
                        for (int j = -n; j < n; j++)
                        {
                            int side = (j >= 0) ? (+1) : (-1);

                            DVector3 v00 = Vertex(centre[s], across[s], halfPositive[s], halfNegative[s], j, n);
                            DVector3 v01 = Vertex(centre[s], across[s], halfPositive[s], halfNegative[s], j + 1, n);
                            DVector3 v10 = Vertex(centre[s + 1], across[s + 1], halfPositive[s + 1], halfNegative[s + 1], j, n);
                            DVector3 v11 = Vertex(centre[s + 1], across[s + 1], halfPositive[s + 1], halfNegative[s + 1], j + 1, n);

                            corners.Add(v00); corners.Add(v10); corners.Add(v11);
                            triLinkOut.Add(k); triSideOut.Add(side);

                            corners.Add(v00); corners.Add(v11); corners.Add(v01);
                            triLinkOut.Add(k); triSideOut.Add(side);
                        }
                    }
                }
                else
                {
                    // Each bar's triangles to the neighbouring samples' centres: forward to the next
                    // sample and back to the previous one, each side its own fan. At one segment
                    // per link the bar at each node and the triangle to each neighbouring node -
                    // the construction as drawn - and every span of the link is covered twice,
                    // once from each of its bars, which the rest-area weighting treats alike.
                    for (int s = 0; s <= m; s++)
                    {
                        for (int j = -n; j < n; j++)
                        {
                            int side = (j >= 0) ? (+1) : (-1);

                            DVector3 v0 = Vertex(centre[s], across[s], halfPositive[s], halfNegative[s], j, n);
                            DVector3 v1 = Vertex(centre[s], across[s], halfPositive[s], halfNegative[s], j + 1, n);

                            if (s < m)
                            {
                                corners.Add(v0); corners.Add(v1); corners.Add(centre[s + 1]);
                                triLinkOut.Add(k); triSideOut.Add(side);
                            }

                            if (s > 0)
                            {
                                corners.Add(v1); corners.Add(v0); corners.Add(centre[s - 1]);
                                triLinkOut.Add(k); triSideOut.Add(side);
                            }
                        }
                    }
                }

                linksMeasured++;
            }

            trianglesBeforeCut = triLinkOut.Count;

            for (int t = 0; t < trianglesBeforeCut; t++)
                restAreaBeforeCut += Area(corners[3 * t], corners[3 * t + 1], corners[3 * t + 2]);

            if ((settings.cut) && (field != null) && (field.cellSize.x > 1e-6f))
            {
                CutAtCellPlanes(corners, triLinkOut, triSideOut, field.minBound + 0.5f * field.cellSize, field.cellSize.x);
                cutApplied = true;
            }

            int count = triLinkOut.Count;

            if (count == 0)
            {
                missingReason = "no ribbon could be built - no corridor width on any link";
                return;
            }

            rest = corners.ToArray();
            triLink = triLinkOut.ToArray();
            triSide = triSideOut.ToArray();
            restArea = new double[count];
            restSlope = new double[count];
            limit = new double[count];
            columnNodes = new int[count][];

            for (int t = 0; t < count; t++)
            {
                // The winding is oriented so the rest normal points up, and the cosine is signed
                // from here on: the same corner order is carried to every state, so a triangle
                // that folds over reads a negative cosine, past vertical. A triangle vertical at
                // rest has no upward face to orient by and keeps the order it was built with.
                if (DVector3.Dot(DVector3.Cross(rest[3 * t + 1] - rest[3 * t], rest[3 * t + 2] - rest[3 * t]), worldUp) < 0.0)
                    (rest[3 * t + 1], rest[3 * t + 2]) = (rest[3 * t + 2], rest[3 * t + 1]);

                Measure(rest[3 * t], rest[3 * t + 1], rest[3 * t + 2], worldUp, out restArea[t], out double cosine);

                restSlope[t] = Degrees(cosine);
                restAreaTotal += restArea[t];
                limit[t] = Math.Max(settings.limit, restSlope[t]);

                if (restSlope[t] > restMaxSlope)
                {
                    restMaxSlope = restSlope[t];
                    restMaxLink = triLink[t];
                }

                columnNodes[t] = ColumnNodesFor(field, t);
            }
        }

        // ------------------------------------------------------------------ per-triangle facts

        public int TriangleLink(int t) => triLink[t];
        public int TriangleSide(int t) => triSide[t];
        public int LinkNodeA(int link) => linkA[link];
        public int LinkNodeB(int link) => linkB[link];
        public double RestArea(int t) => restArea[t];
        public double RestSlopeDegrees(int t) => restSlope[t];
        public double LimitDegrees(int t) => limit[t];

        /// <summary>
        /// The nodes a triangle's carry can depend on, for a finite-difference Jacobian to skip the
        /// rest: through the field, the union of the trilinear influences at its three rest
        /// corners; by link blend, the link's two nodes. Null when the field could not say, which
        /// means every column.
        /// </summary>
        public int[] ColumnNodes(int t) => columnNodes[t];

        private int[] ColumnNodesFor(FullDeformationField field, int t)
        {
            if (!throughField)
                return new[] { linkA[triLink[t]], linkB[triLink[t]] };

            var union = new HashSet<int>();

            for (int c = 0; c < 3; c++)
            {
                if (!field.TryGetTrilinearInfluences(rest[3 * t + c].ToVector3(), out int[] ids, out _)) return null;

                foreach (int id in ids) union.Add(id);
            }

            var result = new int[union.Count];

            union.CopyTo(result);
            Array.Sort(result);

            return result;
        }

        // ------------------------------------------------------------------ reading a state

        /// <summary>The blender a state is carried through, or null when the ribbon is carried by link blend.</summary>
        public FullDeformationField.TransformBlender BlenderFor(EDStateView state)
            => (throughField) ? (deformation.CreateFieldBlender(state)) : (null);

        /// <summary>
        /// One triangle at a state: the signed cosine of its normal against up (negative once the
        /// triangle has folded over), and its corners. A collapsed triangle reads as a wall
        /// (cosine zero).
        /// </summary>
        public double CosineAt(int t, EDStateView state, FullDeformationField.TransformBlender blender, out DVector3 a, out DVector3 b, out DVector3 c)
        {
            int k = triLink[t];

            a = Carry(rest[3 * t], k, state, blender);
            b = Carry(rest[3 * t + 1], k, state, blender);
            c = Carry(rest[3 * t + 2], k, state, blender);

            Measure(a, b, c, deformation.upVectorD, out _, out double cosine);

            return cosine;
        }

        public double CosineAt(int t, EDStateView state, FullDeformationField.TransformBlender blender)
            => CosineAt(t, state, blender, out _, out _, out _);

        /// <summary>Every triangle at a state: the aggregate the columns report, and each triangle if a record is given.</summary>
        public Reading Read(EDStateView state, FullDeformationField.TransformBlender blender, List<Triangle> record)
        {
            var result = new Reading { maxSlopeLink = -1, maxExcessLink = -1, maxExcess = double.NegativeInfinity };

            double areaSlope = 0.0;
            double areaOver = 0.0;

            for (int t = 0; t < triLink.Length; t++)
            {
                int k = triLink[t];

                double slope = Degrees(CosineAt(t, state, blender, out DVector3 a, out DVector3 b, out DVector3 c));

                bool over = slope > limit[t];
                double excess = slope - restSlope[t];

                result.triangles++;
                areaSlope += restArea[t] * slope;

                if (slope > result.maxSlope)
                {
                    result.maxSlope = slope;
                    result.maxSlopeLink = k;
                    result.maxSlopeSide = triSide[t];
                }

                if (excess > result.maxExcess)
                {
                    result.maxExcess = excess;
                    result.maxExcessLink = k;
                }

                if (over)
                {
                    result.overLimit++;
                    areaOver += restArea[t];
                }

                record?.Add(new Triangle
                {
                    a = a.ToVector3(), b = b.ToVector3(), c = c.ToVector3(),
                    slope = (float)slope, restSlope = (float)restSlope[t], limit = (float)limit[t],
                    overLimit = over, side = triSide[t], nodeA = linkA[k], nodeB = linkB[k],
                });
            }

            result.meanSlope = (restAreaTotal > 0.0) ? (areaSlope / restAreaTotal) : (0.0);
            result.areaOverLimit = (restAreaTotal > 0.0) ? (areaOver / restAreaTotal) : (0.0);

            if (result.maxExcessLink < 0) result.maxExcess = 0.0;

            return result;
        }

        /// <summary>The exported columns at a state, in the order of <see cref="columns"/>; empty when there is nothing to read.</summary>
        public string[] Describe(EDStateView state, FullDeformationField.TransformBlender blender)
        {
            if (missingReason != null) return Array.Empty<string>();

            var r = Read(state, blender, null);

            if (r.triangles == 0) return Array.Empty<string>();

            return new[]
            {
                linksMeasured.ToString(CultureInfo.InvariantCulture),
                r.triangles.ToString(CultureInfo.InvariantCulture),
                r.maxSlope.ToString("F4", CultureInfo.InvariantCulture),
                NodeOf(r.maxSlopeLink, linkA),
                NodeOf(r.maxSlopeLink, linkB),
                r.maxSlopeSide.ToString(CultureInfo.InvariantCulture),
                r.meanSlope.ToString("F4", CultureInfo.InvariantCulture),
                r.maxExcess.ToString("F4", CultureInfo.InvariantCulture),
                NodeOf(r.maxExcessLink, linkA),
                NodeOf(r.maxExcessLink, linkB),
                r.overLimit.ToString(CultureInfo.InvariantCulture),
                r.areaOverLimit.ToString("F6", CultureInfo.InvariantCulture),
                restMaxSlope.ToString("F4", CultureInfo.InvariantCulture),
            };
        }

        /// <summary>What was built, for the Build line and the export's status.</summary>
        public string summary
        {
            get
            {
                if (missingReason != null) return $"{missingReason} - nothing to read";

                var ci = CultureInfo.InvariantCulture;

                string carried = (throughField) ? ("through the field") : ((settings.carrierMode == EDFloorRibbonSettings.Carrier.ThroughField) ? ("by link blend, no field to carry through") : ("by link blend"));
                string widths = (settings.widths == EDFloorRibbonSettings.WidthSource.Probe)
                    ? ($"widths probed at cone {settings.coneAngle.ToString("F0", ci)} deg, {probesUnbounded} sides unbounded and interpolated, {linksOneSided} links one-sided")
                    : ("widths from the seed bars");
                string cut = (settings.cut)
                    ? ((cutApplied)
                        ? ($", cut at the cell planes into {triLink.Length} triangles from {trianglesBeforeCut} (rest area {restAreaTotal.ToString("F4", ci)} against {restAreaBeforeCut.ToString("F4", ci)} before the cut)")
                        : (", cut asked for but there is no field to cut against"))
                    : ("");
                string unmeasured = (linksUnmeasured > 0) ? ($", {linksUnmeasured} links without a width left out") : ("");
                string restMax = (restMaxLink >= 0)
                    ? ($"rest max slope {restMaxSlope.ToString("F2", ci)} deg at link {linkA[restMaxLink]}-{linkB[restMaxLink]}")
                    : ("rest max slope 0");

                return $"{linksMeasured} links x {settings.along} along x {2 * settings.across} across = {triLink.Length} triangles ({settings.ribbonForm}), carried {carried}, {widths}{cut}{unmeasured}; {restMax}, rest area {restAreaTotal.ToString("F4", ci)}, limit {settings.limit.ToString("F0", ci)} deg or the triangle's rest slope";
            }
        }

        /// <summary>
        /// Every triangle at a state to a text file, steepest first, under a per-link summary: which
        /// links carry the over-limit area is what the columns cannot say, and a crushed floor reads
        /// as steep as a rising one, so a pose that reads worse than the solve on the count may be
        /// worse on the ramp or worse somewhere it folded.
        /// </summary>
        public bool TryDumpRows(EDStateView state, string path, string owner, out int written)
        {
            written = 0;

            if (missingReason != null) return false;

            var triangles = new List<Triangle>(triLink.Length);

            if (Read(state, BlenderFor(state), triangles).triangles == 0) return false;

            int links = linkA.Length;
            var linkMax = new double[links];
            var linkExcess = new double[links];
            var linkRestMax = new double[links];
            var linkOverArea = new double[links];
            var linkArea = new double[links];
            var linkCount = new int[links];

            for (int t = 0; t < triangles.Count; t++)
            {
                int k = triLink[t];
                Triangle tri = triangles[t];

                linkCount[k]++;
                linkArea[k] += restArea[t];
                linkMax[k] = Math.Max(linkMax[k], tri.slope);
                linkExcess[k] = Math.Max(linkExcess[k], tri.slope - tri.restSlope);
                linkRestMax[k] = Math.Max(linkRestMax[k], tri.restSlope);

                if (tri.overLimit) linkOverArea[k] += restArea[t];
            }

            var linkOrder = new List<int>();

            for (int k = 0; k < links; k++)
                if (linkCount[k] > 0) linkOrder.Add(k);

            linkOrder.Sort((x, y) => linkMax[y].CompareTo(linkMax[x]));

            var triangleOrder = new List<int>(triangles.Count);

            for (int t = 0; t < triangles.Count; t++) triangleOrder.Add(t);

            triangleOrder.Sort((x, y) => triangles[y].slope.CompareTo(triangles[x].slope));

            var ci = CultureInfo.InvariantCulture;

            using (var w = new System.IO.StreamWriter(path, false))
            {
                w.WriteLine($"floorSlope rows: {owner}, {summary}");
                w.WriteLine($"form {settings.ribbonForm} along {settings.along} across {settings.across} widths {settings.widths} carrier {((throughField) ? ("ThroughField") : ("LinkBlend"))} limit {settings.limit.ToString("F1", ci)}");
                w.WriteLine();
                w.WriteLine("links, steepest first: nodeA nodeB triangles restMaxSlope maxSlope maxExcess areaOverLimit(fraction of the link's rest area)");

                foreach (int k in linkOrder)
                {
                    double overFraction = (linkArea[k] > 0.0) ? (linkOverArea[k] / linkArea[k]) : (0.0);

                    w.WriteLine($"link {linkA[k]} {linkB[k]} {linkCount[k]} {linkRestMax[k].ToString("F3", ci)} {linkMax[k].ToString("F3", ci)} {linkExcess[k].ToString("F3", ci)} {overFraction.ToString("F4", ci)}");
                }

                w.WriteLine();
                w.WriteLine("triangles, steepest first: nodeA nodeB side restSlope slope excess limit over restArea restCentroid deformedCentroid");

                foreach (int t in triangleOrder)
                {
                    Triangle tri = triangles[t];
                    DVector3 restCentroid = (rest[3 * t] + rest[3 * t + 1] + rest[3 * t + 2]) / 3.0;
                    Vector3 deformedCentroid = (tri.a + tri.b + tri.c) / 3.0f;

                    w.WriteLine($"tri {tri.nodeA} {tri.nodeB} {tri.side} {tri.restSlope.ToString("F3", ci)} {tri.slope.ToString("F3", ci)} {(tri.slope - tri.restSlope).ToString("F3", ci)} {tri.limit.ToString("F3", ci)} {((tri.overLimit) ? (1) : (0))} {restArea[t].ToString("F5", ci)} {Fmt(restCentroid.ToVector3(), ci)} {Fmt(deformedCentroid, ci)}");

                    written++;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>
        /// A rest point to the state: through the field when there is one and the carrier asks for
        /// it, otherwise the two end nodes' maps blended linearly by the point's position along the
        /// rest link - at the ends the node's own map, in the middle an even blend. The position
        /// along the link is read off the rest point rather than stored, so a vertex the cut made
        /// is carried the same way as one the tessellation made.
        /// </summary>
        private DVector3 Carry(DVector3 restPoint, int link, EDStateView state, FullDeformationField.TransformBlender blender)
        {
            if (blender != null)
                return deformation.DeformClearancePoint(restPoint, default, state, blender);

            var nodes = deformation.nodes;
            int a = linkA[link], b = linkB[link];
            double s = DVector3.Dot(restPoint - nodes[a].restPosition, linkDir[link]) / linkLength[link];

            s = Math.Max(0.0, Math.Min(1.0, s));

            DVector3 byA = state.DeformVertex(a, restPoint, nodes[a].restPosition);
            DVector3 byB = state.DeformVertex(b, restPoint, nodes[b].restPosition);

            return byA * (1.0 - s) + byB * s;
        }

        /// <summary>
        /// A triangle's area and the signed cosine of its normal against up, in the winding it is
        /// given - the rest orientation faces up, so a negative cosine is a triangle folded over. A
        /// collapsed triangle reads as a wall.
        /// </summary>
        private static void Measure(DVector3 a, DVector3 b, DVector3 c, DVector3 up, out double area, out double cosine)
        {
            DVector3 normal = DVector3.Cross(b - a, c - a);
            double length = normal.magnitude;

            area = 0.5 * length;

            if (length < 1e-14)
            {
                cosine = 0.0;
                return;
            }

            cosine = Math.Max(-1.0, Math.Min(1.0, DVector3.Dot(normal, up) / length));
        }

        /// <summary>The slope in degrees, 0 to 180: past 90 the triangle has folded over.</summary>
        public static double Degrees(double cosine) => Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosine))) * 180.0 / Math.PI;

        private static double Area(DVector3 a, DVector3 b, DVector3 c)
            => 0.5 * DVector3.Cross(b - a, c - a).magnitude;

        private static DVector3 Vertex(DVector3 centre, DVector3 across, double halfPositive, double halfNegative, int j, int n)
        {
            double fraction = (double)j / n;
            double half = (j >= 0) ? (halfPositive) : (halfNegative);

            return centre + across * (fraction * half);
        }

        private static double SeedHalf(FullDeformationField field, int nodeIndex, double scale)
        {
            if ((field == null) || (nodeIndex < 0) || (nodeIndex >= field.deformationNodeCount)) return double.MaxValue;

            float sourceLength = field.GetDeformationNode(nodeIndex).sourceLength;

            return (sourceLength > 0.0f) ? (0.5 * sourceLength * scale) : (double.MaxValue);
        }

        private static double Lerp(double a, double b, double t)
        {
            if (a == double.MaxValue) return b;
            if (b == double.MaxValue) return a;

            return a + (b - a) * t;
        }

        /// <summary>
        /// Replaces every unbounded entry (MaxValue) by a linear interpolation between the nearest
        /// bounded entries either side, or the nearest one alone past the ends. False, and the
        /// array untouched, when nothing in it is bounded.
        /// </summary>
        private static bool FillUnbounded(double[] values)
        {
            int count = values.Length;
            int any = -1;

            for (int i = 0; i < count; i++)
                if (values[i] != double.MaxValue) { any = i; break; }

            if (any < 0) return false;

            for (int i = 0; i < count; i++)
            {
                if (values[i] != double.MaxValue) continue;

                int left = i - 1, right = i + 1;

                while ((left >= 0) && (values[left] == double.MaxValue)) left--;
                while ((right < count) && (values[right] == double.MaxValue)) right++;

                if ((left >= 0) && (right < count))
                    values[i] = values[left] + (values[right] - values[left]) * (double)(i - left) / (right - left);
                else if (left >= 0)
                    values[i] = values[left];
                else
                    values[i] = values[right];
            }

            return true;
        }

        /// <summary>
        /// Clips every triangle at the planes x, y, z = origin + k * spacing that pass through it,
        /// axis by axis, both sides kept (Sutherland-Hodgman), and fan-triangulates the convex
        /// pieces. A corner within a small fraction of the spacing of a plane counts as on it and
        /// never splits, so a vertex on a plane makes no zero-area sliver. The link and side of a
        /// piece are its parent's. Checked outside Unity on 2000 random triangle sets: area
        /// conserved per link, every piece inside one cell to the snap band.
        /// </summary>
        private static void CutAtCellPlanes(List<DVector3> corners, List<int> links, List<int> sides, Vector3 origin, double spacing)
        {
            var outCorners = new List<DVector3>(corners.Count * 2);
            var outLinks = new List<int>(links.Count * 2);
            var outSides = new List<int>(sides.Count * 2);
            var pieces = new List<List<DVector3>>();
            var next = new List<List<DVector3>>();
            double epsilon = 1e-5 * spacing;

            for (int t = 0; t < links.Count; t++)
            {
                pieces.Clear();
                pieces.Add(new List<DVector3> { corners[3 * t], corners[3 * t + 1], corners[3 * t + 2] });

                for (int axis = 0; axis < 3; axis++)
                {
                    double lo = Math.Min(Component(corners[3 * t], axis), Math.Min(Component(corners[3 * t + 1], axis), Component(corners[3 * t + 2], axis)));
                    double hi = Math.Max(Component(corners[3 * t], axis), Math.Max(Component(corners[3 * t + 1], axis), Component(corners[3 * t + 2], axis)));
                    double o = Component(origin, axis);

                    int first = (int)Math.Ceiling((lo - o) / spacing);
                    int last = (int)Math.Floor((hi - o) / spacing);

                    for (int k = first; k <= last; k++)
                    {
                        double plane = o + k * spacing;

                        if ((plane <= lo + epsilon) || (plane >= hi - epsilon)) continue;

                        next.Clear();

                        foreach (var piece in pieces)
                        {
                            var below = new List<DVector3>(piece.Count + 2);
                            var above = new List<DVector3>(piece.Count + 2);

                            Split(piece, axis, plane, epsilon, below, above);

                            if (below.Count >= 3) next.Add(below);
                            if (above.Count >= 3) next.Add(above);
                        }

                        (pieces, next) = (next, pieces);
                    }
                }

                foreach (var piece in pieces)
                {
                    for (int i = 1; i + 1 < piece.Count; i++)
                    {
                        if (Area(piece[0], piece[i], piece[i + 1]) < 1e-14) continue;

                        outCorners.Add(piece[0]); outCorners.Add(piece[i]); outCorners.Add(piece[i + 1]);
                        outLinks.Add(links[t]);
                        outSides.Add(sides[t]);
                    }
                }
            }

            corners.Clear(); corners.AddRange(outCorners);
            links.Clear(); links.AddRange(outLinks);
            sides.Clear(); sides.AddRange(outSides);
        }

        private static void Split(List<DVector3> polygon, int axis, double plane, double epsilon, List<DVector3> below, List<DVector3> above)
        {
            int count = polygon.Count;

            for (int i = 0; i < count; i++)
            {
                DVector3 p = polygon[i], q = polygon[(i + 1) % count];
                double dp = Component(p, axis) - plane;
                double dq = Component(q, axis) - plane;

                if (Math.Abs(dp) < epsilon) dp = 0.0;
                if (Math.Abs(dq) < epsilon) dq = 0.0;

                if (dp <= 0.0) below.Add(p);
                if (dp >= 0.0) above.Add(p);

                if (((dp < 0.0) && (dq > 0.0)) || ((dp > 0.0) && (dq < 0.0)))
                {
                    DVector3 x = p + (q - p) * (dp / (dp - dq));

                    below.Add(x);
                    above.Add(x);
                }
            }
        }

        private static double Component(DVector3 v, int axis) => (axis == 0) ? (v.x) : ((axis == 1) ? (v.y) : (v.z));

        private static double Component(Vector3 v, int axis) => (axis == 0) ? (v.x) : ((axis == 1) ? (v.y) : (v.z));

        private static string NodeOf(int link, int[] ends)
            => (link >= 0) ? (ends[link].ToString(CultureInfo.InvariantCulture)) : ("-1");

        private static string Fmt(Vector3 v, CultureInfo ci)
            => $"{v.x.ToString("F4", ci)} {v.y.ToString("F4", ci)} {v.z.ToString("F4", ci)}";
    }
}
#endif
