using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UC.DoubleMath;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Golden-dump capture. Everything here is read-only with respect to the solve; it exists so a
    /// refactor can be proven not to have changed a single bit of the result.
    ///
    /// The dump is deliberately structured as sections in a fixed order, one record per line, so
    /// that a plain line diff localises a regression to a specific block (see
    /// <see cref="EDDiagnostics.CompareGolden"/>).
    /// </summary>
    public partial class EmbededDeformation
    {
        /// <summary>
        /// Writes the state that exists before a solve: the graph, the bindings, the structure and
        /// the constraints. Call this after Build/UpdateBindings and before running the solver.
        /// </summary>
        public void DumpStaticState(TextWriter w)
        {
            DumpGraph(w);
            DumpBindings(w);
            DumpStructure(w);
            DumpConstraints(w);
        }

        /// <summary>
        /// Writes the state that exists after a solve: the solved parameter vector and the
        /// resulting clearances.
        /// </summary>
        public void DumpSolvedState(TextWriter w)
        {
            w.WriteLine("[final]");

            if (currentState == null)
            {
                w.WriteLine("state null");
                return;
            }

            w.WriteLine($"paramCount {currentState.Count}");

            // One node per line rather than one double per line: 12 doubles is the natural
            // record here, and it keeps a divergence report pointing at a node index.
            for (int i = 0; (i * 12) < currentState.Count; i++)
            {
                int b = i * 12;
                var values = new double[12];
                for (int j = 0; j < 12; j++) values[j] = currentState.Get(b + j);

                w.WriteLine($"node {i} {EDDiagnostics.F(values)}");
            }

            w.WriteLine("[clearance]");

            int clearanceCount = (currentState.clearances != null) ? (currentState.clearances.count) : (0);

            if (clearanceCount == 0)
            {
                w.WriteLine("none");
                return;
            }

            for (int i = 0; i < clearanceCount; i++)
                w.WriteLine($"seg {i} {EDDiagnostics.F(currentState.clearances.Get(i))}");
        }

        private void DumpGraph(TextWriter w)
        {
            w.WriteLine("[graph]");
            w.WriteLine($"source {deformationGraphSource}");
            w.WriteLine($"nodeCount {((nodes != null) ? (nodes.Count) : (0))}");
            w.WriteLine($"vertexCount {((restVertices != null) ? (restVertices.Length) : (0))}");
            w.WriteLine($"triangleCount {((triangles != null) ? (triangles.Length / 3) : (0))}");

            if (nodes == null) return;

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];

                // Neighbour order is an artefact of insertion, not part of the numerical
                // contract - sorting keeps the dump stable if a builder ever reorders them.
                var neighbours = (n.neighbors != null) ? (new List<int>(n.neighbors)) : (new List<int>());
                neighbours.Sort();

                w.WriteLine($"node {i} pos {EDDiagnostics.F(n.restPosition)} right {EDDiagnostics.F(n.restRight)} up {EDDiagnostics.F(n.restUp)} fwd {EDDiagnostics.F(n.restForward)} nbr {EDDiagnostics.F(neighbours)}");
            }
        }

        private void DumpBindings(TextWriter w)
        {
            w.WriteLine("[bindings]");

            if (bindings == null)
            {
                w.WriteLine("none");
                return;
            }

            w.WriteLine($"count {bindings.Length}");

            for (int i = 0; i < bindings.Length; i++)
                w.WriteLine($"vert {i} {FormatBinding(bindings[i])}");
        }

        private void DumpStructure(TextWriter w)
        {
            w.WriteLine("[structure]");

            if (structure == null)
            {
                w.WriteLine("none");
                return;
            }

            w.WriteLine($"count {structure.Count}");

            for (int i = 0; i < structure.Count; i++)
            {
                var s = structure[i];

                w.WriteLine($"seg {i} p1 {EDDiagnostics.F(s.p1)} p2 {EDDiagnostics.F(s.p2)} normal {EDDiagnostics.F(s.normal)} nodes {s.node1} {s.node2}");
                w.WriteLine($"seg {i} probeT {EDDiagnostics.F(s.probeT)} probeB {EDDiagnostics.F(s.probeB)} center {EDDiagnostics.F(s.center)}");
                w.WriteLine($"seg {i} bind1 {FormatBinding(s.bind1)} bind2 {FormatBinding(s.bind2)}");
                w.WriteLine($"seg {i} cBind {FormatBinding(s.cBind)} tBind {FormatBinding(s.tBind)} bBind {FormatBinding(s.bBind)}");

                // Only NavED builds populate the rest clearances, so this section is absent for
                // the other modes rather than zero-filled.
                if ((restState != null) && (restState.clearances != null) && (i < restState.clearances.count))
                    w.WriteLine($"seg {i} restClearance {EDDiagnostics.F(restState.clearances.Get(i))}");
            }
        }

        private void DumpConstraints(TextWriter w)
        {
            w.WriteLine("[constraints]");

            w.WriteLine($"handle {((handleConstraints != null) ? (handleConstraints.Count) : (0))}");
            if (handleConstraints != null)
            {
                for (int i = 0; i < handleConstraints.Count; i++)
                {
                    var c = handleConstraints[i];
                    w.WriteLine($"handle {i} width {EDDiagnostics.F(c.width)} terminal {c.isTerminal} verts {((c.vertexIndices != null) ? (c.vertexIndices.Count) : (0))}");
                    w.WriteLine($"handle {i} rest {FormatMatrix(c.restHandleMatrix)}");
                    w.WriteLine($"handle {i} current {FormatMatrix(c.currentHandleMatrix)}");
                }
            }

            w.WriteLine($"vertex {((vertexConstraints != null) ? (vertexConstraints.Count) : (0))}");
            if (vertexConstraints != null)
            {
                for (int i = 0; i < vertexConstraints.Count; i++)
                    w.WriteLine($"vertex {i} idx {vertexConstraints[i].vertexIndex} target {EDDiagnostics.F(vertexConstraints[i].targetPosition)}");
            }

            w.WriteLine($"terminal {((terminalConstraints != null) ? (terminalConstraints.Count) : (0))}");
            if (terminalConstraints != null)
            {
                for (int i = 0; i < terminalConstraints.Count; i++)
                {
                    var t = terminalConstraints[i];
                    w.WriteLine($"terminal {i} node {t.nodeIndex} pos {EDDiagnostics.F(t.targetPosition)} right {EDDiagnostics.F(t.targetRight)} up {EDDiagnostics.F(t.targetUp)} fwd {EDDiagnostics.F(t.targetForward)} scale {EDDiagnostics.F(t.targetScale)}");
                }
            }

            w.WriteLine($"linkAngle {((linkAngleConstraints != null) ? (linkAngleConstraints.Count) : (0))}");
            if (linkAngleConstraints != null)
            {
                for (int i = 0; i < linkAngleConstraints.Count; i++)
                {
                    var l = linkAngleConstraints[i];
                    w.WriteLine($"linkAngle {i} center {l.centerNode} a {l.neighborA} b {l.neighborB} cos {EDDiagnostics.F(l.restCos)} sin {EDDiagnostics.F(l.restSin)}");
                }
            }
        }

        private static string FormatBinding(EDVertexBinding b)
            => $"[{EDDiagnostics.F(b.nodeIndices)}] [{EDDiagnostics.F(b.weights)}]";

        private static string FormatMatrix(Matrix4x4 m)
        {
            var values = new double[16];
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    values[r * 4 + c] = m[r, c];

            return EDDiagnostics.F(values);
        }
    }
}
#endif
