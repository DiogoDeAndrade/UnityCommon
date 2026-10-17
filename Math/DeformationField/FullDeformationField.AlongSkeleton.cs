using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a rest position finds the nodes that carry it.
///
/// ThroughVolume is the field as it has always been: the nearest sources through the solid, the
/// resolver's weights over them, the eight surrounding cells interpolated. AlongSkeleton keeps the
/// field's distances for one job only - saying which nodes are near through the solid, a wall
/// between two corridors keeping the far one out - and then reads the graph: the point belongs to
/// the first linked pair among those nodes it projects onto, at the projection's position along
/// their link, and nothing else carries it. See the partial below for the rule and its fallbacks.
/// </summary>
public enum EDVertexInfluenceMethod
{
    /// <summary>The nearest sources through the solid, blended by the resolver's weights.</summary>
    ThroughVolume,
    /// <summary>The nearest linked pair the point projects onto, at (1 - t, t) along their link.</summary>
    AlongSkeleton
}

/// <summary>What the along-skeleton resolution found at a point.</summary>
public enum EDSkeletonResolution
{
    /// <summary>Nothing influences the point - outside the field, or the field has no links.</summary>
    NoInfluence,
    /// <summary>The projection fell inside the link of a linked pair among the influences.</summary>
    OnLink,
    /// <summary>
    /// Linked pairs existed but every projection fell past a link's end, so the best-ranked pair was
    /// taken with t clamped: the end node carries the point, as it does for a point beyond a door.
    /// </summary>
    ClampedToEnd,
    /// <summary>No two of the influences are linked, so the point blends as ThroughVolume would.</summary>
    NoLinkedPair
}

/// <summary>
/// What a node's cross-section is, for the along-skeleton influence.
///
/// LinkPerpendicular: the plane through the node perpendicular to the link being read, so t is the
/// point's projection on the link - exact on a straight run, and at a bend node a different plane
/// for each of its links. NodeFrame: the plane through the node perpendicular to its own rest
/// forward, one plane per node whatever the link - the bisector at a bend, and the door's plane at a
/// terminal whose frame was taken from its connector - with t the point's position between the two
/// end nodes' planes, dA / (dA + dB). A junction's forward is the average of its links and no
/// section at all, so a junction keeps the link's plane under either.
/// </summary>
public enum EDSkeletonSection
{
    LinkPerpendicular,
    NodeFrame
}

/// <summary>
/// What the along-skeleton influence does where its sections fail to tile the corridor. At a bend
/// sharper than the corridor is wide a link's two end planes converge on the inner side and cross:
/// before the crossing the region is pinched - thinner than the link is long, so t sweeps from one
/// end to the other across a sliver and the carry folds - and past it a point is inside no region
/// (a gap) and is clamped to an end; at a hub's rim two links' regions both hold the point (an
/// overlap) and the first-ranked wins. Both fixes act on the three, and on nothing else: on a
/// straight run, and wherever one region holds the point at a thickness of at least the blend
/// distance, they return the None rule's pair to the bit.
/// </summary>
public enum EDSkeletonKinkFix
{
    /// <summary>One pair: t between the planes however thin the region, clamped past an end in a gap, the first-ranked in an overlap.</summary>
    None,
    /// <summary>
    /// The pair's share falls to the volume blend's set - the four-influence blend the field holds
    /// anyway - with the region's thickness in a pinch, with the overshoot past the region in a gap
    /// and with the depth inside a second region in an overlap, each over Kink Blend Distance of the
    /// link's length; so where the sections do not tile, the field is the volume blend.
    /// </summary>
    BlendTowardsVolume,
    /// <summary>
    /// The regions that claim the point are averaged, never leaving the skeleton's pairs: the inside
    /// regions by their depth, and, as the region holding the point thins to a pinch or ends in a
    /// gap, the clamped carries of the neighbouring regions within the blend distance, by how near
    /// each is.
    /// </summary>
    BlendSections
}

