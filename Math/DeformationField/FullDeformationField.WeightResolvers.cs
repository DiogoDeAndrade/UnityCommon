using System;
using UnityEngine;

/// <summary>
/// The mappings from a cell's stored distances to its blend weights.
///
/// Set to set rather than a scalar function of one distance, because three of the four cannot be
/// written any other way: entmax's threshold is a property of the whole set and is what produces its
/// zeros, dividing by the furthest kept distance is what makes sigma and the entmax temperature
/// dimensionless, and the zero-distance case is each mapping's own business rather than a shared
/// special case in front of all of them.
///
/// **Deliberately never serialized.** They are built per graph build from the settings on the graph
/// builder and discarded with it. Putting [SerializeReference] on one would persist {class, ns, asm}
/// into every asset holding it, and renaming or moving a resolver would then detach them all.
/// </summary>
public partial class FullDeformationField
{
    public abstract class WeightResolver
    {
        /// <summary>
        /// Writes <paramref name="count"/> weights for <paramref name="count"/> distances.
        ///
        /// **Distances arrive sorted nearest-first, and that is a precondition rather than an observation.** ComputeWeights gathers them out of a cell that SortInfluences has already
        /// ordered by (distance, node). Entmax's threshold search relies on it; a resolver handed an unsorted array would return a wrong answer rather than an error.
        ///
        /// Every distance passed in is real - the gather drops empty slots and MaxValue - so a resolver never has to test for those. It writes into a buffer the caller owns, because
        /// this runs once per cell and returning a fresh array would allocate once per cell too.
        ///
        /// Weights are expected to sum to one. Writing all zeros is allowed and means "nothing acts here", which the field already treats as a result rather than a gap.
        /// </summary>
        public abstract void ComputeCellWeights(float[] distances, int count, float[] weights);

        /// <summary>
        /// The mapping and its parameters, short and stable, for the golden dump and for the check that refuses a comparison across a weighting change.
        ///
        /// Generated from the parameters rather than written beside them, so it cannot drift out of sync with what the resolver actually does - which is the one way a recorded setting is
        /// worse than no recorded setting at all.
        /// </summary>
        public abstract string Describe();

        /// <summary>
        /// The distances divided by the largest of them, written into <paramref name="normalized"/>.
        ///
        /// Here rather than in ComputeWeights because only some mappings want it, and a caller applying it on their behalf would be deciding something that belongs to the mapping - the
        /// legacy inverse-distance path in particular must not receive it, or it stops reproducing.
        ///
        /// Dividing by the *furthest kept* distance pins the last influence to a constant score, whatever the mapping: with sigma fixed, the k-th node always weighs the same amount before
        /// normalization. That is a statement about what the k-th influence means and not an accident - the OriginalED binding weight does the same thing, and goes to exactly zero
        /// there. The alternative is a world-space parameter, which is brittle against voxel density and piece scale in exactly the way a fixed softmax temperature is.
        /// </summary>
        protected static void NormalizeToFurthest(float[] distances, int count, float[] normalized)
        {
            float furthest = 0.0f;

            for (int i = 0; i < count; i++)
            {
                if (distances[i] > furthest) furthest = distances[i];
            }

            // Every distance identical, or every one zero. Both leave the mapping nothing to discriminate on, and scaling by an arbitrary number would invent a spread that is not
            // there, so they pass through unscaled.
            if (furthest <= DistanceEpsilon)
            {
                for (int i = 0; i < count; i++) normalized[i] = distances[i];
                return;
            }

            for (int i = 0; i < count; i++) normalized[i] = distances[i] / furthest;
        }

        protected static void WriteEvenSplit(int count, float[] weights)
        {
            float share = 1.0f / count;

            for (int i = 0; i < count; i++) weights[i] = share;
        }

        /// <summary>
        /// Divides through by the sum, or writes zeros when there is no sum to divide by. The single point every score-based mapping normalizes at, so none of them can quietly skip it.
        /// </summary>
        protected static void NormalizeInPlace(float[] weights, int count)
        {
            float sum = 0.0f;

            for (int i = 0; i < count; i++) sum += weights[i];

            if (sum <= 0.0f)
            {
                for (int i = 0; i < count; i++) weights[i] = 0.0f;
                return;
            }

            for (int i = 0; i < count; i++) weights[i] /= sum;
        }
    }

