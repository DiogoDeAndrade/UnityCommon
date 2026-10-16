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

    public EDVertexInfluenceMethod builtInfluenceMethod => influenceMethod;
    public EDSkeletonSection builtSkeletonSections => skeletonSections;
    public string builtInfluenceDescriptor => (influenceMethod == EDVertexInfluenceMethod.AlongSkeleton)
                                              ? ($"AlongSkeleton sections {skeletonSections}")
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
