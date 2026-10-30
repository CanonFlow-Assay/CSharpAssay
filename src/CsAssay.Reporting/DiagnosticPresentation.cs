using System.Collections.Immutable;
using System.Text;
using CsAssay.Catalogue;
using CsAssay.Domain;

namespace CsAssay.Reporting;

public enum ConsoleFormat { Msbuild, Text }

public sealed record PresentedFinding(Finding Finding, string Level, string Message, bool AcceptedSuppression);
public sealed record ExecutionDiagnostic(string Code, string Message, string Kind);
public sealed record DiagnosticCounts(int BlockingFindings, int ShellWarnings, int DefaultWarnings, int OtherWarnings, int AdvisoryFindings, int AcceptedSuppressions)
{
    public int Warnings => ShellWarnings + DefaultWarnings + OtherWarnings;
}

/// <summary>Shared effective presentation; raw JSON severity remains unchanged.</summary>
public static class DiagnosticPresentation
{
    public static ImmutableArray<PresentedFinding> Findings(AssayVerdict verdict) => verdict.Evidence.Findings.Select(finding =>
    {
        var integrity = finding.RuleId == "CSAP0001";
        var accepted = finding.Suppressed && !integrity;
        var admitted = RuleCatalogue.Find(finding.RuleId) is Presence<RuleRecord>.Present rule &&
            rule.Value.Status == RuleStatus.Admitted && rule.Value.Certainty == RuleCertainty.Deterministic &&
            rule.Value.Disposition == RuleDisposition.Block;
        var blocking = integrity || admitted && finding.Certainty == RuleCertainty.Deterministic && finding.Disposition == RuleDisposition.Block;
        return new PresentedFinding(finding, accepted ? "note" : blocking ? "error" : admitted ? "warning" : "note", Sanitize(finding.Message), accepted);
    }).ToImmutableArray();

    public static ImmutableArray<ExecutionDiagnostic> ExecutionDiagnostics(AssayVerdict verdict) =>
        verdict.Evidence.Missing.Select(item => new ExecutionDiagnostic(item.Code, Sanitize(item.Message), "configuration"))
            .Concat(verdict.Evidence.Failures.Select(item => new ExecutionDiagnostic(item.Code, Sanitize(item.Message), "execution")))
            .Concat(verdict.Evidence.Tests.Where(test => test.Required && test.Outcome == TestRunOutcome.Failed)
                .Select(test => new ExecutionDiagnostic("CSASSAY-REQUIRED-TESTS-FAILED", Sanitize(TestFailureMessage(test)), "test")))
            .ToImmutableArray();

    private static string TestFailureMessage(TestRunEvidence test) =>
        "Required tests failed: " + test.Input + " (" + test.Failed + " failed)." +
        string.Concat(test.FailureDetails.Select(detail => " [" + detail.TestName + "] " +
            (detail.Message.Length > 0 ? detail.Message : "Failure detail unavailable."))) +
        (test.FailureDetailsTruncated ? " [Failure details truncated; counts remain complete.]" : "");

    public static bool ExecutionSuccessful(AssayVerdict verdict) =>
        verdict.Kind is not (AssayVerdictKind.ToolFailure or AssayVerdictKind.Inconclusive) &&
        verdict.Evidence.Failures.IsDefaultOrEmpty && verdict.Evidence.Missing.IsDefaultOrEmpty &&
        !verdict.Evidence.Tests.Any(test => test.Required && test.Outcome == TestRunOutcome.NotRun);

    public static string Summary(AssayVerdict verdict)
    {
        var findings = Findings(verdict);
        var effective = Counts(findings);
        var errors = effective.BlockingFindings + ExecutionDiagnostics(verdict).Length;
        var warnings = effective.Warnings;
        var notes = effective.AdvisoryFindings;
        var suppressions = effective.AcceptedSuppressions;
        var provisional = verdict.Evidence.VerificationMode == VerificationMode.Analysis ||
            verdict.Evidence.VerificationMode == VerificationMode.Unspecified && !verdict.Evidence.IsAuthoritative;
        var prefix = Headline(verdict);
        var counts = errors + " errors, " + warnings + " warnings" + (notes > 0 ? ", " + notes + " notes" : "") +
            (suppressions > 0 ? ", " + suppressions + " accepted suppression" + (suppressions == 1 ? "" : "s") : "");
        return prefix + ": " + counts + (provisional ? ". Configured release tests were not run as a complete verification gate." :
            verdict.Evidence.IsAuthoritative ? "." : ". Release evidence is incomplete.");
    }

    public static string Headline(AssayVerdict verdict) => verdict.Kind switch
        {
            AssayVerdictKind.ToolFailure => "Tool failed",
            AssayVerdictKind.Inconclusive => "Verification incomplete",
            _ when verdict.Evidence.VerificationMode == VerificationMode.Analysis ||
                verdict.Evidence.VerificationMode == VerificationMode.Unspecified && !verdict.Evidence.IsAuthoritative => "Analysis only",
            AssayVerdictKind.Fail => "Verification failed",
            _ => "Verification passed"
        };

    public static DiagnosticCounts Counts(ImmutableArray<PresentedFinding> findings) => new(
        findings.Count(f => !f.AcceptedSuppression && f.Level == "error"),
        findings.Count(f => !f.AcceptedSuppression && f.Level == "warning" && f.Finding.Scope == "shell"),
        findings.Count(f => !f.AcceptedSuppression && f.Level == "warning" && f.Finding.Scope == "default"),
        findings.Count(f => !f.AcceptedSuppression && f.Level == "warning" && f.Finding.Scope is not ("shell" or "default")),
        findings.Count(f => !f.AcceptedSuppression && f.Level == "note"),
        findings.Count(f => f.AcceptedSuppression));

    public static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsControl(character) || character is '\u2028' or '\u2029' or '\u202a' or '\u202b' or '\u202c' or '\u202d' or '\u202e' or '\u2066' or '\u2067' or '\u2068' or '\u2069' ? ' ' : character);
        }
        return builder.ToString();
    }
}
