using System.Collections.Immutable;
using System.Text;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class MarkdownWriter
{
    public static string Write(AssayVerdict verdict, ImmutableArray<ReportArtifact> artifacts = default)
    {
        var report = ReadableReport.From(verdict, artifacts); var builder = new StringBuilder();
        builder.Append("# CSharpAssay: ").Append(report.Headline).Append("\n\n").Append(E(report.Summary)).Append("\n\n");
        foreach (var table in report.Tables)
        {
            builder.Append("## ").Append(table.Title).Append("\n\n");
            if (table.Rows.IsEmpty) { builder.Append(E(table.EmptyMessage)).Append("\n\n"); continue; }
            Row(table.Headers); builder.Append('|').AppendJoin('|', table.Headers.Select(_ => " --- ")).Append("|\n");
            foreach (var row in table.Rows) Row(row);
            builder.Append('\n');
        }
        Findings("Active findings", report.Active); Findings("Accepted suppressed observations", report.Suppressed);
        builder.Append("This is a recorded evidence summary, not a universal correctness or security proof. Per-file atomic report writing is not a multi-file transaction.\n");
        return builder.ToString();
        void Row(IEnumerable<string> cells) => builder.Append("| ").AppendJoin(" | ", cells.Select(E)).Append(" |\n");
        void Findings(string title, ImmutableArray<PresentedFinding> items)
        {
            builder.Append("## ").Append(title).Append("\n\n");
            if (items.IsEmpty) { builder.Append("None recorded.\n\n"); return; }
            Row(["Rule", "Location", "Effective severity", "Scope", "Message", "Documentation"]); builder.Append("| --- | --- | --- | --- | --- | --- |\n");
            foreach (var item in items)
            {
                builder.Append("| ").AppendJoin(" | ", ReadableReport.FindingCells(item).Select(E)).Append(" | ");
                var url = ReadableReport.Documentation(item);
                builder.Append(url.Length > 0 ? "[Rule guide](" + url + ")" : "Unavailable").Append(" |\n");
            }
            builder.Append('\n');
        }
    }
    private static string E(string value) => ReportEscaping.Markdown(value);
}
