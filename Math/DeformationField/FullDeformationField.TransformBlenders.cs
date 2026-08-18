using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How the transforms of the nodes influencing a point are combined into the single transform that
/// acts there.
///
/// The weights decide *how much* each node says; this decides *what it means* to average what they
/// say. They are genuinely separate questions, and the distance-to-weight sweep is the evidence:
/// every mapping in EDFieldWeightMode is monotonic in distance and normalized to sum to one, so the
/// nearest node always takes the largest weight and the only thing any of their parameters controls
/// is how concentrated the blend is. Both ends of that one axis are bad - concentrated reproduces the
/// single-influence result, spread out folds - which is what points at this enum rather than at
/// another weighting function.
///
/// LinearAffine is what the field has always done and what every structure golden is captured
/// against. It is also the thing under suspicion: R1 and R2 can both be rotations while (R1 + R2)/2
/// is not one, so blending whole affine matrices shrinks and eventually inverts wherever the node
/// rotations disagree.
/// </summary>
public enum EDFieldBlendMode
{
    /// <summary>
    /// The weighted mean of the affine matrices, component by component. The original formulation,
    /// and the only mode the goldens have ever been captured under.
    /// </summary>
    LinearAffine
}

public partial class FullDeformationField
{
    /// <summary>
    /// Walks the nodes influencing a point, whichever way the field was asked to sample.
    ///
    /// A struct with no allocation and no shared state, because the clearance pass blends inside a
    /// Parallel.For and anything held on the blender would be a data race. It also collapses what
    /// used to be four near-identical gather loops - cell and trilinear, position and matrix - into
    /// one, so a blender writes its arithmetic once and cannot accidentally implement the cell case
    /// differently from the trilinear one.
    ///
    /// The guards live here rather than in the blenders because they are all facts about the field:
    /// a zero weight, a negative node id, an index past the node list. What a blender skips on top of
    /// that is a node it has no transform for, which is a fact about the blender.
    /// </summary>
    public struct InfluenceEnumerator
    {
        private readonly FullDeformationField       field;
        private readonly DeformationFieldWeights    cell;
        private readonly TrilinearRegion            region;
        private readonly float                      fx;
        private readonly float                      fy;
        private readonly float                      fz;
        private readonly bool                       trilinear;
        private readonly bool                       valid;
        private int                                 index;

        internal InfluenceEnumerator(FullDeformationField field, DeformationFieldWeights cell, bool valid)
        {
            this.field = field;
            this.cell = cell;
            this.valid = valid;

            region = default;
            fx = 0.0f;
            fy = 0.0f;
            fz = 0.0f;
            trilinear = false;
            index = 0;
        }

        internal InfluenceEnumerator(FullDeformationField field, TrilinearRegion region, float fx, float fy, float fz, bool valid)
        {
            this.field = field;
            this.region = region;
            this.fx = fx;
            this.fy = fy;
            this.fz = fz;
            this.valid = valid;

            cell = default;
            trilinear = true;
            index = region.influenceStart;
        }

