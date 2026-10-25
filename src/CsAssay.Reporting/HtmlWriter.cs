using System.Collections.Immutable;
using System.Text;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class HtmlWriter
{
    public static string Write(AssayVerdict verdict) => Write(verdict, []);

    public static string Write(AssayVerdict verdict, ImmutableArray<ReportArtifact> artifacts)
    {
        var report = ReadableReport.From(verdict, artifacts); var builder = new StringBuilder();
        builder.Append("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">\n")
            .Append("<title>CSharpAssay report</title>\n<style>body{font:16px system-ui;max-width:80rem;margin:2rem auto;padding:0 1rem}table{border-collapse:collapse;width:100%;margin-bottom:1rem}th,td{border:1px solid #bbb;padding:.45rem;text-align:left;overflow-wrap:anywhere}thead{background:#eee}.table{overflow-x:auto}h1{font-size:1.8rem}</style></head><body>\n")
            .Append("<h1>CSharpAssay: ").Append(E(report.Headline)).Append("</h1>\n<p>").Append(E(report.Summary)).Append("</p>\n");
        foreach (var table in report.Tables)
        {
            builder.Append("<h2>").Append(E(table.Title)).Append("</h2>\n");
            if (table.Rows.IsEmpty) { builder.Append("<p>").Append(E(table.EmptyMessage)).Append("</p>\n"); continue; }
            Start(table.Headers);
            foreach (var row in table.Rows) Row(row);
            End();
        }
        Findings("Active findings", report.Active); Findings("Accepted suppressed observations", report.Suppressed);
        builder.Append("<p>This is a recorded evidence summary, not a universal correctness or security proof. Per-file atomic report writing is not a multi-file transaction.</p>\n</body></html>\n");
        return builder.ToString();
        void Start(IEnumerable<string> headers)
        {
            builder.Append("<div class=\"table\"><table><thead><tr>");
            foreach (var header in headers) builder.Append("<th>").Append(E(header)).Append("</th>");
            builder.Append("</tr></thead><tbody>\n");
        }
        void Row(IEnumerable<string> cells)
        {
            builder.Append("<tr>"); foreach (var cell in cells) builder.Append("<td>").Append(E(cell)).Append("</td>"); builder.Append("</tr>\n");
        }
        void End() => builder.Append("</tbody></table></div>\n");
        void Findings(string title, ImmutableArray<PresentedFinding> items)
        {
            builder.Append("<h2>").Append(E(title)).Append("</h2>\n");
            if (items.IsEmpty) { builder.Append("<p>None recorded.</p>\n"); return; }
            Start(["Rule", "Location", "Effective severity", "Scope", "Message", "Documentation"]);
            foreach (var item in items)
            {
                builder.Append("<tr>");
                foreach (var cell in ReadableReport.FindingCells(item)) builder.Append("<td>").Append(E(cell)).Append("</td>");
                var url = ReadableReport.Documentation(item);
                builder.Append("<td>").Append(url.Length > 0 ? "<a href=\"" + E(url) + "\">Rule guide</a>" : "Unavailable").Append("</td></tr>\n");
            }
            End();
        }
    }
    private static string E(string value) => ReportEscaping.Html(value);
}