/// <summary>
/// The along-skeleton influence: a point is carried by the two ends of the link it lies beside, in
/// proportion to where along the link it projects.
///
/// Resolved per query rather than baked per cell, so t is exact at the point instead of being
/// interpolated across a voxel, and the pool a point chooses from is the trilinear union of its
/// eight surrounding cells rather than one cell's list. Everything downstream is untouched: the
/// result is an ordinary influence set of two entries, so the blenders, the captured influence
/// tables and the probe all read it through the same enumerator they read the volume blend
/// through.
///
/// The rule, in the order the influences rank (weight descending, node index on a tie): take node
/// A; among the lower-ranked nodes linked to A, in their rank order, project the point on the link
/// A-B; the first projection that falls inside its link wins, and the point is carried by A at
/// (1 - t) and B at t. No link of A works, the next A. Two fallbacks, each counted at build time so
/// a piece says how often it needed them: when every linked pair's projection fell past an end, the
/// best-ranked pair is taken with t clamped, which hands a point beyond a door to the terminal and a
/// point past a bend's corner to the corner node; when no two influences are linked at all, the
/// point blends as ThroughVolume would, so nothing is ever left undeformed for want of a rule.
///
/// The width of a corridor enters nowhere: a door's jamb projects to t = 1 on its own link whatever
/// the door's width, which is what the seed bars existed to approximate. Under this method the
/// builder seeds every node as a point and the bar toggles are ignored.
///
/// What is not continuous: where the first-ranked linked pair changes between two links that do not
/// share the node the point is near - a hub's rim between two doors, where the point projects inside
/// both doors' links from the junction - the carrying pair flips across the ranks' crossing and the
/// output tears there. Along a corridor, and on either side of a bend, consecutive links share a
/// node and every flip is between two expressions of that node, so the map is continuous.
/// </summary>
public partial class FullDeformationField
{
    [SerializeField, HideInInspector]
    EDVertexInfluenceMethod influenceMethod = EDVertexInfluenceMethod.ThroughVolume;

    // The graph's links, compressed: node n's neighbours are linkNodes[linkStart[n] .. linkStart[n + 1]).
    // Handed over by the builder once the nodes are seeded, whatever the method, so a probe can say
    // what AlongSkeleton would resolve to under a ThroughVolume field. Null when none were given.
    [SerializeField, HideInInspector]
    int[] linkStart;
    [SerializeField, HideInInspector]
    int[] linkNodes;

    [SerializeField, HideInInspector]
    EDSkeletonSection skeletonSections = EDSkeletonSection.LinkPerpendicular;

    [SerializeField, HideInInspector]
    EDSkeletonKinkFix skeletonKinkFix = EDSkeletonKinkFix.None;
    // The blend distance of the kink fixes, as a fraction of the link's rest length.
    [SerializeField, HideInInspector]
    float skeletonKinkDistance = 0.5f;

    public EDVertexInfluenceMethod builtInfluenceMethod => influenceMethod;
    public EDSkeletonSection builtSkeletonSections => skeletonSections;
    public EDSkeletonKinkFix builtSkeletonKinkFix => skeletonKinkFix;
    public float builtSkeletonKinkDistance => skeletonKinkDistance;
    // The kink fix and its distance only when a fix is on, so a dump made before the option existed
    // reads the same under None.
    public string builtInfluenceDescriptor => (influenceMethod == EDVertexInfluenceMethod.AlongSkeleton)
                                              ? ($"AlongSkeleton sections {skeletonSections}"
                                                 + ((skeletonKinkFix != EDSkeletonKinkFix.None)
                                                    ? ($" kink {skeletonKinkFix} {skeletonKinkDistance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}")
                                                    : ("")))
                                              : (influenceMethod.ToString());
    public bool hasNodeLinks => (linkStart != null) && (linkNodes != null) && (deformationNodes != null) && (linkStart.Length == deformationNodes.Count + 1);

    /// <summary>
    /// The graph's adjacency, one list per node in node order - EDNode.neighbors as the builder
    /// holds them. A self link or an index outside the node list is dropped.
    /// </summary>
    public void SetNodeLinks(IReadOnlyList<IReadOnlyList<int>> neighbours)
    {
        int nodeCount = (deformationNodes != null) ? (deformationNodes.Count) : (0);

        var start = new int[nodeCount + 1];
        var entries = new List<int>(nodeCount * 2);

        for (int i = 0; i < nodeCount; i++)
        {
            start[i] = entries.Count;

            IReadOnlyList<int> list = ((neighbours != null) && (i < neighbours.Count)) ? (neighbours[i]) : (null);

            if (list == null) continue;

            for (int j = 0; j < list.Count; j++)
            {
                int n = list[j];

                if ((n < 0) || (n >= nodeCount) || (n == i)) continue;

                entries.Add(n);
            }
        }

        start[nodeCount] = entries.Count;

        linkStart = start;
        linkNodes = entries.ToArray();
    }