        /// <summary>
        /// The next node that actually acts here, and by how much. False when there are none left,
        /// which is also the answer for a point outside the field entirely - a caller that blends
        /// nothing and falls back to the undeformed position is doing the right thing either way.
        /// </summary>
        public bool MoveNext(out int nodeIndex, out float weight)
        {
            nodeIndex = -1;
            weight = 0.0f;

            if (!valid) return false;

            if (trilinear)
            {
                int end = region.influenceStart + region.influenceCount;

                while (index < end)
                {
                    TrilinearInfluence influence = field.trilinearInfluences[index];

                    index++;

                    float w = InterpolateTrilinearWeight(influence, fx, fy, fz);
                    int n = influence.nodeId;

                    if (w <= 0.0f) continue;
                    if (n < 0) continue;
                    if (n >= field.deformationNodes.Count) continue;

                    nodeIndex = n;
                    weight = w;

                    return true;
                }

                return false;
            }

            while (index < cell.weights.Length)
            {
                float w = cell.weights[index];
                int n = cell.nodeId[index];

                index++;

                if (w <= 0.0f) continue;
                if (n < 0) continue;
                if (n >= field.deformationNodes.Count) continue;

                nodeIndex = n;
                weight = w;

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// A set of node transforms, frozen, together with the rule for combining them.
    ///
    /// **The transforms are frozen at construction and there is deliberately no way to change them.**
    /// A blender's lifetime is one deformation pass, over which the node frames are constant by
    /// construction, so there is nothing to invalidate and no way to read a stale one. That is worth
    /// more than the allocation it costs: a blender holding the previous iteration's frames would not
    /// throw, it would produce plausible geometry, which is the failure mode this codebase keeps
    /// paying for. Different transforms mean a new blender.
    ///
    /// **Sampling mode is a per-call argument, not part of the freeze.** Cell and trilinear are two
    /// questions one can ask of the same state, and the debug tools exist precisely to ask both and
    /// show the difference; baking one in would take that away.
    ///
    /// Blenders are read-only once constructed and hold no scratch, so one can be shared across the
    /// threads of a parallel clearance pass.
    ///
    /// Deliberately never serialized, for the same reason as WeightResolver: a [SerializeReference]
    /// would persist {class, ns, asm} into every asset holding one, and renaming a blender would then
    /// detach them all. The mode enum is what gets stored; the blender is behaviour.
    /// </summary>
    public abstract class TransformBlender
    {
        protected readonly FullDeformationField  field;

        /// <summary>
        /// Each node's rest-to-current transform, computed once here rather than per influence per
        /// vertex - which is what the blend sites did before, recomputing the same handful of
        /// matrices thousands of times per pass.
        ///
        /// It is a pure function of the frame and the rest inverse with no accumulation in it, so
        /// computing it once and reading it back is bit-identical to computing it every time. The
        /// cache is a speedup that cannot move a golden.
        /// </summary>
        private readonly Matrix4x4[]             nodeMatrices;

        /// <summary>
        /// Whether the frame source had anything to say about a node. Separate from the matrix
        /// because there is no matrix value that means "absent" - identity is a perfectly ordinary
        /// transform - and the blend sites this replaces all skipped such a node rather than blending
        /// something neutral for it.
        /// </summary>
        private readonly bool[]                  nodeAvailable;

        /// <summary>
        /// One node's transform, temporarily replacing the frozen one.
        ///
        /// **The single exception to the freeze, and it exists for finite differencing.** Building the
        /// clearance Jacobian nudges one parameter, re-measures, and puts it back, once per column -
        /// so it needs "this state, with node k moved" hundreds of times per segment per iteration.
        /// Deriving a whole blender for each of those would allocate a copy of every node's transform
        /// per column, which is the one thing this hot path cannot afford.
        ///
        /// An override rather than a write into the frozen array, because it cannot corrupt anything:
        /// Clear is total, so a missed restore loses a perturbation instead of quietly poisoning the
        /// base state for every column after it. The array the blend reads from is still written
        /// exactly once, in the constructor.
        ///
        /// **A blender carrying an override is owned by one thread.** The Jacobian gets one per
        /// worker, which is what makes that true there. The clearance pass shares a single blender
        /// across its workers and never overrides - those two uses must not be swapped without
        /// thinking about this field.
        /// </summary>
        private int                              overrideNode = -1;
        private Matrix4x4                        overrideMatrix;

        protected TransformBlender(FullDeformationField field, Func<int, Frame?> getCurrentNodeFrame)
        {
            this.field = field;

            int count = field.deformationNodeCount;

            nodeMatrices = new Matrix4x4[count];
            nodeAvailable = new bool[count];

            for (int i = 0; i < count; i++)
            {
                Frame? frame = getCurrentNodeFrame(i);

                if (!frame.HasValue) continue;

                nodeMatrices[i] = field.NodeDeformMatrix(i, frame.Value);
                nodeAvailable[i] = true;
            }
        }

        protected bool TryGetNodeMatrix(int nodeIndex, out Matrix4x4 matrix)
        {
            if (nodeIndex == overrideNode)
            {
                matrix = overrideMatrix;

                return true;
            }

            if ((nodeIndex < 0) || (nodeIndex >= nodeMatrices.Length) || (!nodeAvailable[nodeIndex]))
            {
                matrix = Matrix4x4.identity;
                return false;
            }

            matrix = nodeMatrices[nodeIndex];

            return true;
        }

        /// <summary>
        /// Puts one node somewhere else for the next few queries. See the override field for why this
        /// exists, and for the ownership rule that comes with it.
        ///
        /// Only one node at a time, because that is what a finite difference needs - twelve
        /// consecutive parameters belong to one node, so perturbing a parameter moves exactly one
        /// frame. A second call replaces the first rather than adding to it.
        /// </summary>
        public void SetNodeOverride(int nodeIndex, Frame frame)
        {
            if ((nodeIndex < 0) || (nodeIndex >= nodeMatrices.Length)) return;

            overrideNode = nodeIndex;
            overrideMatrix = field.NodeDeformMatrix(nodeIndex, frame);
        }

        /// <summary>
        /// Back to the frozen transforms. Total rather than paired with a particular Set, so it is
        /// safe to call in a finally without knowing whether the Set ran.
        /// </summary>
        public void ClearNodeOverride()
        {
            overrideNode = -1;
        }

        /// <summary>
        /// The blend and its parameters, short and stable, for the golden dump and for the check that
        /// refuses a comparison across a change to it. Generated from the blender rather than written
        /// beside it, so it cannot drift out of sync with what the blender does.
        /// </summary>
        public abstract string Describe();

        /// <summary>
        /// The affine transform acting at a rest-space point. False when nothing influences the point,
        /// in which case it should be left where it is.
        /// </summary>
        public abstract bool TryGetMatrix(Vector3 position, bool trilinear, out Matrix4x4 matrix);

        /// <summary>
        /// Where the deformation carries a point.
        ///
        /// Virtual, and blending once then applying is the honest default: for any blend that
        /// decomposes the transforms, "combine, then act" *is* the definition, and applying each
        /// node's transform separately before averaging would be a different operation. Only
        /// LinearAffineBlender overrides it, and only because the two are algebraically identical
        /// there - see that class for why it still has to.
        /// </summary>
        public virtual Vector3 DeformPosition(Vector3 position, bool trilinear)
        {
            if (!TryGetMatrix(position, trilinear, out Matrix4x4 matrix)) return position;

            return matrix.MultiplyPoint3x4(position);
        }

        /// <summary>
        /// Carries a frame through the deformation: its origin moves, and its axes pick up whatever
        /// rotation, scale and shear the blend produces there. The axes are transformed as vectors, so
        /// their lengths are meaningful - an axis coming back longer means the deformation stretches
        /// there.
        /// </summary>
        public Frame DeformFrame(Frame frame, bool trilinear)
        {
            if (!TryGetMatrix(frame.position, trilinear, out Matrix4x4 matrix)) return frame;

            return new Frame(matrix.MultiplyPoint3x4(frame.position),
                             matrix.MultiplyVector(frame.right),
                             matrix.MultiplyVector(frame.up),
                             matrix.MultiplyVector(frame.forward));
        }
    }

    /// <summary>
    /// The weighted mean of the affine matrices, component by component.
    ///
    /// **This is the legacy blend and it must stay bit-identical.** It is what all nine goldens were
    /// captured against, so the arithmetic below is the original arithmetic in the original order.
    ///
    /// That is also why it overrides DeformPosition rather than inheriting the base. The position
    /// sites never built a matrix: they applied each node's transform to the point and averaged the
    /// resulting displacements. Averaging the matrices and applying once is the same number in the
    /// reals - matrix application is linear in the matrix - and a different number in float. The
    /// duplication is deliberate and belongs to this class alone; every other blender inherits the
    /// base, where blending once and applying is not an optimisation but the meaning.
    /// </summary>
    public sealed class LinearAffineBlender : TransformBlender
    {
        public LinearAffineBlender(FullDeformationField field, Func<int, Frame?> getCurrentNodeFrame)
            : base(field, getCurrentNodeFrame)
        {
        }

        public override string Describe() => "linear";

        public override bool TryGetMatrix(Vector3 position, bool trilinear, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;

            Matrix4x4 blended = Matrix4x4.zero;
            float weightSum = 0.0f;

            InfluenceEnumerator influences = field.EnumerateInfluences(position, trilinear);

            while (influences.MoveNext(out int nodeIndex, out float weight))
            {
                if (!TryGetNodeMatrix(nodeIndex, out Matrix4x4 nodeMatrix)) continue;

                AccumulateMatrix(ref blended, nodeMatrix, weight);

                weightSum += weight;
            }

            if (weightSum <= DistanceEpsilon) return false;

            for (int i = 0; i < 16; i++) blended[i] /= weightSum;

            matrix = blended;

            return true;
        }

        public override Vector3 DeformPosition(Vector3 position, bool trilinear)
        {
            Vector3 displacement = Vector3.zero;
            float weightSum = 0.0f;

            InfluenceEnumerator influences = field.EnumerateInfluences(position, trilinear);

            while (influences.MoveNext(out int nodeIndex, out float weight))
            {
                if (!TryGetNodeMatrix(nodeIndex, out Matrix4x4 nodeMatrix)) continue;

                Vector3 deformedPosition = nodeMatrix.MultiplyPoint3x4(position);

                displacement += weight * (deformedPosition - position);

                weightSum += weight;
            }

            // Defensive normalization, in case the cell carries partial or invalid weights.
            if (weightSum <= DistanceEpsilon) return position;

            displacement /= weightSum;

            return position + displacement;
        }
    }

    /// <summary>
    /// A blender over the node frames as a list, where a node past the end of the list is one this
    /// blender has no transform for. Null in, null out, so a caller without frames stays on whatever
    /// path it had for that case.
    /// </summary>
    public TransformBlender CreateBlender(List<Frame> currentNodeFrames)
    {
        if (currentNodeFrames == null) return null;

        return CreateBlenderInternal((nodeIndex) => (nodeIndex < currentNodeFrames.Count) ? (currentNodeFrames[nodeIndex]) : ((Frame?)null));
    }

    /// <summary>
    /// A blender over a frame lookup, which is assumed to answer for every node - the callers that
    /// use this shape read out of the solver state rather than a snapshot.
    /// </summary>
    public TransformBlender CreateBlender(Func<int, Frame> getCurrentNodeFrame)
    {
        if (getCurrentNodeFrame == null) return null;

        return CreateBlenderInternal((nodeIndex) => (Frame?)getCurrentNodeFrame(nodeIndex));
    }

    private TransformBlender CreateBlenderInternal(Func<int, Frame?> getCurrentNodeFrame)
    {
        switch (blendMode)
        {
            default:
                return new LinearAffineBlender(this, getCurrentNodeFrame);
        }
    }

    /// <summary>
    /// The influences acting at a point, gathered the way the caller asks for them.
    ///
    /// Trilinear is what deforms geometry; the containing cell's weights are a step function across
    /// cell boundaries and exist for the tools that want to see exactly that difference.
    /// </summary>
    internal InfluenceEnumerator EnumerateInfluences(Vector3 position, bool trilinear)
    {
        if (trilinear)
        {
            EnsureTrilinearRegionsBuilt();

            if (!TryGetTrilinearRegion(position, out TrilinearRegion region, out float fx, out float fy, out float fz))
            {
                return new InfluenceEnumerator(this, default(TrilinearRegion), 0.0f, 0.0f, 0.0f, false);
            }

            return new InfluenceEnumerator(this, region, fx, fy, fz, true);
        }

        DeformationFieldWeights weights = GetWeights(position);

        return new InfluenceEnumerator(this, weights, (weights.weights != null) && (weights.nodeId != null));
    }
}