    /// <summary>
    /// Normalized inverse distance, 1/max(d, eps) over the sum, splitting evenly between any nodes at distance zero.
    ///
    /// **This is the legacy mapping and it must stay bit-identical.** It is what every structure golden was captured against, so the arithmetic below is the original arithmetic in the
    /// original order - including that the even-split branch fires on the whole set the moment one distance is at or under epsilon, and that max(d, eps) is applied inside both the sum and the
    /// division rather than once.
    ///
    /// The even-split branch exists only because 1/d diverges. It is not inherited by the mappings below, none of which has a singularity at zero to guard - and since seeds gained their true
    /// sub-voxel distances it very rarely fires here either.
    /// </summary>
    public sealed class InverseDistanceWeights : WeightResolver
    {
        public override string Describe() => "invdist";

        public override void ComputeCellWeights(float[] distances, int count, float[] weights)
        {
            int zeroDistanceCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (distances[i] <= DistanceEpsilon) zeroDistanceCount++;
            }

            if (zeroDistanceCount > 0)
            {
                float share = 1f / zeroDistanceCount;

                for (int i = 0; i < count; i++)
                {
                    weights[i] = (distances[i] <= DistanceEpsilon) ? (share) : (0f);
                }

                return;
            }

            float invDistanceSum = 0f;

            for (int i = 0; i < count; i++)
            {
                invDistanceSum += 1f / Mathf.Max(distances[i], DistanceEpsilon);
            }

            if (invDistanceSum <= 0f)
            {
                for (int i = 0; i < count; i++) weights[i] = 0f;
                return;
            }