    int LinkCount(int node) => (hasNodeLinks) ? (linkStart[node + 1] - linkStart[node]) : (0);

    /// <summary>
    /// The section plane's normal at one end of link a-b, pointing from a towards b: the node's own
    /// forward under NodeFrame, where the node is not a junction and its forward is not across the
    /// link; the link's direction otherwise.
    /// </summary>
    Vector3 SectionNormal(int node, Vector3 linkDirection)
    {
        if (skeletonSections != EDSkeletonSection.NodeFrame) return linkDirection;
        if (LinkCount(node) >= 3) return linkDirection;

        Vector3 forward = deformationNodes[node].frame.forward;

        float dot = Vector3.Dot(forward, linkDirection);

        // Across the link, or no frame at all: nothing to section with.
        if (Mathf.Abs(dot) <= 1e-3f) return linkDirection;

        if (dot < 0.0f) forward = -forward;

        float length = forward.magnitude;

        return (length > 1e-6f) ? (forward / length) : (linkDirection);
    }

    /// <summary>
    /// Where a point lies along link a-b, by the sections in force: inside with t in [0, 1], or
    /// outside with t clamped to the nearer end. False for a link of no length.
    /// </summary>
    bool AlongLink(Vector3 position, int a, int b, out float t, out bool inside)
    {
        t = 0.0f;
        inside = false;

        Vector3 pa = deformationNodes[a].frame.position;
        Vector3 pb = deformationNodes[b].frame.position;
        Vector3 ab = pb - pa;

        float lengthSq = ab.sqrMagnitude;

        if (lengthSq <= DistanceEpsilon * DistanceEpsilon) return false;

        if (skeletonSections == EDSkeletonSection.LinkPerpendicular)
        {
            float along = Vector3.Dot(position - pa, ab) / lengthSq;

            inside = (along >= 0.0f) && (along <= 1.0f);
            t = Mathf.Clamp01(along);

            return true;
        }

        Vector3 direction = ab / Mathf.Sqrt(lengthSq);

        float dA = Vector3.Dot(position - pa, SectionNormal(a, direction));
        float dB = Vector3.Dot(pb - position, SectionNormal(b, direction));
        float sum = dA + dB;

        if ((dA >= 0.0f) && (dB >= 0.0f) && (sum > DistanceEpsilon))
        {
            inside = true;
            t = dA / sum;

            return true;
        }

        // Before a's plane, past b's, or between two planes that cross here: the nearer end.
        if ((dA < 0.0f) && (dB >= 0.0f)) t = 0.0f;
        else if ((dB < 0.0f) && (dA >= 0.0f)) t = 1.0f;
        else t = (Mathf.Abs(dA) <= Mathf.Abs(dB)) ? (0.0f) : (1.0f);

        return true;
    }

    /// <summary>
    /// AlongLink with what the kink fixes read beside t: the link's rest length, and the point's
    /// margin inside the link's region - the smaller of its two signed distances to the end planes,
    /// positive inside, negative by the overshoot outside, in world units. Under LinkPerpendicular
    /// the distances are the projection's along the link. Same inside test and same clamp as
    /// AlongLink; this one is read only when a fix is on.
    /// </summary>
    bool AlongLinkMargin(Vector3 position, int a, int b, out float t, out bool inside, out float margin, out float thickness, out float length)
    {
        t = 0.0f;
        inside = false;
        margin = 0.0f;
        thickness = 0.0f;
        length = 0.0f;

        Vector3 pa = deformationNodes[a].frame.position;
        Vector3 pb = deformationNodes[b].frame.position;
        Vector3 ab = pb - pa;

        float lengthSq = ab.sqrMagnitude;

        if (lengthSq <= DistanceEpsilon * DistanceEpsilon) return false;

        length = Mathf.Sqrt(lengthSq);

        float dA;
        float dB;

        if (skeletonSections == EDSkeletonSection.LinkPerpendicular)
        {
            float along = Vector3.Dot(position - pa, ab) / lengthSq;

            dA = along * length;
            dB = (1.0f - along) * length;
        }
        else
        {
            Vector3 direction = ab / length;

            dA = Vector3.Dot(position - pa, SectionNormal(a, direction));
            dB = Vector3.Dot(pb - position, SectionNormal(b, direction));
        }

        margin = Mathf.Min(dA, dB);

        // The region's thickness at the point along the planes' normals: the link's length where the
        // planes are parallel, less where they converge, nothing at their crossing.
        float sum = dA + dB;

        thickness = sum;

        if ((dA >= 0.0f) && (dB >= 0.0f) && (sum > DistanceEpsilon))
        {
            inside = true;
            t = dA / sum;

            return true;
        }

        if ((dA < 0.0f) && (dB >= 0.0f)) t = 0.0f;
        else if ((dB < 0.0f) && (dA >= 0.0f)) t = 1.0f;
        else t = (Mathf.Abs(dA) <= Mathf.Abs(dB)) ? (0.0f) : (1.0f);

        return true;
    }

