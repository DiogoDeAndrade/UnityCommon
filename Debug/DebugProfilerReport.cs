using System;
using System.Collections.Generic;
using System.Text;

namespace UC
{
    /// <summary>
    /// A tree of named timers in the order their stages ran, rendered as the table the solver's
    /// time report prints.
    ///
    /// A stage opens with Begin and closes when the scope it returns is disposed, so a stage begun
    /// inside another is its child, and a return out of the middle of one still closes it. A stage
    /// begun under the same parent with the same name as an earlier one is the same row: its time
    /// accumulates and the row says how many times it ran, so a step a build repeats reads as
    /// repeated rather than as two rows that happen to share a name.
    ///
    /// The clock is read unconditionally, where DebugProfiler.DebugMark compiles out without
    /// UC_PROFILER_ENABLE. The stages this describes run once a build and cost up to seconds each;
    /// a timestamp per stage is not a cost worth a compile switch, and a table of zeros under a
    /// switch that is off would read as stages that cost nothing.
    ///
    /// Main thread only: stages open and close in one nested order, and the open stack has no lock.
    /// </summary>
    public class DebugProfilerReport
    {
        public class Row
        {
            public string           name;
            public int              depth;
            public int              calls;
            public DebugProfiler    timer = new DebugProfiler();
            public List<Row>        children = new List<Row>();

            public double milliseconds => timer.accumulatedTimeMS;
        }

        /// <summary>
        /// Closes its stage when disposed. Returned by Begin; keep it in a using, so the stage
        /// closes on every way out of the code it times.
        /// </summary>
        public class Scope : IDisposable
        {
            private DebugProfilerReport report;
            private readonly Row        row;

            internal Scope(DebugProfilerReport report, Row row)
            {
                this.report = report;
                this.row = row;
            }

            public void Dispose()
            {
                if (report == null) return;

                report.End(row);
                report = null;
            }
        }

        private readonly List<Row>  roots = new List<Row>();
        private readonly Stack<Row> open = new Stack<Row>();

        /// <summary>The top-level stages, in the order they first ran; each carries its children.</summary>
        public IReadOnlyList<Row> rootRows => roots;

        /// <summary>
        /// Opens a stage under whichever stage is open, or at the top when none is. The row is the
        /// one already there under that parent with this name, or a new one.
        /// </summary>
        public Scope Begin(string name)
        {
            List<Row> siblings = (open.Count > 0) ? (open.Peek().children) : (roots);

            Row row = null;

            foreach (Row sibling in siblings)
            {
                if (sibling.name == name)
                {
                    row = sibling;
                    break;
                }
            }

            if (row == null)
            {
                row = new Row { name = name, depth = open.Count };
                siblings.Add(row);
            }

            row.calls++;
            row.timer.Mark();
            open.Push(row);

            return new Scope(this, row);
        }

        private void End(Row row)
        {
            // A scope disposed after its stage was already closed - by a parent's scope disposed
            // first - has nothing left to close. Checked before popping, or the loop below would
            // empty the stack looking for it.
            if (!open.Contains(row)) return;

            // Close everything opened inside the stage as well: a child whose scope outlives its
            // parent's would otherwise be left open, and every stage after it would be filed under
            // it.
            while (open.Count > 0)
            {
                Row top = open.Pop();

                top.timer.Mark();

                if (top == row) return;
            }
        }

        /// <summary>
        /// The table: one line per stage, the name indented by its depth, its time, its share of
        /// the top-level stages' sum, and how many times it ran when more than once. A stage whose
        /// children leave more than a hundredth of it - and more than a millisecond - unaccounted
        /// for gets a "(not itemised)" line after them, so a cost nothing was named for is seen
        /// rather than found by subtraction.
        /// </summary>
        public string Render(string title, IEnumerable<string> subtitleLines = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine(title);

            if (subtitleLines != null)
            {
                foreach (string line in subtitleLines)
                    sb.AppendLine("  " + line);
            }

            double whole = 0.0;

            foreach (Row root in roots)
                whole += root.milliseconds;

            foreach (Row root in roots)
                RenderRow(sb, root, whole);

            return sb.ToString();
        }

        private static void RenderRow(StringBuilder sb, Row row, double whole)
        {
            int indent = 2 + 2 * row.depth;

            AppendLine(sb, row.name, indent, row.milliseconds, whole, row.calls);

            if (row.children.Count == 0) return;

            double itemised = 0.0;

            foreach (Row child in row.children)
            {
                RenderRow(sb, child, whole);

                itemised += child.milliseconds;
            }

            double rest = row.milliseconds - itemised;

            if (rest > Math.Max(1.0, 0.01 * row.milliseconds))
                AppendLine(sb, "(not itemised)", indent + 2, rest, whole, 1);
        }

        private static void AppendLine(StringBuilder sb, string name, int indent, double milliseconds, double whole, int calls)
        {
            string line = new string(' ', indent) + name.PadRight(Math.Max(0, 34 - indent)) + $"{milliseconds,10:F3} ms";

            if (whole > 0.0) line += $"  {100.0 * milliseconds / whole,5:F1}%";
            if (calls > 1) line += $"  (x{calls})";

            sb.AppendLine(line);
        }
    }
}
