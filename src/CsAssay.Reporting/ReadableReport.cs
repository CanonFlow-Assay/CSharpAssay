using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public sealed record ReportArtifact(string Format, string Path);
public sealed record ReportTable(string Title, ImmutableArray<string> Headers, ImmutableArray<ImmutableArray<string>> Rows, string EmptyMessage);

/// <summary>Presentation only. Consumes an already completed verdict; never evaluates policy or executes checks.</summary>
public sealed record ReadableReport(string Headline, string Summary, ImmutableArray<ReportTable> Tables,
    ImmutableArray<PresentedFinding> Active, ImmutableArray<PresentedFinding> Suppressed)
{
    public static ReadableReport From(AssayVerdict verdict, ImmutableArray<ReportArtifact> artifacts)
    {
        var e = verdict.Evidence;
        var findings = DiagnosticPresentation.Findings(verdict);
        var counts = DiagnosticPresentation.Counts(findings);
        var diagnostics = DiagnosticPresentation.ExecutionDiagnostics(verdict);
        var tables = ImmutableArray.CreateBuilder<ReportTable>();
        tables.Add(new("Requested scope and authority", ["Field", "Recorded value"],
            [["Requested input", e.Input], ["Verification mode", e.VerificationMode.ToString()], ["Authority flag", e.IsAuthoritative ? "true" : "false"],
             ["Verdict / exit", verdict.Kind + " / " + N(verdict.ExitCode)], ["Requested / effective profile", e.RequestedProfile + " / " + e.Profile],
             ["Project compilations", N(e.Projects.Length)]], "No scope recorded."));
        var commandRows = e.Invocation is Presence<CommandEvidence>.Present invocation
            ? ImmutableArray.Create(ImmutableArray.Create("Working directory", invocation.Value.WorkingDirectory),
                ImmutableArray.Create("Executable", invocation.Value.Executable),
                ImmutableArray.Create("Arguments (JSON array)", JsonSerializer.Serialize(invocation.Value.Arguments)))
            : [];
        tables.Add(new("Repeat this invocation", ["Field", "Recorded value"], commandRows,
            "Unknown: this result did not record a CLI invocation. Consult the caller's command record."));
        tables.Add(new("Effective findings", ["Category", "Count"],
            [["Active blocking findings", N(counts.BlockingFindings)], ["Shell warnings", N(counts.ShellWarnings)],
             ["Default-scope warnings", N(counts.DefaultWarnings)], ["Other-scope warnings", N(counts.OtherWarnings)],
             ["Advisory findings", N(counts.AdvisoryFindings)], ["Accepted suppressions", N(counts.AcceptedSuppressions)],
             ["Missing evidence", N(e.Missing.Length)], ["Execution/configuration failures", N(e.Failures.Length)],
             ["Execution diagnostics (separate from finding counts)", N(diagnostics.Length)]], "No counts recorded."));
        var testsUnknown = e.Failures.Any(f => f.Component == "policy") || e.Missing.Any(m => m.Code.StartsWith("CSASSAY-CONFIG", StringComparison.Ordinal));
        tables.Add(new("Required tests", ["Input", "Configuration", "Required", "Outcome", "Total", "Passed", "Failed", "Skipped"],
            e.Tests.Select(t => ImmutableArray.Create(t.Input, t.Configuration, t.Required ? "yes" : "no", t.Outcome == TestRunOutcome.NotRun ? "Not run" : t.Outcome.ToString(),
                N(t.Total), N(t.Passed), N(t.Failed), N(t.Skipped))).ToImmutableArray(),
            testsUnknown ? "No test results recorded; policy configuration was not established." : "None configured. This does not establish application behavior."));
        tables.Add(new("Failed test details", ["Required input", "Test", "Runner message"],
            e.Tests.SelectMany(t => t.FailureDetails.Select(d => ImmutableArray.Create(t.Input, d.TestName,
                d.Message.Length > 0 ? d.Message : "Unavailable: runner supplied no assertion message.")))
                .Concat(e.Tests.Where(t => t.FailureDetailsTruncated).Select(t => ImmutableArray.Create(t.Input, "Truncated", "Detail limit reached; aggregate counts remain complete.")))
                .ToImmutableArray(), "No structured failed-test detail recorded."));
        tables.Add(new("Missing evidence", ["Code", "Project", "Target", "Reason"],
            e.Missing.Select(m => ImmutableArray.Create(m.Code, m.Project, m.TargetFramework, m.Message)).ToImmutableArray(), "None recorded."));
        tables.Add(new("Execution/configuration diagnostics", ["Kind", "Code", "Message"],
            diagnostics.Select(d => ImmutableArray.Create(d.Kind, d.Code, d.Message)).ToImmutableArray(), "None recorded."));
        tables.Add(new("Recorded tool and policy identities", ["Identity", "Value"],
            [["Tool version", e.ToolVersion], ["Policy origin / path", e.Policy.Origin + " / " + e.Policy.Path], ["Policy bytes SHA-256", e.Policy.Sha256],
             ["Policy text SHA-256", e.PolicyTextSha256], ["Policy evaluation date", e.PolicyEvaluationDate], ["Finding identity root", e.FindingIdentityRoot],
             ["Recorded SDK", e.Toolchain.SdkVersion], ["Running runtime", e.Toolchain.RuntimeVersion], ["Recorded MSBuild", e.Toolchain.MsBuildVersion],
             ["Roslyn", e.Toolchain.RoslynVersion], ["Recorded OS", e.Toolchain.OperatingSystem]], "No identities recorded."));
        tables.Add(new("Analyzer identities", ["Identity", "Assembly version", "SHA-256"],
            e.Analyzers.Select(a => ImmutableArray.Create(a.Identity, a.AssemblyVersion, a.Sha256)).ToImmutableArray(), "None recorded."));
        tables.Add(new("Project scope", ["Project", "Path", "Target", "Profile", "Language", "Nullable", "Loaded"],
            e.Projects.Select(p => ImmutableArray.Create(p.Name, p.Path, p.TargetFramework, p.Profile.ToString(), p.LanguageVersion, p.NullableContext, p.Loaded ? "yes" : "no")).ToImmutableArray(), "No project compilations recorded."));
        tables.Add(new("Suppression authorization observations", ["Rule", "Form", "Authorized", "Owner", "Evaluation date", "Fingerprint", "Reason"],
            e.Suppressions.Select(s => ImmutableArray.Create(s.RuleId, s.Form, s.Authorized ? "yes" : "no", s.Owner, s.EvaluationDate, s.Fingerprint, s.Justification)).ToImmutableArray(), "None recorded."));
        tables.Add(new("Report artifacts", ["Format", "Requested path"], artifacts.IsDefault ? [] : artifacts.Select(a => ImmutableArray.Create(a.Format, a.Path)).ToImmutableArray(),
            "No artifact paths supplied. Complete machine evidence belongs in JSON/SARIF, not this summary."));
        return new(DiagnosticPresentation.Headline(verdict), DiagnosticPresentation.Summary(verdict), tables.ToImmutable(),
            findings.Where(f => !f.AcceptedSuppression).ToImmutableArray(), findings.Where(f => f.AcceptedSuppression).ToImmutableArray());
    }

    public static string Documentation(PresentedFinding item) => RuleCatalogue.Find(item.Finding.RuleId) is Presence<RuleRecord>.Present found
        ? RuleCatalogue.DocumentationUrl(found.Value) : "";
    public static ImmutableArray<string> FindingCells(PresentedFinding item) => [item.Finding.RuleId,
        item.Finding.Location.Path + ":" + N(item.Finding.Location.StartLine) + ":" + N(item.Finding.Location.StartColumn),
        item.Level, item.Finding.Scope, item.Message];
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}

internal static class ReportEscaping
{
    public static string Html(string value) => WebUtility.HtmlEncode(DiagnosticPresentation.Sanitize(value));
    public static string Markdown(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in DiagnosticPresentation.Sanitize(value))
        {
            switch (character)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\'': builder.Append("&#39;"); break;
                case '\\' or '|' or '[' or ']' or '(' or ')' or '*' or '_' or '`' or '!' or '#' or '~': builder.Append('\\').Append(character); break;
                default: builder.Append(character); break;
            }
        }
        return builder.ToString();
    }
}