    bool AreLinked(int a, int b)
    {
        if (!hasNodeLinks) return false;

        int end = linkStart[a + 1];

        for (int i = linkStart[a]; i < end; i++)
        {
            if (linkNodes[i] == b) return true;
        }

        return false;
    }

    // Per-thread scratch for the gather, because the resolution runs inside the parallel passes
    // (the Jacobians, the clearance pass) and a shared buffer would be a race. Grown on demand and
    // then reused, so after the first few queries a resolution allocates nothing.
    [ThreadStatic] static int[]   skeletonNodes;
    [ThreadStatic] static float[] skeletonWeights;

    static void GrowSkeletonScratch(int needed)
    {
        int size = Mathf.Max(16, needed * 2);

        var nodes = new int[size];
        var weights = new float[size];

        if (skeletonNodes != null)
        {
            Array.Copy(skeletonNodes, nodes, skeletonNodes.Length);
            Array.Copy(skeletonWeights, weights, skeletonWeights.Length);
        }

        skeletonNodes = nodes;
        skeletonWeights = weights;
    }

    static bool RanksBefore(int node, float weight, int otherNode, float otherWeight)
        => (weight > otherWeight) || ((weight == otherWeight) && (node < otherNode));

    /// <summary>
    /// Walks a copy of the enumerator into the scratch, in rank order: weight descending, node
    /// index ascending on a tie, so the order is a function of the influences and not of the order
    /// the field happens to yield them in. Returns how many were gathered.
    /// </summary>
    static int GatherRankedInfluences(InfluenceEnumerator influences)
    {
        int count = 0;

        InfluenceEnumerator pass = influences;

        while (pass.MoveNext(out int node, out float weight))
        {
            if ((skeletonNodes == null) || (count >= skeletonNodes.Length)) GrowSkeletonScratch(count + 1);

            int k = count;

            while ((k > 0) && RanksBefore(node, weight, skeletonNodes[k - 1], skeletonWeights[k - 1]))
            {
                skeletonNodes[k] = skeletonNodes[k - 1];
                skeletonWeights[k] = skeletonWeights[k - 1];
                k--;
            }

            skeletonNodes[k] = node;
            skeletonWeights[k] = weight;

            count++;
        }

        return count;
    }

    /// <summary>
    /// The rule of the class comment over a given set of influences. True with a pair and its t when
    /// the point is carried by a link (OnLink) or by a link's end (ClampedToEnd); false when nothing
    /// influences the point or no two influences are linked, and the status says which.
    /// </summary>
    internal bool TryResolveSkeletonPair(InfluenceEnumerator influences, Vector3 position, out int nodeA, out int nodeB, out float t, out EDSkeletonResolution status)
    {
        nodeA = -1;
        nodeB = -1;
        t = 0.0f;
        status = EDSkeletonResolution.NoInfluence;

        int count = GatherRankedInfluences(influences);

        if (count == 0) return false;

        int[] ranked = skeletonNodes;

        int clampedA = -1;
        int clampedB = -1;
        float clampedT = 0.0f;

        for (int i = 0; i < count; i++)
        {
            int a = ranked[i];

            for (int j = i + 1; j < count; j++)
            {
                int b = ranked[j];

                if (!AreLinked(a, b)) continue;

                // Two nodes at one position have no link to read along.
                if (!AlongLink(position, a, b, out float along, out bool inside)) continue;

                if (inside)
                {
                    nodeA = a;
                    nodeB = b;
                    t = along;
                    status = EDSkeletonResolution.OnLink;

                    return true;
                }

                // The first linked pair in rank order is the one to clamp to if nothing lands inside.
                if (clampedA < 0)
                {
                    clampedA = a;
                    clampedB = b;
                    clampedT = along;
                }
            }
        }

        if (clampedA >= 0)
        {
            nodeA = clampedA;
            nodeB = clampedB;
            t = clampedT;
            status = EDSkeletonResolution.ClampedToEnd;

            return true;
        }

        status = EDSkeletonResolution.NoLinkedPair;

        return false;
    }

