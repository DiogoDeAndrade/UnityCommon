using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    // The convex hull of a point set. Quickhull (Barber, Dobkin and Huhdanpaa 1996) in double
    // precision, the hull kept as triangles with no face merging: what is read off it is the volume
    // and which points are inside, and a hull face cut into two triangles encloses the same volume
    // and the same points. Written 2026-10-21 for the Deform project's benchmark metric and the
    // empty-space option of its field quality terms, and checked there by Tools/BenchmarkMetricsCheck.
    // Nothing but Vector3 at the entry, so that harness drives it as it stands.

    /// <summary>
    /// The convex hull of a point set: its triangles, wound outward, its volume, and its face planes
    /// for telling a point inside from one outside.
    ///
    /// <para>A point within a rounding tolerance of a face - three units in the last place of the
    /// coordinates' magnitude, as John Lloyd's QuickHull3D sets it - counts as on the face and is left
    /// out, so exactly coplanar or collinear input (the grid of a box face, a duplicated vertex) is
    /// built from its extreme points. Without face merging, input that is degenerate within rounding
    /// can still produce a hull face too thin for its normal to be trusted, or a visible region that is
    /// not a disc, which no correct hull has. Every pass checks for both, and for a closed surface of
    /// Euler characteristic two at the end; a pass that fails is run again on the points joggled - each
    /// coordinate moved by a deterministic pseudo-random amount, 1e-9 of the coordinates' magnitude and
    /// ten times more on each retry - as qhull's joggle (option QJ) does. The joggled pass's triangles
    /// are kept and its volume is taken on the points as given, so the joggle reaches the volume only
    /// through the choice of triangulation, at the order of the joggle.</para>
    ///
    /// <para>Fewer than four points, or every point on one plane within the tolerance, is a degenerate
    /// hull: volume zero and no triangles, reported as degenerate rather than as a failure.</para>
    /// </summary>
    public static class ConvexHull3D
    {
        private const double unitRoundoff = 2.2204460492503131e-16;

        // The first pass is exact; each later one joggles by this fraction of the coordinates' magnitude.
        private static readonly double[] joggles = { 0.0, 1e-9, 1e-8, 1e-7, 1e-6 };

        // A new face thinner than this fraction of the coordinates' magnitude - twice its area over its
        // longest edge - fails the pass: its normal is then known to no better than the unit roundoff
        // over this, about 2e-6, which bounds what a point tested against it can be misjudged by.
        private const double minHeightFraction = 1e-10;

        /// <summary>
        /// The hull of the points. Returns false only when no pass, joggled or not, built a consistent
        /// hull, with the reason; a degenerate set returns true with <paramref name="degenerate"/> set,
        /// volume zero and no triangles. <paramref name="joggle"/> is the fraction of the coordinates'
        /// magnitude the points were joggled by, zero for the exact pass. The triangles index the input.
        /// </summary>
        public static bool TryBuild(Vector3[] points, out double volume, out int[] triangles, out bool degenerate, out double joggle, out string why)
            => TryBuildFromPass(points, 0, out volume, out triangles, out degenerate, out joggle, out why);

        /// <summary>
        /// The same, starting at a later pass - 1 is the first joggle - so the shell harness can check
        /// the joggled passes, which the input it builds rarely sends the build to.
        /// </summary>
        internal static bool TryBuildFromPass(Vector3[] points, int firstPass, out double volume, out int[] triangles, out bool degenerate, out double joggle, out string why)
        {
            volume = 0.0;
            triangles = Array.Empty<int>();
            degenerate = false;
            joggle = 0.0;
            why = null;

            if (points == null)
            {
                why = "no points";
                return false;
            }

            int count = points.Length;

            var x0 = new double[count];
            var y0 = new double[count];
            var z0 = new double[count];

            double maxX = 0.0, maxY = 0.0, maxZ = 0.0;

            for (int i = 0; i < count; i++)
            {
                x0[i] = points[i].x;
                y0[i] = points[i].y;
                z0[i] = points[i].z;

                if (double.IsNaN(x0[i]) || double.IsNaN(y0[i]) || double.IsNaN(z0[i]) || double.IsInfinity(x0[i]) || double.IsInfinity(y0[i]) || double.IsInfinity(z0[i]))
                {
                    why = $"point {i} is not finite";
                    return false;
                }

                maxX = Math.Max(maxX, Math.Abs(x0[i]));
                maxY = Math.Max(maxY, Math.Abs(y0[i]));
                maxZ = Math.Max(maxZ, Math.Abs(z0[i]));
            }

            double tolerance = 3.0 * unitRoundoff * (maxX + maxY + maxZ);
            double magnitude = Math.Max(maxX, Math.Max(maxY, maxZ));

            string failure = null;

            for (int attempt = Mathf.Clamp(firstPass, 0, joggles.Length - 1); attempt < joggles.Length; attempt++)
            {
                double amount = joggles[attempt] * magnitude;

                double[] x = x0, y = y0, z = z0;

                if (amount > 0.0)
                {
                    x = Joggled(x0, amount, attempt, 0);
                    y = Joggled(y0, amount, attempt, 1);
                    z = Joggled(z0, amount, attempt, 2);
                }

                var builder = new Builder(x, y, z, tolerance, minHeightFraction * magnitude, amount > 0.0);

                bool built = builder.Run(out bool flat, out failure);

                // Flatness is decided by the exact pass, which is the only one a flat set reaches - the
                // joggles are for a set that is not flat and failed - and is a hull of no volume.
                if (flat)
                {
                    degenerate = true;
                    return true;
                }

                if (built)
                {
                    triangles = builder.Triangles();
                    volume = Volume(x0, y0, z0, triangles);
                    joggle = joggles[attempt];

                    return true;
                }
            }

            why = $"no consistent hull after joggling the points by up to {joggles[joggles.Length - 1]:G2} of their magnitude: {failure}";

            return false;
        }

        /// <summary>
        /// The hull's face planes, four doubles a face - the outward unit normal and its offset, so
        /// a point p is on the inner side of face f when n.p - d is at most zero - taken on the
        /// points as given. A face with no area there (three points collinear, which a joggled pass
        /// can triangulate) bounds nothing and is left out.
        /// </summary>
        public static double[] FacePlanes(Vector3[] points, int[] triangles)
        {
            var planes = new List<double>();

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];

                double ux = (double)b.x - a.x, uy = (double)b.y - a.y, uz = (double)b.z - a.z;
                double vx = (double)c.x - a.x, vy = (double)c.y - a.y, vz = (double)c.z - a.z;
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);

                if (!(length > 0.0)) continue;

                nx /= length;
                ny /= length;
                nz /= length;

                planes.Add(nx);
                planes.Add(ny);
                planes.Add(nz);
                planes.Add(nx * a.x + ny * a.y + nz * a.z);
            }

            return planes.ToArray();
        }

        /// <summary>
        /// Whether a point is inside the hull the planes bound - on the inner side of every face, or
        /// beyond it by no more than the tolerance. Every plane is read, so it costs the face count.
        /// </summary>
        public static bool Contains(double[] planes, Vector3 point, double tolerance)
        {
            for (int i = 0; i + 3 < planes.Length; i += 4)
            {
                if ((planes[i] * point.x) + (planes[i + 1] * point.y) + (planes[i + 2] * point.z) - planes[i + 3] > tolerance)
                    return false;
            }

            return true;
        }

        /// <summary>The enclosed volume of outward-wound triangles, each against the mean of the vertices they use.</summary>
        private static double Volume(double[] x, double[] y, double[] z, int[] triangles)
        {
            if (triangles.Length == 0) return 0.0;

            double ox = 0.0, oy = 0.0, oz = 0.0;

            for (int i = 0; i < triangles.Length; i++)
            {
                ox += x[triangles[i]];
                oy += y[triangles[i]];
                oz += z[triangles[i]];
            }

            ox /= triangles.Length;
            oy /= triangles.Length;
            oz /= triangles.Length;

            double volume = 0.0;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];

                double ax = x[a] - ox, ay = y[a] - oy, az = z[a] - oz;
                double bx = x[b] - ox, by = y[b] - oy, bz = z[b] - oz;
                double cx = x[c] - ox, cy = y[c] - oy, cz = z[c] - oz;

                volume += ax * (by * cz - bz * cy) + ay * (bz * cx - bx * cz) + az * (bx * cy - by * cx);
            }

            return volume / 6.0;
        }

        /// <summary>Every coordinate moved by up to the amount, by a hash of its index, axis and attempt - the same joggle on every run.</summary>
        private static double[] Joggled(double[] values, double amount, int attempt, int axis)
        {
            var joggled = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
                joggled[i] = values[i] + amount * Hash(((ulong)i * 3UL) + (ulong)axis + ((ulong)attempt << 40));

            return joggled;
        }

        /// <summary>SplitMix64 to a number in [-1, 1).</summary>
        private static double Hash(ulong seed)
        {
            unchecked
            {
                ulong z = seed + 0x9E3779B97F4A7C15UL;

                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;

                return ((z >> 11) * (1.0 / 9007199254740992.0)) * 2.0 - 1.0;
            }
        }

        /// <summary>
        /// One pass of Quickhull over fixed coordinates. Faces are triangles in flat lists, three
        /// vertices and three neighbours each - the face across edge k, which runs from vertex k to
        /// vertex k + 1 - with a unit normal and a centroid to measure a point's distance from.
        /// </summary>
        private sealed class Builder
        {
            private readonly double[] x, y, z;
            private readonly int count;
            private readonly double tolerance;
            private readonly double minHeight;
            // Joggled: a point within the tolerance of a face it is tested against for visibility is a
            // precision failure rather than "on the face", since the joggle exists to rule that out.
            private readonly bool strict;

            private readonly List<int>       vertex = new List<int>();
            private readonly List<int>       across = new List<int>();
            private readonly List<double>    nx = new List<double>(), ny = new List<double>(), nz = new List<double>();
            private readonly List<double>    cx = new List<double>(), cy = new List<double>(), cz = new List<double>();
            private readonly List<bool>      alive = new List<bool>();
            private readonly List<List<int>> outside = new List<List<int>>();
            private readonly List<int>       stamp = new List<int>();

            private int currentStamp;

            public Builder(double[] x, double[] y, double[] z, double tolerance, double minHeight, bool strict)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                count = x.Length;
                this.tolerance = tolerance;
                this.minHeight = minHeight;
                this.strict = strict;
            }

            private double Distance(int f, int p)
                => nx[f] * (x[p] - cx[f]) + ny[f] * (y[p] - cy[f]) + nz[f] * (z[p] - cz[f]);

            /// <summary>
            /// A face over three vertices, wound as given; -1 when it is thinner than the minimum height.
            /// The normal is taken at the vertex opposite the longest edge, from the two shorter edges.
            /// </summary>
            private int AddFace(int a, int b, int c)
            {
                double abx = x[b] - x[a], aby = y[b] - y[a], abz = z[b] - z[a];
                double bcx = x[c] - x[b], bcy = y[c] - y[b], bcz = z[c] - z[b];
                double cax = x[a] - x[c], cay = y[a] - y[c], caz = z[a] - z[c];

                double ab = abx * abx + aby * aby + abz * abz;
                double bc = bcx * bcx + bcy * bcy + bcz * bcz;
                double ca = cax * cax + cay * cay + caz * caz;

                double qx, qy, qz;
                double longest;

                if ((ab >= bc) && (ab >= ca))
                {
                    // Opposite c: bc x ca, which is (b - a) x (c - a).
                    qx = bcy * caz - bcz * cay;
                    qy = bcz * cax - bcx * caz;
                    qz = bcx * cay - bcy * cax;
                    longest = ab;
                }
                else if (bc >= ca)
                {
                    // Opposite a: ca x ab, which is -(ab x ca).
                    qx = -(aby * caz - abz * cay);
                    qy = -(abz * cax - abx * caz);
                    qz = -(abx * cay - aby * cax);
                    longest = bc;
                }
                else
                {
                    // Opposite b: ab x bc.
                    qx = aby * bcz - abz * bcy;
                    qy = abz * bcx - abx * bcz;
                    qz = abx * bcy - aby * bcx;
                    longest = ca;
                }

                double length = Math.Sqrt(qx * qx + qy * qy + qz * qz);

                if ((!(length > 0.0)) || (length / Math.Sqrt(longest) < minHeight)) return -1;

                int f = alive.Count;

                vertex.Add(a); vertex.Add(b); vertex.Add(c);
                across.Add(-1); across.Add(-1); across.Add(-1);
                nx.Add(qx / length); ny.Add(qy / length); nz.Add(qz / length);
                cx.Add((x[a] + x[b] + x[c]) / 3.0); cy.Add((y[a] + y[b] + y[c]) / 3.0); cz.Add((z[a] + z[b] + z[c]) / 3.0);
                alive.Add(true);
                outside.Add(null);
                stamp.Add(0);

                return f;
            }

            /// <summary>The live faces' vertices, three per face.</summary>
            public int[] Triangles()
            {
                var triangles = new List<int>();

                for (int f = 0; f < alive.Count; f++)
                {
                    if (!alive[f]) continue;

                    triangles.Add(vertex[3 * f]);
                    triangles.Add(vertex[3 * f + 1]);
                    triangles.Add(vertex[3 * f + 2]);
                }

                return triangles.ToArray();
            }

            /// <summary>The edge of face g that runs from u to v, or -1.</summary>
            private int EdgeOf(int g, int u, int v)
            {
                for (int k = 0; k < 3; k++)
                    if ((vertex[3 * g + k] == u) && (vertex[3 * g + ((k + 1) % 3)] == v)) return k;

                return -1;
            }

            public bool Run(out bool flat, out string failure)
            {
                flat = false;
                failure = null;

                if (count < 4)
                {
                    flat = true;
                    return false;
                }

                // ---- the first simplex: the widest pair along an axis, the point farthest from their
                // line, the point farthest from their plane.

                int[] minIndex = { 0, 0, 0 };
                int[] maxIndex = { 0, 0, 0 };

                for (int p = 1; p < count; p++)
                {
                    if (x[p] < x[minIndex[0]]) minIndex[0] = p;
                    if (x[p] > x[maxIndex[0]]) maxIndex[0] = p;
                    if (y[p] < y[minIndex[1]]) minIndex[1] = p;
                    if (y[p] > y[maxIndex[1]]) maxIndex[1] = p;
                    if (z[p] < z[minIndex[2]]) minIndex[2] = p;
                    if (z[p] > z[maxIndex[2]]) maxIndex[2] = p;
                }

                double[] spread =
                {
                    x[maxIndex[0]] - x[minIndex[0]],
                    y[maxIndex[1]] - y[minIndex[1]],
                    z[maxIndex[2]] - z[minIndex[2]],
                };

                int axis = ((spread[0] >= spread[1]) && (spread[0] >= spread[2])) ? (0) : ((spread[1] >= spread[2]) ? (1) : (2));

                if (spread[axis] <= tolerance)
                {
                    flat = true;
                    return false;
                }

                int v0 = minIndex[axis], v1 = maxIndex[axis];

                double dx = x[v1] - x[v0], dy = y[v1] - y[v0], dz = z[v1] - z[v0];
                double dl = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                dx /= dl; dy /= dl; dz /= dl;

                int v2 = -1;
                double best = 0.0;

                for (int p = 0; p < count; p++)
                {
                    double wx = x[p] - x[v0], wy = y[p] - y[v0], wz = z[p] - z[v0];
                    double qx = wy * dz - wz * dy, qy = wz * dx - wx * dz, qz = wx * dy - wy * dx;
                    double d = qx * qx + qy * qy + qz * qz;

                    if (d > best)
                    {
                        best = d;
                        v2 = p;
                    }
                }

                if ((v2 < 0) || (Math.Sqrt(best) <= tolerance))
                {
                    flat = true;
                    return false;
                }

                double ux = x[v1] - x[v0], uy = y[v1] - y[v0], uz = z[v1] - z[v0];
                double vx = x[v2] - x[v0], vy = y[v2] - y[v0], vz = z[v2] - z[v0];
                double px = uy * vz - uz * vy, py = uz * vx - ux * vz, pz = ux * vy - uy * vx;
                double pl = Math.Sqrt(px * px + py * py + pz * pz);

                px /= pl; py /= pl; pz /= pl;

                int v3 = -1;
                double above = 0.0;

                best = 0.0;

                for (int p = 0; p < count; p++)
                {
                    double d = px * (x[p] - x[v0]) + py * (y[p] - y[v0]) + pz * (z[p] - z[v0]);

                    if (Math.Abs(d) > best)
                    {
                        best = Math.Abs(d);
                        above = d;
                        v3 = p;
                    }
                }

                if ((v3 < 0) || (best <= tolerance))
                {
                    flat = true;
                    return false;
                }

                // The base wound away from the apex, and the three sides each running its base edge
                // the other way round.
                int a = v0, b = v1, c = v2, apex = v3;

                if (above > 0.0)
                {
                    b = v2;
                    c = v1;
                }

                int[] simplex =
                {
                    AddFace(a, b, c),
                    AddFace(b, a, apex),
                    AddFace(c, b, apex),
                    AddFace(a, c, apex),
                };

                foreach (int f in simplex)
                {
                    if (f < 0)
                    {
                        failure = "the first simplex has a face too thin to measure against";
                        return false;
                    }
                }

                if (!LinkByVertices(simplex, out failure)) return false;

                // ---- every other point to the face it is farthest outside, if any

                for (int p = 0; p < count; p++)
                {
                    if ((p == v0) || (p == v1) || (p == v2) || (p == v3)) continue;

                    Assign(p, simplex);
                }

                var pending = new Stack<int>();

                foreach (int f in simplex)
                    if (outside[f] != null) pending.Push(f);

                var visible = new List<int>();
                var search = new Stack<int>();
                var hu = new List<int>();
                var hv = new List<int>();
                var hg = new List<int>();
                var hk = new List<int>();
                var startAt = new Dictionary<int, int>();
                var order = new List<int>();
                var created = new List<int>();
                var orphans = new List<int>();

                int iterations = 0;

                while (pending.Count > 0)
                {
                    int f = pending.Pop();

                    if ((!alive[f]) || (outside[f] == null) || (outside[f].Count == 0)) continue;

                    if (++iterations > count)
                    {
                        failure = "more iterations than points";
                        return false;
                    }

                    // ---- the eye: the farthest point outside the face

                    int eye = -1;
                    double far = tolerance;

                    foreach (int p in outside[f])
                    {
                        double d = Distance(f, p);

                        if (d > far)
                        {
                            far = d;
                            eye = p;
                        }
                    }

                    if (eye < 0)
                    {
                        // Cannot happen: every point was assigned for being farther than the tolerance.
                        failure = "a face's outside set has no point outside it";
                        return false;
                    }

                    // ---- the faces the eye sees, grown from this one across its neighbours

                    currentStamp++;
                    visible.Clear();
                    search.Clear();

                    stamp[f] = currentStamp;
                    search.Push(f);

                    while (search.Count > 0)
                    {
                        int g = search.Pop();

                        visible.Add(g);

                        for (int k = 0; k < 3; k++)
                        {
                            int h = across[3 * g + k];

                            if (stamp[h] == currentStamp) continue;

                            double d = Distance(h, eye);

                            if (strict && (Math.Abs(d) <= tolerance))
                            {
                                failure = "a point lies on a face within rounding";
                                return false;
                            }

                            if (d > tolerance)
                            {
                                stamp[h] = currentStamp;
                                search.Push(h);
                            }
                        }
                    }

                    // ---- the horizon: the visible faces' edges to faces it does not see, as one cycle

                    hu.Clear(); hv.Clear(); hg.Clear(); hk.Clear();
                    startAt.Clear();

                    foreach (int g in visible)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            int h = across[3 * g + k];

                            if (stamp[h] == currentStamp) continue;

                            int u = vertex[3 * g + k], v = vertex[3 * g + ((k + 1) % 3)];
                            int back = EdgeOf(h, v, u);

                            if (back < 0)
                            {
                                failure = "a face's neighbour does not run the shared edge the other way";
                                return false;
                            }

                            if (startAt.ContainsKey(u))
                            {
                                failure = "the visible region is not a disc - its horizon passes a vertex twice";
                                return false;
                            }

                            startAt.Add(u, hu.Count);
                            hu.Add(u);
                            hv.Add(v);
                            hg.Add(h);
                            hk.Add(back);
                        }
                    }

                    int edges = hu.Count;

                    if (edges < 3)
                    {
                        failure = "a horizon of fewer than three edges";
                        return false;
                    }

                    order.Clear();

                    int current = 0;

                    for (int s = 0; s < edges; s++)
                    {
                        order.Add(current);

                        if (!startAt.TryGetValue(hv[current], out int next))
                        {
                            failure = "the horizon does not close";
                            return false;
                        }

                        current = next;

                        if ((current == 0) && (s != edges - 1))
                        {
                            failure = "the visible region is not a disc - its horizon is more than one cycle";
                            return false;
                        }
                    }

                    if (current != 0)
                    {
                        failure = "the horizon does not close";
                        return false;
                    }

                    // ---- the cone from the eye over the horizon

                    created.Clear();

                    for (int s = 0; s < edges; s++)
                    {
                        int i = order[s];
                        int nf = AddFace(hu[i], hv[i], eye);

                        if (nf < 0)
                        {
                            failure = "a new face too thin to measure against";
                            return false;
                        }

                        created.Add(nf);
                    }

                    for (int s = 0; s < edges; s++)
                    {
                        int i = order[s];
                        int nf = created[s];

                        across[3 * nf + 0] = hg[i];
                        across[3 * hg[i] + hk[i]] = nf;
                        across[3 * nf + 1] = created[(s + 1) % edges];
                        across[3 * nf + 2] = created[(s + edges - 1) % edges];
                    }

                    // ---- the visible faces retired, their points handed to the new faces

                    orphans.Clear();

                    foreach (int g in visible)
                    {
                        alive[g] = false;

                        if (outside[g] != null)
                        {
                            foreach (int p in outside[g])
                                if (p != eye) orphans.Add(p);

                            outside[g] = null;
                        }
                    }

                    foreach (int p in orphans) Assign(p, created);

                    foreach (int nf in created)
                        if (outside[nf] != null) pending.Push(nf);
                }

                return Validate(out failure);
            }

            /// <summary>The point to the listed face it is farthest outside, beyond the tolerance; nowhere if none.</summary>
            private void Assign(int p, IList<int> faces)
            {
                int target = -1;
                double far = tolerance;

                for (int i = 0; i < faces.Count; i++)
                {
                    double d = Distance(faces[i], p);

                    if (d > far)
                    {
                        far = d;
                        target = faces[i];
                    }
                }

                if (target < 0) return;

                if (outside[target] == null) outside[target] = new List<int>();

                outside[target].Add(p);
            }

            /// <summary>The faces' neighbours, found by matching each edge with the one running the other way.</summary>
            private bool LinkByVertices(IList<int> faces, out string failure)
            {
                failure = null;

                var edgeAt = new Dictionary<long, int>();

                foreach (int f in faces)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        long key = ((long)vertex[3 * f + k] << 32) | (uint)vertex[3 * f + ((k + 1) % 3)];

                        if (edgeAt.ContainsKey(key))
                        {
                            failure = "two faces run the same edge the same way";
                            return false;
                        }

                        edgeAt.Add(key, 3 * f + k);
                    }
                }

                foreach (int f in faces)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        long twin = ((long)vertex[3 * f + ((k + 1) % 3)] << 32) | (uint)vertex[3 * f + k];

                        if (!edgeAt.TryGetValue(twin, out int slot))
                        {
                            failure = "an edge with no face on its other side";
                            return false;
                        }

                        across[3 * f + k] = slot / 3;
                    }
                }

                return true;
            }

            /// <summary>
            /// The live faces close up: every neighbour alive and running the shared edge back, and the
            /// vertices, edges and faces in Euler's ratio for a sphere.
            /// </summary>
            private bool Validate(out string failure)
            {
                failure = null;

                int faces = 0;
                var used = new HashSet<int>();

                for (int f = 0; f < alive.Count; f++)
                {
                    if (!alive[f]) continue;

                    faces++;

                    for (int k = 0; k < 3; k++)
                    {
                        int h = across[3 * f + k];

                        if ((h < 0) || (!alive[h]) || (EdgeOf(h, vertex[3 * f + ((k + 1) % 3)], vertex[3 * f + k]) < 0))
                        {
                            failure = "the finished hull is not closed";
                            return false;
                        }

                        used.Add(vertex[3 * f + k]);
                    }
                }

                if (((3 * faces) % 2 != 0) || (used.Count - (3 * faces / 2) + faces != 2))
                {
                    failure = $"the finished hull has {used.Count} vertices, {3 * faces / 2} edges and {faces} faces, not a sphere";
                    return false;
                }

                return true;
            }
        }
    }
}