            for (int i = 0; i < count; i++)
            {
                weights[i] = (1f / Mathf.Max(distances[i], DistanceEpsilon)) / invDistanceSum;
            }
        }
    }

    /// <summary>
    /// Powered inverse distance, 1/max(d, floor)^p over the sum.
    ///
    /// Sharper than the legacy mapping above p = 1 and flatter below it, which is the first thing Phase 7 wants to sweep.
    ///
    /// **The floor clamps the distance, it is not added to it**, and the difference matters more than it looks. Written as 1/(d + eps)^p the correction is roughly p*eps/d, so the constant's
    /// influence grows with the very parameter being swept - at p = 8 and eps = 0.01 it distorts a distance of 0.2 by around 40%, which means the thing held fixed changes meaning as the thing
    /// being varied moves. Written as 1/(d^p + eps) it is worse the other way: the constant dominates wherever d^p is below it, i.e. under d = eps^(1/p), which at p = 8 is 0.56 and swallows most of
    /// a map piece's near range. Clamping instead gives exactly 1/d^p everywhere above the floor - which is everywhere that matters, since the floor sits below the smallest real distance - while
    /// still being bounded at d = 0. The floor stays a distance and means the same thing at every p.
    ///
    /// So at p = 1 this differs from the legacy mapping by the even-split branch and by nothing else, which is exactly the comparison worth having: it isolates what that branch was doing. It still
    /// does not reproduce the legacy goldens, and that is the reason.
    ///
    /// Two nodes both under the floor get identical scores and split evenly once normalized, so the legacy branch's behaviour falls out of the arithmetic rather than being special-cased. Nodes
    /// further out keep a small share rather than being zeroed, which is the smooth version of what the branch did abruptly.
    ///
    /// No distance normalization is offered. Scaling every distance by the same factor scales every score by that factor to the power p, which the normalization then divides straight back out -
    /// so it would change nothing here except through the floor.
    /// </summary>
    public sealed class InversePowerWeights : WeightResolver
    {
        private readonly float power;
        private readonly float distanceFloor;

        public InversePowerWeights(float power, float distanceFloor)
        {
            this.power = power;
            this.distanceFloor = Mathf.Max(distanceFloor, DistanceEpsilon);
        }

        // "floor" rather than "eps", because the two named the same number and meant different arithmetic. A dump that recorded one while the code did the other would be worse than a
        // dump that recorded nothing.
        public override string Describe() => $"invpow p {power:F4} floor {distanceFloor:F6}";

        public override void ComputeCellWeights(float[] distances, int count, float[] weights)
        {
            for (int i = 0; i < count; i++)
            {
                weights[i] = 1.0f / Mathf.Pow(Mathf.Max(distances[i], distanceFloor), power);
            }

            NormalizeInPlace(weights, count);
        }
    }

    /// <summary>
    /// Gaussian falloff, exp(-(d/sigma)^p) over the sum.
    ///
    /// Sigma is a fraction of the furthest kept distance when normalization is on, and a world-space length when it is off. On is the intended setting: a world-space sigma is brittle against both
    /// the voxel density and the scale of the map piece, in exactly the way a fixed softmax temperature is. Off exists so that brittleness can be demonstrated rather than asserted.
    ///
    /// p = 2 is the true Gaussian. Higher p flattens the centre and steepens the shoulder, towards a compact-support kernel without needing a support radius.
    /// </summary>
    public sealed class GaussianWeights : WeightResolver
    {
        private readonly float sigma;
        private readonly float power;
        private readonly bool normalizeDistances;

        private float[] scratch;

        public GaussianWeights(float sigma, float power, bool normalizeDistances)
        {
            this.sigma = Mathf.Max(sigma, 1e-4f);
            this.power = power;
            this.normalizeDistances = normalizeDistances;
        }

        public override string Describe() => $"gauss sigma {sigma:F4} p {power:F4} norm {normalizeDistances}";

        public override void ComputeCellWeights(float[] distances, int count, float[] weights)
        {
            float[] source = distances;

            if (normalizeDistances)
            {
                if ((scratch == null) || (scratch.Length < count)) scratch = new float[count];

                NormalizeToFurthest(distances, count, scratch);

                source = scratch;
            }

            for (int i = 0; i < count; i++)
            {
                weights[i] = Mathf.Exp(-Mathf.Pow(source[i] / sigma, power));
            }

            NormalizeInPlace(weights, count);
        }
    }

    /// <summary>
    /// Alpha-entmax over scores derived from the distances.
    ///
    /// The reason to want it: it can drive weights to exact zero through the normalization itself rather than through a cutoff, so the effective number of influences varies from place to place
    /// on its own. Nothing else here can do that - every mapping above gives every node in the set a nonzero share however far away it is.
    ///
    /// weights_i = [(alpha - 1) * z_i - tau]_+ ^ (1 / (alpha - 1)), with z_i = -d_i / temperature and tau chosen so the weights sum to one. Alpha 1 would be softmax (dense, never zero) and alpha 2
    /// sparsemax; 1.5 sits between and is the usual choice.
    ///
    /// **Tau is found by bisection rather than in closed form.** The exponent is only a convenient 2 at alpha = 1.5, and a solver that works for one alpha is a solver that silently misbehaves at
    /// the others. The sum is monotonically decreasing in tau, the set holds at most a handful of nodes, and this runs once per cell - so the general method costs nothing worth saving.
    /// </summary>
    public sealed class EntmaxWeights : WeightResolver
    {
        private const int bisectionIterations = 60;

        private readonly float alpha;
        private readonly float temperature;
        private readonly bool normalizeDistances;

        private float[] scratch;

        public EntmaxWeights(float alpha, float temperature, bool normalizeDistances)
        {
            // Strictly above 1, or the exponent below is undefined - at exactly 1 the family is
            // softmax, which is a different formula and not reachable by taking a limit here.
            this.alpha = Mathf.Max(alpha, 1.01f);
            this.temperature = Mathf.Max(temperature, 1e-4f);
            this.normalizeDistances = normalizeDistances;
        }

        public override string Describe() => $"entmax a {alpha:F4} t {temperature:F4} norm {normalizeDistances}";

        public override void ComputeCellWeights(float[] distances, int count, float[] weights)
        {
            float[] source = distances;

            if (normalizeDistances)
            {
                if ((scratch == null) || (scratch.Length < count)) scratch = new float[count];

                NormalizeToFurthest(distances, count, scratch);

                source = scratch;
            }

            float exponent = 1.0f / (alpha - 1.0f);
            float scale = alpha - 1.0f;

            // Near is high, so the scores are negated distances. Sorted nearest-first on the way in means score[0] is the largest.
            float highest = -source[0] / temperature;
            float lowest = -source[count - 1] / temperature;

            // At the upper bound every term is clipped to zero, so the sum is 0 and below 1. At the lower bound the smallest score alone contributes at least 1, so the sum is at least 1.
            // The sum decreases monotonically between them, so bisection cannot miss the crossing.
            float low = scale * lowest - 1.0f;
            float high = scale * highest;

            for (int iteration = 0; iteration < bisectionIterations; iteration++)
            {
                float middle = 0.5f * (low + high);

                float sum = 0.0f;

                for (int i = 0; i < count; i++)
                {
                    float t = scale * (-source[i] / temperature) - middle;

                    if (t > 0.0f) sum += Mathf.Pow(t, exponent);
                }

                if (sum > 1.0f) low = middle;
                else high = middle;
            }

            float tau = 0.5f * (low + high);

            for (int i = 0; i < count; i++)
            {
                float t = scale * (-source[i] / temperature) - tau;

                weights[i] = (t > 0.0f) ? (Mathf.Pow(t, exponent)) : (0.0f);
            }

            // The bisection lands close to a sum of one but not on it, and a blend that does not sum to one is a blend that shrinks or inflates the deformation. Renormalizing costs a pass
            // and removes the tolerance from the answer, at the price of nothing: it is a positive rescale, so it cannot resurrect a weight the threshold set to zero.
            NormalizeInPlace(weights, count);
        }
    }
}