    /// <summary>
    /// What AlongSkeleton resolves a point to, whichever method the field was built with - for the
    /// probe, which can then say what the other method would do at the point in front of it.
    /// </summary>
    public bool TryResolveSkeletonPair(Vector3 position, bool trilinear, out int nodeA, out int nodeB, out float t, out EDSkeletonResolution status)
        => TryResolveSkeletonPair(EnumerateRawInfluences(position, trilinear), position, out nodeA, out nodeB, out t, out status);

    // ----------------------------------------------------------------------------------------------
    // The kink fixes - see EDSkeletonKinkFix. Separate from TryResolveSkeletonPair so the None rule's
    // path stays the statements it was.
    // ----------------------------------------------------------------------------------------------

    // Per-thread scratch for the candidate pairs and the merged result, as the gather's is.
    [ThreadStatic] static int[]   kinkPairA;
    [ThreadStatic] static int[]   kinkPairB;
    [ThreadStatic] static float[] kinkT;
    [ThreadStatic] static float[] kinkMargin;
    [ThreadStatic] static float[] kinkThick;
    [ThreadStatic] static float[] kinkBand;
    [ThreadStatic] static bool[]  kinkInside;
    [ThreadStatic] static int[]   kinkOutNodes;
    [ThreadStatic] static float[] kinkOutWeights;

    static void EnsureKinkCandidates(int needed)
    {
        if ((kinkPairA != null) && (kinkPairA.Length >= needed)) return;

        int size = Mathf.Max(16, needed * 2);

        Array.Resize(ref kinkPairA, size);
        Array.Resize(ref kinkPairB, size);
        Array.Resize(ref kinkT, size);
        Array.Resize(ref kinkMargin, size);
        Array.Resize(ref kinkThick, size);
        Array.Resize(ref kinkBand, size);
        Array.Resize(ref kinkInside, size);
    }

    static void EnsureKinkOut(int needed)
    {
        if ((kinkOutNodes != null) && (kinkOutNodes.Length >= needed)) return;

        int size = Mathf.Max(16, needed * 2);

        Array.Resize(ref kinkOutNodes, size);
        Array.Resize(ref kinkOutWeights, size);
    }

    /// <summary>Adds a node's share to the merged result, summing with an entry the node already has.</summary>
    static void AddKinkWeight(ref int count, int node, float weight)
    {
        if (weight <= 0.0f) return;

        for (int i = 0; i < count; i++)
        {
            if (kinkOutNodes[i] != node) continue;

            kinkOutWeights[i] += weight;

            return;
        }

        EnsureKinkOut(count + 1);

        kinkOutNodes[count] = node;
        kinkOutWeights[count] = weight;

        count++;
    }

    /// <summary>
    /// Whether a clamp lands on a leaf's far side - a point beyond a door, which the terminal carries
    /// alone at any distance, under the fixes as under None.
    /// </summary>
    bool IsDoorClamp(int a, int b, float t)
        => ((t <= 0.0f) && (LinkCount(a) == 1)) || ((t >= 1.0f) && (LinkCount(b) == 1));

