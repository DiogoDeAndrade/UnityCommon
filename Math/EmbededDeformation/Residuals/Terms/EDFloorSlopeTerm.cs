using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UC.DoubleMath;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Penalises the corridor floor between the nodes for being steeper than a walker can climb -
    /// the floor itself, sampled as the floor ribbon (see <see cref="EDFloorRibbon"/>) and carried
    /// through the field the way the geometry is, rather than the frames the nodes carry. Built
    /// 2026-09-25 for the slope item after the ribbon, as a measure, read the solve's two walls:
    /// the top of the piece's own 45 degree ramp driven to 90 by the rise the request adds at its
    /// terminal, and the flat approach to another terminal rising in a wall just before the
    /// connector - both free under the structure slope term, which scores each node's frame tilt
    /// and so is indifferent to a rise carried by translation between level frames.
    ///
    /// One row per ribbon triangle: the structure slope term's normalized hinge on the cosine of
    /// the triangle's normal against up - zero at the limit less the soft band, one at the limit,
    /// growing beyond - with the limit per triangle the larger of the setting and the triangle's
    /// rest slope, so a floor the piece was modelled with is never charged and only what the
    /// deformation added to it is. A ramp modelled at the limit cannot absorb any rise in place,
    /// and the sum of squared hinges then prefers the rise spread onto the runs before and after
    /// it, which is what the one honest hand pose did by hand.
    ///
    /// The Jacobian is finite differences, one column at a time through the field with a node
    /// override on a per-worker blender - the corridor and cross-section terms' machinery - over
    /// the nodes a triangle's three rest corners are influenced by and no others, at a step that
    /// is not one float ulp of the field (the cross-section term's lesson), with the early-out on
    /// a row inside the band, which is exact for a hinge that is identically zero there. By link
    /// blend only the link's two nodes' columns can matter.
    /// </summary>
    [Serializable]
    [PolymorphicName("Floor Slope")]
    public class EDFloorSlopeTerm : EDResidualTerm
    {
        [SerializeField, Tooltip("How the floor is sampled: the ribbon along every link, its tessellation, widths, carrier and limit. The floor slope measure hosts the same settings, so the exported columns and the rows the solver sees are the same triangles.")]
        private EDFloorRibbonSettings ribbon = new EDFloorRibbonSettings();

        [SerializeField, Min(0.0f), Tooltip("How far below a triangle's limit the penalty starts rising, in degrees, so the constraint has a soft edge rather than a cliff - as on the structure slope term.")]
        private float softBand = 5.0f;

        [SerializeField, Min(1e-9f), Tooltip("The finite-difference step per parameter, scaled by the parameter's magnitude. The ribbon is carried through the float field, where 1e-6 is one ulp of a point a few units from its node - quantisation rather than a derivative - so this is 1e-3, as the cross-section term learned.")]
        private float finiteDifferenceStep = 1e-3f;

        public override string name => "floorSlope";

        /// <summary>
        /// The one sanctioned driver-write: for the measure that hosts a weight-zero instance over its
        /// own settings, and for the schedule runner, which swaps a run's tessellation in before Build
        /// and restores the snapshot afterwards. Takes effect at the next instance Reset.
        /// </summary>
        public void SetRibbonSettings(EDFloorRibbonSettings settings) => ribbon = settings ?? new EDFloorRibbonSettings();

        /// <summary>The settings in force, for a driver that snapshots and restores them. Copy before keeping.</summary>
        public EDFloorRibbonSettings ribbonSettings => ribbon;

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new SlopeInstance(this, deformation);

        public sealed class SlopeInstance : Instance
        {
            private readonly EDFloorSlopeTerm   slopeTerm;

            private EDFloorRibbon               ribbon;
            private double[]                    hardCosine = Array.Empty<double>();     // cos(limit) per triangle
            private double[]                    softCosine = Array.Empty<double>();     // cos(limit - band) per triangle

            public SlopeInstance(EDFloorSlopeTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                slopeTerm = term;
            }

            /// <summary>The ribbon as built, for a display or a dump that wants the term's own triangles. Null before Reset.</summary>
            public EDFloorRibbon floorRibbon => ribbon;

            /// <summary>What Reset built, for the Build line and the measure's status.</summary>
            public string summary => (ribbon != null) ? (ribbon.summary) : ("not built");

            public override void Reset()
            {
                ribbon = new EDFloorRibbon(deformation, slopeTerm.ribbon);

                int count = ribbon.triangleCount;

                hardCosine = new double[count];
                softCosine = new double[count];

                for (int t = 0; t < count; t++)
                {
                    // The band starts where the setting's band starts or at the triangle's rest
                    // slope, whichever is steeper, and is the band's full width from there - so a
                    // floor the piece was modelled with is exactly free, and the band keeps its
                    // width above it. The first form put the band below the per-triangle limit,
                    // which charged the piece's own 44 degree ramp at rest (inside the 40-45 band
                    // of its 45 degree limit) and flattened it to 40 on the first run (2026-09-25).
                    double band = Math.Max(0.0, slopeTerm.softBand);
                    double soft = Math.Max(ribbon.settingLimit - band, ribbon.RestSlopeDegrees(t));
                    double hard = soft + Math.Max(band, 0.5);

                    hardCosine[t] = Math.Cos(Math.Min(hard, 90.0) * Math.PI / 180.0);
                    softCosine[t] = Math.Cos(Math.Min(soft, 90.0) * Math.PI / 180.0);
                }

                Debug.Log($"[ED] {term.name}: {ribbon.summary}; band {slopeTerm.softBand.ToString("F1", CultureInfo.InvariantCulture)} deg, FD step {slopeTerm.finiteDifferenceStep.ToString("G3", CultureInfo.InvariantCulture)}.");
            }

            protected override int ComputeRowCount() => (ribbon != null) ? (ribbon.triangleCount) : (0);

            // ---------------------------------------------------------------- the row

            /// <summary>
            /// Normalized hinge on the cosine, C1 at its start: v is 0 at the band's start and 1 at
            /// the limit, and the penalty is v squared over two across the band and v - 1/2 beyond
            /// it - the cross-section term's shape. Half the structure slope term's value at the
            /// limit, the same slope as it past the limit, and a smooth onset.
            ///
            /// Both halves were learned the hard way on 2026-09-25. The onset: the structure slope
            /// term's hinge is linear from its start, and with the band starting at each triangle's
            /// rest slope every ramp row sat exactly on that kink at rest, where the float field's
            /// rounding leaves a residual just above the early-out; a finite difference there is
            /// one-sided, so the first step saw a stiffness against steepening the ramp with no
            /// gradient behind it, satisfied the terminals another way and landed in a state the
            /// solver never left. The slope: the first C1 form kept the value 1 at the limit by
            /// going 2v - 1 beyond it, twice the old hinge's slope on walls that sit far beyond the
            /// band, and that term tore the field at the fourth press (213 to 1,589 inverted
            /// tetrahedra in one accepted step) and never recovered. A weight carries across from
            /// the structure slope term's beyond the limit, where the walls are, not at it.
            /// </summary>
            private double Penalty(int t, double cosine)
            {
                double denom = Math.Max(softCosine[t] - hardCosine[t], 1e-12);
                double v = (softCosine[t] - cosine) / denom;

                if (v <= 0.0) return 0.0;

                return (v < 1.0) ? (0.5 * v * v) : (v - 0.5);
            }

            private double EvaluateRow(int t, EDStateView state, double w, FullDeformationField.TransformBlender blender)
                => w * Penalty(t, ribbon.CosineAt(t, state, blender));

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                var blender = ribbon.BlenderFor(state);

                for (int t = 0; t < rowCount; t++)
                    residual[rowOffset + t] = EvaluateRow(t, state, residualWeight, blender);
            }

            // ---------------------------------------------------------------- the Jacobian

            public override bool supportsParallelRows => true;

            /// <summary>One blender per worker: the finite-difference path overrides a node's frame on it.</summary>
            public override object CreateRowScratch(EDState state)
            {
                if (!ribbon.carriesThroughField) return null;

                return deformation.CreateFieldBlender(new EDStateView(state));
            }

            public override double FillJacobianRow(EDState state, DenseMatrix jacobian, int rowOffset, int localIndex, object scratch)
                => FillRow(state, jacobian, rowOffset + localIndex, localIndex, residualWeight, scratch as FullDeformationField.TransformBlender);

            private double FillRow(EDState state, DenseMatrix J, int row, int t, double w, FullDeformationField.TransformBlender blender)
            {
                var baseView = new EDStateView(state);

                if ((ribbon.carriesThroughField) && (blender == null))
                    blender = deformation.CreateFieldBlender(baseView);

                double r0 = EvaluateRow(t, baseView, w, blender);

                if (Math.Abs(r0) <= 1e-12) return 0.0;

                int[] nodes = ribbon.ColumnNodes(t);
                double localJNorm = 0.0;

                if (nodes == null)
                {
                    for (int col = 0; col < state.Count; col++)
                        localJNorm += Column(state, J, row, t, w, blender, col, r0);
                }
                else
                {
                    foreach (int node in nodes)
                        for (int p = 0; p < 12; p++)
                            localJNorm += Column(state, J, row, t, w, blender, node * 12 + p, r0);
                }

                return localJNorm;
            }

            /// <summary>One finite difference: the perturbed node's frame overridden on the blender, cleared in a finally as everywhere else.</summary>
            private double Column(EDState state, DenseMatrix J, int row, int t, double w, FullDeformationField.TransformBlender blender, int col, double r0)
            {
                if (col >= state.Count) return 0.0;

                int node = col / 12;
                double originalParameter = state.Get(col);
                double eps = slopeTerm.finiteDifferenceStep * Math.Max(1.0, Math.Abs(originalParameter));

                var modifiedState = new EDStateView(state, col, eps);

                if (blender != null)
                    blender.SetNodeOverride(node, deformation.GetNodeFrame(node, modifiedState));

                double r1;

                try
                {
                    r1 = EvaluateRow(t, modifiedState, w, blender);
                }
                finally
                {
                    blender?.ClearNodeOverride();
                }

                double value = (r1 - r0) / eps;

                J[row, col] = value;

                return value * value;
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                object scratch = CreateRowScratch(state);

                for (int i = 0; i < rowCount; i++)
                    jacobianNormSq += FillJacobianRow(state, jacobian, rowOffset, i, scratch);
            }

            // ---------------------------------------------------------------- columns and displays

            public override string[] DescribeHeader() => EDFloorRibbon.columns;

            /// <summary>The ribbon's reading at a state, whatever the weight - the same columns the measure exports.</summary>
            public override string[] Describe(EDStateView state)
                => (ribbon != null) ? (ribbon.Describe(state, ribbon.BlenderFor(state))) : (Array.Empty<string>());

            /// <summary>Every ribbon triangle at a state, for the gizmo. Same carry, same slopes as the rows.</summary>
            public bool TryClassifyTriangles(EDStateView state, out List<EDFloorRibbon.Triangle> triangles)
            {
                triangles = null;

                if ((ribbon == null) || (ribbon.missingReason != null)) return false;

                triangles = new List<EDFloorRibbon.Triangle>(ribbon.triangleCount);

                return ribbon.Read(state, ribbon.BlenderFor(state), triangles).triangles > 0;
            }

            /// <summary>Every triangle at a state to a text file, steepest first under a per-link summary.</summary>
            public bool TryDumpRows(EDStateView state, string path, out int written)
            {
                written = 0;

                if (ribbon == null) return false;

                return ribbon.TryDumpRows(state, path, term.name, out written);
            }
        }
#endif
    }
}
#endif
