using System.Net;
using System.Text;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public static class HtmlWriter
{
    public static string Write(AssayVerdict verdict)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        builder.AppendLine("<title>CSharpAssay report</title>");
        builder.AppendLine("<style>body{font:16px system-ui;max-width:72rem;margin:2rem auto;padding:0 1rem}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:.45rem;text-align:left}code{white-space:pre-wrap}.pass{color:#176b2c}.fail,.toolfailure{color:#a11616}.inconclusive{color:#875a00}</style>");
        builder.AppendLine("</head><body>");
        builder.Append("<h1>CSharpAssay <span class=\"")
            .Append(verdict.Kind.ToString().ToLowerInvariant())
            .Append("\">")
            .Append(WebUtility.HtmlEncode(verdict.Kind.ToString()))
            .AppendLine("</span></h1>");
        builder.Append("<p>")
            .Append(verdict.Evidence.Projects.Length)
            .Append(" project compilations, ")
            .Append(verdict.Evidence.Findings.Length)
            .Append(" findings, ")
            .Append(verdict.Evidence.Tests.Sum(test => test.Total))
            .Append(" tests, ")
            .Append(verdict.Evidence.Missing.Length)
            .Append(" missing-evidence entries, ")
            .Append(verdict.Evidence.Failures.Length)
            .AppendLine(" tool failures.</p>");
        builder.AppendLine("<table><thead><tr><th>Rule</th><th>Location</th><th>Message</th></tr></thead><tbody>");
        foreach (var finding in verdict.Evidence.Findings)
        {
            builder.Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(finding.RuleId))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(
                    finding.Location.Path + ":" + finding.Location.StartLine))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(finding.Message))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
        builder.AppendLine("<h2>Required tests</h2>");
        builder.AppendLine("<table><thead><tr><th>Input</th><th>Outcome</th><th>Passed</th><th>Failed</th><th>Skipped</th></tr></thead><tbody>");
        foreach (var test in verdict.Evidence.Tests)
        {
            builder.Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(test.Input))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(test.Outcome.ToString()))
                .Append("</td><td>")
                .Append(test.Passed)
                .Append("</td><td>")
                .Append(test.Failed)
                .Append("</td><td>")
                .Append(test.Skipped)
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table></body></html>");
        return builder.ToString();
    }
}