    /// <summary>
    /// The kink fixes' resolution. True with the influences the point is carried by - the None
    /// rule's pair where nothing needs fixing, a blend of pairs, or the pair's share over the volume
    /// blend's set; false when nothing influences the point or no two influences are linked, when
    /// the caller uses the volume's set as the None rule does. <paramref name="blended"/> says
    /// whether the fix changed the carry against the None rule at this point, for the census.
    ///
    /// The candidates are every linked pair among the influences, in rank order, each with its
    /// margin - the depth inside its region, or minus the overshoot outside it - and its thickness,
    /// the region's width at the point along the planes' normals, which is the link's length where
    /// the planes are parallel and nothing at their crossing. The primary is the first inside pair
    /// in rank order, as under None; with none inside, the candidate nearest to its region rather
    /// than the first-ranked clamp, a door's terminal counting as inside at any distance. The band of
    /// a candidate is Kink Blend Distance times its link's length. The confidence that the sections
    /// tile the place is the least, over every candidate, of one less the candidate's nearness times
    /// how far its thickness falls short of its band - so a pinched region lowers it on both sides of
    /// its boundary and stops counting one band away, which keeps the carry continuous across the
    /// boundary between a full region and a pinched one.
    /// </summary>
    internal bool TryResolveSkeletonBlend(InfluenceEnumerator raw, Vector3 position, out InfluenceEnumerator result, out EDSkeletonResolution status, out bool blended)
    {
        result = raw;
        status = EDSkeletonResolution.NoInfluence;
        blended = false;

        int count = GatherRankedInfluences(raw);

        if (count == 0) return false;

        int[] ranked = skeletonNodes;

        int candidates = 0;
        int primary = -1;
        int nearest = -1;

        for (int i = 0; i < count; i++)
        {
            int a = ranked[i];

            for (int j = i + 1; j < count; j++)
            {
                int b = ranked[j];

                if (!AreLinked(a, b)) continue;
                if (!AlongLinkMargin(position, a, b, out float along, out bool inside, out float margin, out float thickness, out float length)) continue;

                EnsureKinkCandidates(candidates + 1);

                int c = candidates++;

                bool door = !inside && IsDoorClamp(a, b, along);

                kinkPairA[c] = a;
                kinkPairB[c] = b;
                kinkT[c] = along;
                kinkInside[c] = inside;
                kinkMargin[c] = door ? (float.PositiveInfinity) : (margin);
                kinkThick[c] = door ? (float.PositiveInfinity) : (thickness);
                kinkBand[c] = Mathf.Max(DistanceEpsilon, skeletonKinkDistance * length);

                if (inside && (primary < 0)) primary = c;
                if (!inside && ((nearest < 0) || (kinkMargin[c] > kinkMargin[nearest]))) nearest = c;
            }
        }

        if (candidates == 0)
        {
            status = EDSkeletonResolution.NoLinkedPair;

            return false;
        }

        if (primary >= 0)
        {
            status = EDSkeletonResolution.OnLink;
        }
        else
        {
            primary = nearest;
            status = EDSkeletonResolution.ClampedToEnd;
        }

        float confidence = 1.0f;

        for (int c = 0; c < candidates; c++)
        {
            float nearness = (kinkInside[c] || (kinkMargin[c] >= 0.0f)) ? (1.0f) : (Mathf.Clamp01(1.0f + kinkMargin[c] / kinkBand[c]));
            float thick = Mathf.Clamp01(kinkThick[c] / kinkBand[c]);

            confidence = Mathf.Min(confidence, 1.0f - nearness * (1.0f - thick));
        }

        switch (skeletonKinkFix)
        {
            case EDSkeletonKinkFix.BlendTowardsVolume:
                return ResolveBlendTowardsVolume(raw, candidates, primary, confidence, out result, out blended);

            case EDSkeletonKinkFix.BlendSections:
                return ResolveBlendSections(raw, candidates, primary, confidence, out result, out blended);
        }

        result = new InfluenceEnumerator(this, kinkPairA[primary], 1.0f - kinkT[primary], kinkPairB[primary], kinkT[primary]);

        return true;
    }

    /// <summary>
    /// The pair's share g: one inside a single region of full thickness, falling to zero with the
    /// region's thickness (a pinch), by the overshoot past the region (a gap) or by the depth inside
    /// a second region (an overlap), each over the band. At g of one the result is the None rule's
    /// pair to the bit; at zero the volume blend's set; between, the pair at g over the volume's set
    /// normalised at 1 - g, the two pair nodes carrying their volume share in the list and the rest
    /// following as a scaled tail.
    /// </summary>
    bool ResolveBlendTowardsVolume(InfluenceEnumerator raw, int candidates, int primary, float confidence, out InfluenceEnumerator result, out bool blended)
    {
        int a = kinkPairA[primary];
        int b = kinkPairB[primary];
        float t = kinkT[primary];

        float g = confidence;

        if (kinkMargin[primary] < 0.0f) g = Mathf.Min(g, Mathf.Clamp01(1.0f + kinkMargin[primary] / kinkBand[primary]));

        for (int c = 0; c < candidates; c++)
        {
            if ((c == primary) || !kinkInside[c] || (kinkMargin[c] <= 0.0f)) continue;

            g = Mathf.Min(g, Mathf.Clamp01(1.0f - kinkMargin[c] / kinkBand[c]));
        }

        if (g >= 1.0f)
        {
            result = new InfluenceEnumerator(this, a, 1.0f - t, b, t);
            blended = false;

            return true;
        }

        blended = true;

        if (g <= 0.0f)
        {
            result = raw;

            return true;
        }

        float sum = 0.0f;
        float rawA = 0.0f;
        float rawB = 0.0f;

        InfluenceEnumerator pass = raw;

        while (pass.MoveNext(out int node, out float weight))
        {
            sum += weight;

            if (node == a) rawA += weight;
            else if (node == b) rawB += weight;
        }

        if (sum <= DistanceEpsilon)
        {
            result = new InfluenceEnumerator(this, a, 1.0f - t, b, t);

            return true;
        }

        float tail = (1.0f - g) / sum;

        EnsureKinkOut(2);

        kinkOutNodes[0] = a;
        kinkOutWeights[0] = g * (1.0f - t) + rawA * tail;
        kinkOutNodes[1] = b;
        kinkOutWeights[1] = g * t + rawB * tail;

        result = new InfluenceEnumerator(this, raw, tail, 2, kinkOutNodes, kinkOutWeights);

        return true;
    }

    /// <summary>
    /// The regions averaged, in two parts. The inside part: every candidate holding the point
    /// inside, by its depth over its band, so a second region's share rises from zero at its
    /// boundary. The gap part: the clamped carries of the other candidates within their band, by how
    /// near each is, a door's terminal at full share at any distance. The result is the inside part
    /// at the place's confidence over the gap part at the rest - the inside part alone where the
    /// sections tile, the gap part alone where nothing holds the point, and the nearest alone past
    /// every band. One region holding the point alone at full confidence is the None rule's pair to
    /// the bit.
    /// </summary>
    bool ResolveBlendSections(InfluenceEnumerator raw, int candidates, int primary, float confidence, out InfluenceEnumerator result, out bool blended)
    {
        // The inside part, into the scratch from the front.
        int insideCount = 0;
        int insideUsed = 0;
        int insideSingle = -1;
        float insideTotal = 0.0f;

        for (int c = 0; c < candidates; c++)
        {
            if (!kinkInside[c]) continue;

            float s = Mathf.Clamp01(kinkMargin[c] / kinkBand[c]);

            if (s <= 0.0f) continue;

            AddKinkWeight(ref insideCount, kinkPairA[c], s * (1.0f - kinkT[c]));
            AddKinkWeight(ref insideCount, kinkPairB[c], s * kinkT[c]);

            insideTotal += s;
            insideSingle = c;
            insideUsed++;
        }

        if ((insideUsed == 1) && (confidence >= 1.0f))
        {
            result = new InfluenceEnumerator(this, kinkPairA[insideSingle], 1.0f - kinkT[insideSingle], kinkPairB[insideSingle], kinkT[insideSingle]);
            blended = (insideSingle != primary);

            return true;
        }

        // The gap part: the candidates not in the inside part, within their band.
        int gapUsed = 0;
        int gapSingle = -1;
        float gapTotal = 0.0f;

        EnsureKinkOut(insideCount + 1);

        int gapStart = insideCount;
        int gapCount = insideCount;

        for (int c = 0; c < candidates; c++)
        {
            if (kinkInside[c] && (kinkMargin[c] > 0.0f)) continue;

            float f = (kinkMargin[c] >= 0.0f) ? (1.0f) : (Mathf.Clamp01(1.0f + kinkMargin[c] / kinkBand[c]));

            if (f <= 0.0f) continue;

            AddKinkWeightFrom(gapStart, ref gapCount, kinkPairA[c], f * (1.0f - kinkT[c]));
            AddKinkWeightFrom(gapStart, ref gapCount, kinkPairB[c], f * kinkT[c]);

            gapTotal += f;
            gapSingle = c;
            gapUsed++;
        }

        if ((insideUsed == 0) && (gapUsed <= 1))
        {
            int c = (gapUsed == 1) ? (gapSingle) : (primary);

            result = new InfluenceEnumerator(this, kinkPairA[c], 1.0f - kinkT[c], kinkPairB[c], kinkT[c]);
            blended = (c != primary);

            return true;
        }

        // Combine: the inside part at its confidence, the gap part at the rest; either part alone
        // when the other is empty.
        float insideShare = (insideUsed > 0) ? ((gapUsed > 0) ? (confidence) : (1.0f)) : (0.0f);
        float gapShare = 1.0f - insideShare;

        int outCount = 0;

        for (int i = 0; i < insideCount; i++)
        {
            if (insideShare > 0.0f) AddKinkWeight(ref outCount, kinkOutNodes[i], kinkOutWeights[i] * insideShare / insideTotal);
        }

        for (int i = gapStart; i < gapCount; i++)
        {
            if (gapShare > 0.0f) AddKinkWeightFrom(0, ref outCount, kinkOutNodes[i], kinkOutWeights[i] * gapShare / gapTotal);
        }

        result = new InfluenceEnumerator(this, raw, 0.0f, outCount, kinkOutNodes, kinkOutWeights);
        blended = true;

        return true;
    }

    /// <summary>AddKinkWeight over the entries from <paramref name="start"/> on, so two parts can share the scratch.</summary>
    static void AddKinkWeightFrom(int start, ref int count, int node, float weight)
    {
        if (weight <= 0.0f) return;

        for (int i = start; i < count; i++)
        {
            if (kinkOutNodes[i] != node) continue;

            kinkOutWeights[i] += weight;

            return;
        }

        EnsureKinkOut(count + 1);

        kinkOutNodes[count] = node;
        kinkOutWeights[count] = weight;

        count++;
    }

    /// <summary>
    /// The kink fix at the centre of every cell anything influences: how many cells the fix changed
    /// the carry at, beside the census of the None rule's outcomes.
    /// </summary>
    public void CountSkeletonKinkBlends(out int cells, out int blended)
    {
        cells = 0;
        blended = 0;

        if (!HasVoxelData()) return;
        if (skeletonKinkFix == EDSkeletonKinkFix.None) return;

        EnsureTrilinearRegionsBuilt();

        int slice = voxelData.gridSize.x * voxelData.gridSize.y;

        for (int index = 0; index < voxelData.data.Length; index++)
        {
            if (!HasInfluence(voxelData.data[index])) continue;

            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / voxelData.gridSize.x;
            int x = rem - y * voxelData.gridSize.x;

            cells++;

            Vector3 centre = VoxelCenter(x, y, z);

            if (TryResolveSkeletonBlend(EnumerateRawInfluences(centre, true), centre, out _, out _, out bool changed) && changed) blended++;
        }
    }

    /// <summary>
    /// The resolution at the centre of every cell anything influences, counted by outcome - the
    /// build's own census of how often each fallback was needed on this piece.
    /// </summary>
    public void CountSkeletonResolutions(out int cells, out int onLink, out int clampedToEnd, out int noLinkedPair)
    {
        cells = 0;
        onLink = 0;
        clampedToEnd = 0;
        noLinkedPair = 0;

        if (!HasVoxelData()) return;

        EnsureTrilinearRegionsBuilt();

        int slice = voxelData.gridSize.x * voxelData.gridSize.y;

        for (int index = 0; index < voxelData.data.Length; index++)
        {
            if (!HasInfluence(voxelData.data[index])) continue;

            int z = index / slice;
            int rem = index - z * slice;
            int y = rem / voxelData.gridSize.x;
            int x = rem - y * voxelData.gridSize.x;

            cells++;

            TryResolveSkeletonPair(VoxelCenter(x, y, z), true, out _, out _, out _, out EDSkeletonResolution status);

            switch (status)
            {
                case EDSkeletonResolution.OnLink: onLink++; break;
                case EDSkeletonResolution.ClampedToEnd: clampedToEnd++; break;
                case EDSkeletonResolution.NoLinkedPair: noLinkedPair++; break;
            }
        }
    }
}
